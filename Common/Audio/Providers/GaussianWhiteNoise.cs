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
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);
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
            var odd = (count % 2);
            var pairableCount = count - odd; // Will be count if even, count - 1 if odd.
            for (var i = 0; i < pairableCount; i += 2)
            {
                var (z0, z1) = NextRandoms();
                buffer[offset + i] = (float)z0;
                buffer[offset + i + 1] = (float)z1;
            }

            // Cases:
            // count = 0 (empty), odd = 0. pairableCount = 0, no extra.
            // count = 1, odd = 1, pairableCount = 0, one odd element to process.
            // count = 2, odd = 0, pairableCount = 2, no odd element to process.
            // ...
            // count = 587, odd = 1, pairableCount = 586, one odd element.
            // count = 588, odd = 0, no odd element.

            if (odd != 0)
            {
                // Process odd entry.
                buffer[offset + count - 1] = (float)NextRandoms().Item1;
            }

            return count;
        }
    }
}
