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
    public class TransmissionSegment : IDisposable
    {
        // Non-owning - belongs to a pool!
        private float[] Audio { get; set; }
        private int AudioLength { get; set; }

        public Span<float> AudioSpan => Audio.AsSpan(0, AudioLength);
        public bool HasEncryption { get; private set; }
        public bool Decryptable { get; private set; }
        public string OriginalClientGuid { get; private set; }
        public string ClientGuid { get; private set; }
        public bool IsSecondary {  get; private set; }
        public Modulation Modulation { get; private set; }
        public double ReceivingPower { get; private set; }
        public bool NoAudioEffects { get; private set; }

        private static readonly ArrayPool<float> Pool = ArrayPool<float>.Shared;

        public TransmissionSegment(DeJitteredTransmission transmission)
        {
            Audio = Pool.Rent(transmission.PCMAudioLength);
            AudioLength = transmission.PCMAudioLength;

            transmission.PCMMonoAudio.AsSpan(0, transmission.PCMAudioLength).CopyTo(AudioSpan);
            
            HasEncryption = transmission.Encryption > 0;
            Decryptable = transmission.Decryptable;
            OriginalClientGuid = transmission.OriginalClientGuid;
            IsSecondary = transmission.IsSecondary;
            Modulation = transmission.Modulation;
            ClientGuid = transmission.Guid;
            ReceivingPower = transmission.ReceivingPower;
            NoAudioEffects = transmission.NoAudioEffects;
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Pool.Return(Audio);
                Audio = null;
                AudioLength = 0;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TransmissionSegment()
        {
            Dispose(false);
        }
    }
}
