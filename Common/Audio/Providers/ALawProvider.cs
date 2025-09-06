using NAudio.Codecs;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class ALawProvider : ISampleProvider
    {
        private ISampleProvider source;
        public WaveFormat WaveFormat => source.WaveFormat;

        public ALawProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = source.Read(buffer, offset, count);
            for (int i = 0; i < samplesRead; ++i)
            {
                // A-law works with 13-bit signed integer samples - 2^13 = 8192.
                // This'll introduce some additional 'loss precsion' contributing to the effect..
                buffer[offset + i] = ALawDecoder.ALawToLinearSample(ALawEncoder.LinearToALawSample((short)(buffer[offset + i] * 8192))) / 8192f;
            }

            return samplesRead;
        }
    }
}
