using NAudio.Wave;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

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

            var vectorSize = Vector<float>.Count;
            var remainder = samplesRead % vectorSize;
            var v_max = new Vector<float>(Max);
            var v_min = new Vector<float>(Min);

            for (var i = 0; i < samplesRead - remainder; i += vectorSize)
            {
                var samples_v = Vector.LoadUnsafe(ref buffer[0], (nuint)(offset + i));

                samples_v = Vector.Max(v_min, Vector.Min(samples_v, v_max));
                samples_v.CopyTo(buffer, offset + i);
            }

            // at most vectorSize - 1.
            for (var i = samplesRead - remainder; i < samplesRead; ++i)
            {
                buffer[offset + i] = Math.Max(Math.Min(buffer[offset + i], Max), Min);
            }
            
            return samplesRead;
        }
    }
}
