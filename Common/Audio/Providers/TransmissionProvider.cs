using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class TransmissionProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);
        private Memory<float> Buffer { get; set; }
        public TransmissionProvider(float[] buffer, int offset, int count)
        {
           Buffer = new Memory<float>(buffer, offset, count - offset);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var available = Math.Min(Buffer.Length, count);
            Buffer.Span.Slice(0, available).CopyTo(new Span<float>(buffer, offset, available));
            if (available < count)
            {
                Array.Clear(buffer, offset + available, count - available);
            }
            return count;
        }
    }
}
