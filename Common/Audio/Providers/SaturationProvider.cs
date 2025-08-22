using NAudio.Utils;
using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class SaturationProvider : ISampleProvider
    {
        private ISampleProvider source;

        private float _gainLinear;
        public float GainDB
        {
            get
            {
                return (float)Decibels.LinearToDecibels(_gainLinear);
            }
            set
            {
                _gainLinear = (float)Decibels.DecibelsToLinear(value);
            }
        }

        private float _thresholdLinear;
        public float ThresholdDB
        {
            get
            {
                return (float)Decibels.LinearToDecibels(_thresholdLinear);
            }
            set
            {
                _thresholdLinear = (float)Decibels.DecibelsToLinear(value);
            }
        }

        public WaveFormat WaveFormat => source.WaveFormat;
        public SaturationProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = source.Read(buffer, offset, count);
            for (int i = 0; i < count; ++i)
            {
                var sample = buffer[offset + i];
                var absSample = Math.Abs(sample);
                if (absSample < _thresholdLinear)
                {
                    buffer[offset + i] = sample * _gainLinear;
                }
            }

            return samplesRead;
        }
    }
}
