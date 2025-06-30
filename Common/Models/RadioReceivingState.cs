using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public class RadioReceivingState
{
    public long LastReceivedAt { get; set; }

    public bool IsSecondary { get; set; }
    public bool IsSimultaneous { get; set; }
    public int ReceivedOn { get; set; }

    public bool PlayedEndOfTransmission { get; set; }

    public string SentBy { get; set; }

    public bool IsReceiving => TimeSpan.FromTicks(DateTime.Now.Ticks - LastReceivedAt).TotalMilliseconds < 350;
}