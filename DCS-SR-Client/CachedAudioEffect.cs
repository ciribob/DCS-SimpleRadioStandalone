using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class CachedAudioEffect
    {
        public enum AudioEffectTypes
        {
            RADIO_TX =0,
            RADIO_RX =1,
            KY_58_TX= 2,
            KY_58_RX = 3,
        }

        public AudioEffectTypes AudioEffectType { get; private set; }

        public byte[] AudioEffectBytes { get; private set; }

        public CachedAudioEffect(AudioEffectTypes audioEffect)
        {
            this.AudioEffectType = audioEffect;

            var file = GetFile();

            using (WaveFileReader reader = new WaveFileReader(file))
            {
                //    Assert.AreEqual(16, reader.WaveFormat.BitsPerSample, "Only works with 16 bit audio");
                AudioEffectBytes = new byte[reader.Length];
                int read = reader.Read(AudioEffectBytes, 0, AudioEffectBytes.Length);
            }

        }

        private System.IO.UnmanagedMemoryStream GetFile()
        {
            switch (AudioEffectType)
            {
                case AudioEffectTypes.KY_58_RX:
                    return Properties.Resources.KY_58_RX_PREAMBLE_SHORT_16_1600;
                case AudioEffectTypes.KY_58_TX:
                    return Properties.Resources.KY_58_TX_PREAMBLE_SHORT_16_1600;
                case AudioEffectTypes.RADIO_RX:
                    return Properties.Resources.mic_click_on_16_1600;
                case AudioEffectTypes.RADIO_TX:
                    return Properties.Resources.mic_click_on_16_1600;
                default:
                    return Properties.Resources.mic_click_on_16_1600;
            }

        }

     
    }
}
