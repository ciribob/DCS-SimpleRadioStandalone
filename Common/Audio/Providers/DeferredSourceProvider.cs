using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class DeferredSourceProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);

        private ISampleProvider _source;
        public ISampleProvider Source
        {
            get => _source;
            set
            {
                if (value != null)
                {
                    if (!value.WaveFormat.Equals(WaveFormat))
                    {
                        throw new ArgumentException("Source should be a 48 kHz single channel float wave format!");
                    }
                }

                _source = value;
            }
        }
        

        public int Read(float[] buffer, int offset, int count)
        {
           return Source.Read(buffer, offset, count);
        }
    }
}
