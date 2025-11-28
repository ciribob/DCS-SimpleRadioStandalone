using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System;
using System.Buffers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;

public class JitterBufferAudio
{
    public float[] Audio { get; internal init; }

    public ulong PacketNumber { get; internal init; }

    public int ReceivedRadio { get; internal init; }

    public Modulation Modulation { get; internal init; }

    public bool Decryptable { get; internal init; }

    public float Volume { get; internal init; }
    public bool IsSecondary { get; internal init; }

    public double Frequency { get; internal init; }
    public bool NoAudioEffects { get; internal init; }

    public string Guid { get; internal init; }
    public string OriginalClientGuid { get; internal init; }
    public short Encryption { get; internal init; }
    public double ReceivingPower { get; internal init; }
    public float LineOfSightLoss { get; internal init; }
    public Ambient Ambient { get; internal init; }
}