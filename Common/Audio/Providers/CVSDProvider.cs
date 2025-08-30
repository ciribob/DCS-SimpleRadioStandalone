using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Codecs;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class CVSDProvider : ISampleProvider
    {
        private ISampleProvider source;
        private CVSD cvsd = new CVSD();

        public WaveFormat WaveFormat => source.WaveFormat;

        public CVSDProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = source.Read(buffer, offset, count);
            for (int i = 0; i < samplesRead; ++i)
            {
                buffer[offset + i] = cvsd.Transform(buffer[offset + i]);
            }

            return samplesRead;
        }
    }
}
