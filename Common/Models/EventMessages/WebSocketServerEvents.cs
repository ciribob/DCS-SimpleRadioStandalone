using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages
{
    public class WebSocketServerStarted
    {
        public WebSocketVoiceServer Server { get; }
        public WebSocketServerStarted(WebSocketVoiceServer server) => Server = server;
    }

    public class RecordingClientRegistered
    {
        public string ClientId { get; }
        public RecordingClientRegistered(string clientId) => ClientId = clientId;
    }

    public class RecordingClientDeregistered
    {
        public string ClientId { get; }
        public RecordingClientDeregistered(string clientId) => ClientId = clientId;
    }
}
