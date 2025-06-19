using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM.Models;

public class VAICOMMessageWrapper
{
    public bool InhibitTX;

    [JsonIgnore] public long LastReceivedAt;

    public int MessageType; //1 is InhibitTX
}