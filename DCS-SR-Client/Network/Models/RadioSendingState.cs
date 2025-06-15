using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.Models;

public class RadioSendingState
{
    [JsonIgnore] public long LastSentAt { get; set; }

    public bool IsSending { get; set; }

    public int SendingOn { get; set; }

    public int IsEncrypted { get; set; }
}