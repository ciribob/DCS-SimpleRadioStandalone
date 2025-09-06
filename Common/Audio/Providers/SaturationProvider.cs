using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Numerics;
using System.Runtime.Intrinsics;

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

            var vectorSize = Vector<float>.Count;
            var remainder = samplesRead % vectorSize;
            var v_threshold = new Vector<float>(_thresholdLinear);
            var v_gain = new Vector<float>(_gainLinear);

            for (var i = 0; i < samplesRead - remainder; i += vectorSize)
            {
                var v_samples = Vector.LoadUnsafe(ref buffer[0], (nuint)(offset + i));
                var v_passing = Vector.LessThan<float>(Vector.Abs(v_samples), v_threshold);
                var v_samplesGain = v_samples * v_gain;

                Vector.ConditionalSelect(v_passing, v_samplesGain, v_samples).CopyTo(buffer, offset + i);
            }

            // at most vectorSize - 1.
            for (var i = samplesRead - remainder; i < samplesRead; ++i)
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
