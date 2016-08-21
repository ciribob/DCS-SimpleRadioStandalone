using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI;
using Newtonsoft.Json;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        public string guid;
        // Received data string.
        public StringBuilder sb = new StringBuilder();
        // Client  socket.
        public Socket workSocket;
    }

    internal class ServerSync: IHandle<ServerSettingsChangedMessage>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        // Thread signal.
        public static ManualResetEvent _allDone = new ManualResetEvent(false);
        private readonly HashSet<IPAddress> _bannedIps;
        private readonly IEventAggregator _eventAggregator;

        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();

        private Socket listener;

        private ServerSettings _serverSettings;

        public ServerSync(ConcurrentDictionary<string, SRClient> connectedClients, HashSet<IPAddress> _bannedIps, IEventAggregator eventAggregator)
        {
            _clients = connectedClients;
            this._bannedIps = _bannedIps;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _serverSettings = ServerSettings.Instance;;
        }

        public void StartListening()
        {
            var ipAddress = new IPAddress(0);
            var localEndPoint = new IPEndPoint(ipAddress, 5002);

            // Create a TCP/IP socket.
            listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    _allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    _logger.Info("Waiting for a connection...");
                    listener.BeginAccept(
                        AcceptCallback,
                        listener);

                    // Wait until a connection is made before continuing.
                    _allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Server Listen error");
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            _allDone.Set();

            try
            {
                // Get the socket that handles the client request.
                var listener = (Socket) ar.AsyncState;
                var handler = listener.EndAccept(ar);
                handler.NoDelay = true;

                // Create the state object.
                var state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    ReadCallback, state);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error accepting socket");
            }
        }

        public void HandleDisconnect(StateObject state)
        {
            _logger.Info("Disconnecting Client");

            if (state != null && state.guid != null)
            {
                //removed
                SRClient client;
                _clients.TryRemove(state.guid, out client);

                if (client != null)
                {
                    _logger.Info("Removed Client " + state.guid);
                }

                try
                {
                    _eventAggregator.PublishOnUIThread(new ServerStateMessage(true, new List<SRClient>(_clients.Values)));
                }
                catch (Exception ex)
                {
                    _logger.Info(ex, "Exception Publishing Client Update After Disconnect");
                }
            }

            try
            {
                state.workSocket.Close();
            }
            catch (Exception ex)
            {
                _logger.Info(ex, "Exception closing socket after disconnect");
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            var state = (StateObject) ar.AsyncState;
            var handler = state.workSocket;

            try
            {
                // Read data from the client socket. 
                var bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(Encoding.UTF8.GetString(
                        state.buffer, 0, bytesRead));

                    var content = state.sb.ToString();
                    if (content.EndsWith("\n"))
                    {
                        //Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                        //   content.Length, content);

                        try
                        {
                            var message = JsonConvert.DeserializeObject<NetworkMessage>(content);

                            HandleMessage(state, message);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Server - Client Exception reading");
                        }

                        //clear the state buffer
                        state.sb.Clear();

                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            ReadCallback, state);
                    }
                    else
                    {
                        // Not all data received. Get more.
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            ReadCallback, state);
                    }
                }
                else
                {
                    HandleDisconnect(state);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading from socket. Disconnecting ");

                HandleDisconnect(state);
            }
        }

        public void HandleMessage(StateObject state, NetworkMessage message)
        {
            try
            {
                var clientIp = (IPEndPoint) state.workSocket.RemoteEndPoint;

                if (_bannedIps.Contains(clientIp.Address))
                {
                    state.workSocket.Disconnect(true);

                    _logger.Warn("Disconnecting Banned Client -  " + clientIp.Address + " " + clientIp.Port);
                    return;
                }

                //  logger.Info("Received From " + clientIp.Address + " " + clientIp.Port);
                // logger.Info("Recevied: " + message.MsgType);

                switch (message.MsgType)
                {

                 
                    case NetworkMessage.MessageType.PING:
                        // Do nothing for now
                        break;
                    case NetworkMessage.MessageType.UPDATE:
                        HandleClientMetaDataUpdate(message);
                        break;
                    case NetworkMessage.MessageType.RADIO_UPDATE:
                        HandleClientMetaDataUpdate(message);
                        HandleClientRadioUpdate(message);
                        break;
                    case NetworkMessage.MessageType.SYNC:

                        var srClient = message.Client;
                        if (!_clients.ContainsKey(srClient.ClientGuid))
                        {
                            srClient.ClientSocket = state.workSocket;

                            //add to proper list
                            _clients[srClient.ClientGuid] = srClient;

                            state.guid = srClient.ClientGuid;

                            _eventAggregator.PublishOnUIThread(new ServerStateMessage(true, new List<SRClient>(_clients.Values)));
                        }

                        HandleRadioClientsSync(clientIp, state.workSocket, message);

                    
                        break;
                    case NetworkMessage.MessageType.SERVER_SETTINGS:
                        HandleServerSettingsMessage(state.workSocket);
                        break;
                    default:
                        _logger.Warn("Recevied unknown message type");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception Handling Message " + ex.Message);
            }
        }

        private void HandleServerSettingsMessage(Socket socket)
        {
            //send server settings
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.SERVER_SETTINGS,
                ServerSettings = _serverSettings.ServerSetting

            };
            Send(socket, replyMessage);

        }

        private void HandleClientMetaDataUpdate(NetworkMessage message)
        {
            if (_clients.ContainsKey(message.Client.ClientGuid))
            {
                var client = _clients[message.Client.ClientGuid];

                if (client != null)
                {
                    //copy the data we need
                    client.LastUpdate = Environment.TickCount;
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.Position = message.Client.Position;
                   

                    //send update to everyone
                    //Remove Client Radio Info
                    var replyMessage = new NetworkMessage
                    {
                        MsgType = NetworkMessage.MessageType.UPDATE,
                        ServerSettings = _serverSettings.ServerSetting,
                    
                        Client = new SRClient()
                        {
                            ClientGuid = client.ClientGuid,
                            Coalition = client.Coalition,
                            Name = client.Name,
                            LastUpdate = client.LastUpdate,
                            Position = client.Position,
                        }
                    };

                    foreach (var clientToSent in _clients)
                    {
                        Send(clientToSent.Value.ClientSocket, replyMessage);
                    }
                }
            }
        }


        private void HandleClientRadioUpdate(NetworkMessage message)
        {
            if (_clients.ContainsKey(message.Client.ClientGuid))
            {
                var client = _clients[message.Client.ClientGuid];

                if (client != null)
                {
                    //update to local ticks
                    message.Client.RadioInfo.LastUpdate = Environment.TickCount;

                    client.LastUpdate = Environment.TickCount;
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.RadioInfo = message.Client.RadioInfo;
                    client.Position = message.Client.Position;
                    

                    _logger.Info("Received Radio Update");
                }
            }
        }


        private void HandleRadioClientsSync(IPEndPoint clientIp, Socket clientSocket, NetworkMessage message)
        {
            //store new client
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.SYNC,
                Clients = new List<SRClient>(),
                ServerSettings = _serverSettings.ServerSetting
            };

            foreach (var clientToSent in _clients)
            {
                replyMessage.Clients.Add(clientToSent.Value);
            }

            Send(clientSocket, replyMessage);
        }

        private static void Send(Socket handler, NetworkMessage message)
        {
            try
            {
                // Convert the string data to byte data using ASCII encoding.
                var byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message) + "\n");

                // Begin sending the data to the remote device.
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    SendCallback, handler);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception Sending Message " + ex.Message);
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var handler = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.
                var bytesSent = handler.EndSend(ar);
                //  Console.WriteLine("Sent {0} bytes to client.", bytesSent);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Exception SendCallback ");
            }
        }

        public void RequestStop()
        {
            try
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        client.Value.ClientSocket.Close();
                    }
                    catch (Exception ex)
                    {
                    }
                }
                listener.Close();

                _clients.Clear();
            }
            catch (Exception ex)
            {
            }
        }

        public void Handle(ServerSettingsChangedMessage message)
        {
            foreach (var clientToSent in _clients)
            {
                try
                {
                    HandleServerSettingsMessage(clientToSent.Value.ClientSocket);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Exception Sending Server Settings ");
                }
               
            }
        }
    }
}