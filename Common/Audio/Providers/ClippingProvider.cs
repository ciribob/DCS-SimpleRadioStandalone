using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class ClippingProvider : ISampleProvider
    {
        private float Min { get; set; }
        private float Max { get; set; }

        public WaveFormat WaveFormat => Source.WaveFormat;

        private ISampleProvider Source;

        public ClippingProvider(ISampleProvider source, float min, float max)
        {
            Source = source;
            Min = min;
            Max = max;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = Source.Read(buffer, offset, count);
            for (int i = 0; i < count; ++i)
            {
                buffer[offset + i] = Math.Max(Math.Min(buffer[offset + i], Max), Min);
            }
            return samplesRead;
        }
    }
}
