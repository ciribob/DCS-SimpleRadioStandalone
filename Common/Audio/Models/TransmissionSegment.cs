using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models
{
    // https://stackoverflow.com/questions/538060/proper-use-of-the-idisposable-interface#538238
    public class TransmissionSegment
    {
        // Non-owning - belongs to a pool!
        public float[] Audio { get; }
        public bool HasEncryption { get; }
        public bool Decryptable { get; }
        public string OriginalClientGuid { get; }
        public string ClientGuid { get; }
        public bool IsSecondary {  get; }
        public Modulation Modulation { get; }
        public double ReceivingPower { get; }
        public bool NoAudioEffects { get; }

        public TransmissionSegment(DeJitteredTransmission transmission)
        {
            Audio = new float[transmission.PCMAudioLength];

            transmission.PCMMonoAudio.AsSpan(0, transmission.PCMAudioLength).CopyTo(Audio);
            
            HasEncryption = transmission.Encryption > 0;
            Decryptable = transmission.Decryptable;
            OriginalClientGuid = transmission.OriginalClientGuid;
            IsSecondary = transmission.IsSecondary;
            Modulation = transmission.Modulation;
            ClientGuid = transmission.Guid;
            ReceivingPower = transmission.ReceivingPower;
            NoAudioEffects = transmission.NoAudioEffects;
        }
    }
}
