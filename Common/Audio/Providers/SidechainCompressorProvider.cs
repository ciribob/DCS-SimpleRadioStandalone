using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class SidechainCompressorProvider : ISampleProvider
    {
        public SidechainCompressor Compressor { get; set; }
        public ISampleProvider SidechainProvider { get; set; }
        public ISampleProvider SignalProvider { get; set; }
        public WaveFormat WaveFormat => SignalProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var floatPool = ArrayPool<float>.Shared;
            var signalCount = SignalProvider.Read(buffer, offset, count);

            if (signalCount == 0)
                return 0;

            var sidechainBuffer = floatPool.Rent(signalCount);
            buffer.AsSpan(offset, signalCount).CopyTo(sidechainBuffer.AsSpan(0, signalCount));
            var sidechainCount = SidechainProvider.Read(sidechainBuffer, 0, signalCount);

            var processedCount = Math.Min(signalCount, sidechainCount);

            for (var i = 0; i < processedCount; ++i)
            {
                buffer[offset + i] = (float)Compressor.Process(sidechainBuffer[i], buffer[offset + i]);
            }

            floatPool.Return(sidechainBuffer);

            return processedCount;
        }
    }
}
