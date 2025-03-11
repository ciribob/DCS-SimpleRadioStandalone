using System.Collections.Generic;
using System.Net;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public class OutgoingUDPPackets
    {
        public List<IPEndPoint> OutgoingEndPoints { get; set; }
        public byte[] ReceivedPacket { get; set; }
    }
}