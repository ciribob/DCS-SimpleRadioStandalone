using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;

//TODO profile if its better as class or struct
public struct DeJitteredTransmission
{
    public int ReceivedRadio { get; set; }

    public Modulation Modulation { get; set; }

    public bool Decryptable { get; set; }
    public short Encryption { get; set; }

    public float Volume { get; set; }
    public bool IsSecondary { get; set; }

    public double Frequency { get; set; }

    public float[] PCMMonoAudio { get; set; }

    public int PCMAudioLength { get; set; }
    public bool NoAudioEffects { get; set; }

    public string Guid { get; set; }

    public string OriginalClientGuid { get; set; }
    public double ReceivingPower { get; internal set; }
    public float LineOfSightLoss { get; internal set; }
    public Ambient Ambient { get; internal set; }
}