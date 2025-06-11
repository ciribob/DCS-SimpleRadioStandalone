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

        private float _dryGainLinear = (float)Decibels.DecibelsToLinear(0f);
        private float _wetGainLinear = (float)Decibels.DecibelsToLinear(1f);

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
                var dry = buffer[offset + i];
                var wet = 0f;
                var absDry = Math.Abs(dry);
                if (absDry > _thresholdLinear)
                {
                    // dry clips at threshold.
                    dry = (float)Math.CopySign(_thresholdLinear, dry);


                    // overdrive part.
                    wet = (float)Math.CopySign(absDry - _thresholdLinear, dry) * _wetGainLinear;
                }

                buffer[offset + i] = _gainLinear * _dryGainLinear * dry + wet / _gainLinear;
            }

            return samplesRead;
        }
    }
}
