using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class NetworkMessage
    {
        public enum MessageType
        {
            UPDATE, //META Data update - No Radio Information
            PING,
            SYNC,
            RADIO_UPDATE, //Only received server side
            SERVER_SETTINGS
        }

        public SRClient Client { get; set; }

        public MessageType MsgType { get; set; }

        public List<SRClient> Clients { get; set; }

        public string[] ServerSettings { get; set; }

    }
}