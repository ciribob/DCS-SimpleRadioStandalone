using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Server;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class ClientSync
    {
        public delegate void ConnectCallback(bool result);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private ConnectCallback _callback;
        private readonly ConcurrentDictionary<string, SRClient> _clients;
        private readonly string _guid;
        private IPEndPoint _serverEndpoint;
        private TcpClient _tcpClient;

        public static string[] ServerSettings = new string[Enum.GetValues(typeof(Server.ServerSettingType)).Length];

        public ClientSync(ConcurrentDictionary<string, SRClient> clients, string guid)
        {
            this._clients = clients;
            this._guid = guid;
        }


        public void TryConnect(IPEndPoint endpoint, ConnectCallback callback)
        {
            this._callback = callback;
            _serverEndpoint = endpoint;

            var tcpThread = new Thread(Connect);
            tcpThread.Start();
        }

        private void Connect()
        {
            var radioSync = new RadioDCSSyncServer(ClientRadioUpdated, ClientCoalitionUpdate, _clients, _guid);
            using (_tcpClient = new TcpClient())
            {
                _tcpClient.SendTimeout = 10;
                try
                {
                    _tcpClient.Connect(_serverEndpoint);

                    if (_tcpClient.Connected)
                    {
                        radioSync.Listen();

                        _tcpClient.NoDelay = true;

                        CallOnMain(true);
                        ClientSyncLoop();
                    }
                }
                catch (SocketException ex)
                {
                    Logger.Error(ex, "error connecting to server");
                }
            }

            radioSync.Stop();

            //disconnect callback
            CallOnMain(false);
        }

        private void ClientRadioUpdated()
        {
            
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = RadioDCSSyncServer.DcsPlayerSideInfo.side,
                    Name = RadioDCSSyncServer.DcsPlayerSideInfo.name,
                    ClientGuid = _guid,
                    RadioInfo = RadioDCSSyncServer.DcsPlayerRadioInfo,
                    Position = RadioDCSSyncServer.DcsPlayerRadioInfo.pos
                },
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE
            });

        }

        private void ClientCoalitionUpdate()
        {
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = RadioDCSSyncServer.DcsPlayerSideInfo.side,
                    Name = RadioDCSSyncServer.DcsPlayerSideInfo.name,
                    Position = RadioDCSSyncServer.DcsPlayerSideInfo.Position,
                    ClientGuid = _guid
                },
                MsgType = NetworkMessage.MessageType.UPDATE
            });
        }

        private void CallOnMain(bool result)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new ThreadStart(delegate { _callback(result); }));
            }
            catch (Exception ex)
            {
            }
        }

        private void ClientSyncLoop()
        {
            //clear the clietns list
            _clients.Clear();

            using (var reader = new StreamReader(_tcpClient.GetStream(), Encoding.UTF8))
            {
                try
                {
                    //start the loop off by sending a SYNC Request
                    SendToServer(new NetworkMessage
                    {
                        Client = new SRClient
                        {
                            Coalition = RadioDCSSyncServer.DcsPlayerSideInfo.side,
                            Name = RadioDCSSyncServer.DcsPlayerSideInfo.name,
                            Position = RadioDCSSyncServer.DcsPlayerSideInfo.Position,
                            ClientGuid = _guid
                        },
                        MsgType = NetworkMessage.MessageType.SYNC
                    });


                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var serverMessage = JsonConvert.DeserializeObject<NetworkMessage>(line);
                            if (serverMessage != null)
                            {
                                switch (serverMessage.MsgType)
                                {
                                    case NetworkMessage.MessageType.PING:
                                        // Do nothing for now
                                        break;
                                    case NetworkMessage.MessageType.UPDATE:

                                        ServerSettings = serverMessage.ServerSettings;

                                        if (_clients.ContainsKey(serverMessage.Client.ClientGuid))
                                        {
                                            var srClient = _clients[serverMessage.Client.ClientGuid];
                                            var updatedSrClient = serverMessage.Client;
                                            if (srClient != null)
                                            {
                                                srClient.LastUpdate = Environment.TickCount;
                                                srClient.Name = updatedSrClient.Name;
                                                srClient.Coalition = updatedSrClient.Coalition;
                                                srClient.LastUpdate = Environment.TickCount;
                                                srClient.Position = updatedSrClient.Position;
//
//                                                Logger.Info("Recevied Update Client: " + NetworkMessage.MessageType.UPDATE + " From: " +
//                                                            lastRadioTransmit.Client.Name + " Coalition: " +
//                                                            lastRadioTransmit.Client.Coalition + " Pos: " + lastRadioTransmit.Client.Position);
                                            }
                                        }
                                        else
                                        {
                                            var connectedClient = serverMessage.Client;
                                            connectedClient.LastUpdate = Environment.TickCount;

                                            //init with LOS true so you can hear them incase of bad DCS install where
                                            //LOS isnt working
                                            connectedClient.HasLineOfSight = true;

                                            _clients[serverMessage.Client.ClientGuid] = connectedClient;

                                            Logger.Info("Recevied New Client: " + NetworkMessage.MessageType.UPDATE + " From: " +
                                                   serverMessage.Client.Name + " Coalition: " +
                                                   serverMessage.Client.Coalition);
                                        }
                                        break;
                                    case NetworkMessage.MessageType.SYNC:
                                        Logger.Info("Recevied: " + NetworkMessage.MessageType.SYNC);

                                        if (serverMessage.Clients != null)
                                        {
                                            foreach (var client in serverMessage.Clients)
                                            {
                                                client.LastUpdate = Environment.TickCount;
                                                //init with LOS true so you can hear them incase of bad DCS install where
                                                //LOS isnt working
                                                client.HasLineOfSight = true;
                                                _clients[client.ClientGuid] = client;
                                            }
                                        }
                                        //add server settings
                                        ServerSettings = serverMessage.ServerSettings;

                                        break;

                                    case NetworkMessage.MessageType.SERVER_SETTINGS:

                                        Logger.Info("Recevied: " + NetworkMessage.MessageType.SERVER_SETTINGS);
                                        ServerSettings = serverMessage.ServerSettings;
                                        break;
                                    default:
                                        Logger.Warn("Recevied unknown " + line);
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Client exception reading from socket ");
                        }

                        // do something with line
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Client exception reading - Disconnecting ");
                }
            }
            //clear the clietns list
            _clients.Clear();

            //disconnect callback
            CallOnMain(false);
        }


        private void SendToServer(NetworkMessage message)
        {
            try
            {
                var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message) + "\n");

                _tcpClient.GetStream().Write(json, 0, json.Length);
                //Need to flush?
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Client exception sending so server");

                Disconnect();
            }
        }

        //implement IDispose? To close stuff properly?
        public void Disconnect()
        {
            try
            {
                if (_tcpClient != null)
                {
                    _tcpClient.Close(); // this'll stop the socket blocking
                }
            }
            catch (Exception ex)
            {
            }

            Logger.Warn("Disconnecting from server");

            //CallOnMain(false);
        }
    }
}