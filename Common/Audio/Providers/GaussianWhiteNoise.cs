using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class GaussianWhiteNoise : ISampleProvider
    {
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);
        private Random random = new Random();

        // Generate normal distribution via Box-Mueller.
        private (double, double) NextRandoms()
        {
            var u1 = Math.Max(random.NextDouble(), 1e-9);
            var u2 = Math.Max(random.NextDouble(), 1e-9);

            var radius = Math.Sqrt(-2 * Math.Log(u1));
            var theta = 2 * Math.PI * u2;

            var (z1, z0) = Math.SinCos(theta);
            return (z0 * radius, z1 * radius);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // Generate values in pairs.
            for (var i = 0; i < count; i += 2)
            {
                var (z0, z1) = NextRandoms();
                buffer[offset + i] = (float)z0;
                buffer[offset + i + 1] = (float)z1;
            }

            if (count % 2 != 0)
            {
                // Process odd entry.
                buffer[offset + count - 1] = (float)NextRandoms().Item1;
            }

            return count;
        }
    }
}
