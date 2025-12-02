using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;

//TODO profile if its better as class or struct
public struct DeJitteredTransmission
{
    public int ReceivedRadio { get; internal init; }

    public Modulation Modulation { get; internal init; }

    public bool Decryptable { get; internal init; }
    public short Encryption { get; internal init; }

    public float Volume { get; internal init; }
    public bool IsSecondary { get; internal init; }

    public double Frequency { get; internal init; }

    public float[] PCMMonoAudio { get; set; }

    public int PCMAudioLength { get; set; }
    public bool NoAudioEffects { get; internal init; }

    public string Guid { get; internal init; }

    public string OriginalClientGuid { get; internal init; }
    public double ReceivingPower { get; internal init; }
    public float LineOfSightLoss { get; internal init; }
    public Ambient Ambient { get; internal init; }
}