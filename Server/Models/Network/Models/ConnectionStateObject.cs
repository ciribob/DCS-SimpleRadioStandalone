using System.Net.Sockets;
using System.Text;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network.Models
{
    public class ConnectionStateObject
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
}