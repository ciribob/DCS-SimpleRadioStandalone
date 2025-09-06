using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class CachedEffectProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            var processed = 0;
            do
            {
                var availableSamples = Effect.AudioEffectFloat.Length - Position;
                var samplesToCopy = Math.Min(availableSamples, count - processed);
                Array.Copy(Effect.AudioEffectFloat, Position, buffer, offset + processed, samplesToCopy);
                Position += samplesToCopy;

                if (Position == Effect.AudioEffectFloat.Length)
                {
                    Position = PositionRollover(Position, Effect.AudioEffectFloat.Length);
                }

                processed += samplesToCopy;
            } while (processed < count);

            return processed;
        }

        public bool Enabled { get; set; } = true;
        public bool Active => Enabled && Effect.Loaded;


        private int Position { get; set; } = 0;


        private CachedAudioEffect Effect;

        public CachedEffectProvider(CachedAudioEffect Effect)
        {
            this.Effect = Effect;
        }

        protected virtual int PositionRollover(int position, int toneLength)
        {
            if (position == toneLength)
            {
                position = 0;
            }

            return position;
        }
    }
}
