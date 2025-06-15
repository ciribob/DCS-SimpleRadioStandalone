using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class VolumeCachedEffectProvider : ISampleProvider
    {
        private readonly VolumeSampleProvider volumeProvider;
        private readonly CachedEffectProvider effectProvider;

        public WaveFormat WaveFormat => volumeProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            return volumeProvider.Read(buffer, offset, count);
        }

        public VolumeCachedEffectProvider(CachedEffectProvider effectProvider)
        {
            this.effectProvider = effectProvider;
            volumeProvider = new VolumeSampleProvider(effectProvider);
        }

        public float Volume
        {
            get { return volumeProvider.Volume; }
            set
            {
                volumeProvider.Volume = value;
            }
        }

        public bool Enabled
        {
            get { return effectProvider.Enabled; }
            set { effectProvider.Enabled = value; }
        }

        public bool Active => effectProvider.Active;
    }
}
