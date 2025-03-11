using System.Net;
using System.Net.Sockets;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    public class PendingPacket
    {
        public IPEndPoint ReceivedFrom { get; set; }
        public byte[] RawBytes { get; set; }
    }
}