using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using NetCoreServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public class SRSClientSession : TcpSession
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly HashSet<IPAddress> _bannedIps;

        // Received data string.
        private readonly StringBuilder _receiveBuffer = new StringBuilder();

        public string SRSGuid { get; set; }

        /// <summary>
        /// Stores remote address. Only available after connect.
        /// </summary>
        private string RemoteAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Stores remote port. Only available after connect.
        /// </summary>
        private string RemotePort { get; set; } = string.Empty;

        public SRSClientSession(ServerSync server, ConcurrentDictionary<string, SRClient> client, HashSet<IPAddress> bannedIps) : base(server)
        {
            _bannedIps = bannedIps;
        }

        protected override void OnConnected()
        {
            var clientIp = (IPEndPoint)Socket.RemoteEndPoint;
            RemoteAddress = clientIp.Address.ToString();
            RemotePort = clientIp.Port.ToString();

            LogOperation("Client connected");
            
            if (_bannedIps.Contains(clientIp.Address))
            {
                Logger.Warn("Disconnecting Banned Client -  " + clientIp.Address + " " + clientIp.Port);

                Disconnect();
            }
        }

        protected override void OnSent(long sent, long pending)
        {
            // Disconnect slow client with 50MB send buffer
            if (pending > 5e+7)
            {
                Logger.Error($"Disconnecting - pending is too large");
                Disconnect();
            }
        }

        protected override void OnDisconnected()
        {
            LogOperation("Client disconnected");
            RemoteAddress = "";
            RemotePort = "";
            
            _receiveBuffer.Clear();
            ((ServerSync)Server).HandleDisconnect(this);
        }

        private List<NetworkMessage> GetNetworkMessage()
        {
            List<NetworkMessage> messages = new List<NetworkMessage>();
            //search for a \n, extract up to that \n and then remove from buffer
            var content = _receiveBuffer.ToString();
            while (content.Length > 2 && content.Contains("\n"))
            {
                //extract message
                var message = content.Substring(0, content.IndexOf("\n", StringComparison.Ordinal) + 1);

                //now clear from buffer
                _receiveBuffer.Remove(0, message.Length);

                try
                {

                    var networkMessage = (JsonConvert.DeserializeObject<NetworkMessage>(message.Trim()));
                    //trim the received part
                    messages.Add(networkMessage);
                }
                catch (Exception ex)
                {
                    LogOperation("Get network message");
                    Logger.Error(ex, $"Unable to process JSON: \n {message}");
                }


                //load in next part
                content = _receiveBuffer.ToString();
            }

            return messages;
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _receiveBuffer.Append(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));

            foreach (var s in GetNetworkMessage())
            {
                ((ServerSync)Server).HandleMessage(this, s);

            }
        }

        protected override void OnTrySendException( Exception ex)
        {
            Logger.Error(ex,$"Caught Client Session Exception");
        }

        protected override void OnError(SocketError error)
        {
            Logger.Error($"Caught Socket Error: {error}");
        }

        /// <summary>
        /// Logs information about the remote ip and port attempting to perform some operation.
        /// </summary>
        /// <param name="operation">
        /// Plain text name of the operation.
        /// </param>
        private void LogOperation(string operation)
        {
            Logger.Info($"{operation}: IP {RemoteAddress} | Port {RemotePort}");
        }
    }
}
