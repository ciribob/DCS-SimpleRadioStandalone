using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex;
using NAudio.Wave;
using System;
using System.Buffers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class SpeexPreprocessorProvider : ISampleProvider, IDisposable
    {
        private static readonly int FRAME_SIZE = Constants.OUTPUT_SAMPLE_RATE * 10 / 1000; // 10ms
        public Preprocessor Preprocessor { get; } = new Preprocessor(FRAME_SIZE, Constants.OUTPUT_SAMPLE_RATE);

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Preprocessor.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SpeexPreprocessorProvider()
        {
            Dispose(false);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // How many full frames we have available for processing
            var frameCount = count / FRAME_SIZE;
            var samples = new Span<float>(buffer, offset, count);
            var shortPool = ArrayPool<short>.Shared;
            var shorts = shortPool.Rent(FRAME_SIZE);
            var shortSegment = new ArraySegment<short>(shorts, 0, FRAME_SIZE);
            for (var frame = 0; frame < frameCount; ++frame)
            {
                var frameSlice = samples.Slice(frame * FRAME_SIZE, FRAME_SIZE);
                for (var i = 0; i < frameSlice.Length; i++)
                {
                    shortSegment[i] = (short)(frameSlice[i] * short.MaxValue);
                }
                Preprocessor.Process(shortSegment);

                // convert back!
                for (var i = 0; i < frameSlice.Length; i++)
                {
                    frameSlice[i] = (float)shortSegment[i] / ((float)short.MaxValue + 1f);
                }

            }
            return count;
        }
    }
}
