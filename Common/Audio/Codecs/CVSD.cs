using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Codecs
{
    // https://github.com/jgaeddert/liquid-dsp/blob/582f135d1381aacbc36769d779062f33aa697473/src/audio/src/cvsd.c#L124
    internal class CVSD
    {
        private static readonly int BitMask = 0b111;
        private static readonly float Zeta = 1.5f;

        private static readonly float DeltaMin = 0.01f;
        private static readonly float DeltaMax = 1f;

        private float reference = 0f;
        private int bitref = 0;
        private float delta = 0f;
        
        public float Transform(float sample)
        {
            var bit = (reference > sample) ? 0 : 1;
            bitref <<= 1;
            bitref |= bit;
            bitref &= BitMask;

            if (bitref == 0 || bitref == BitMask)
            {
                delta *= Zeta;
            }
            else
            {
                delta /= Zeta;
            }

            delta = Math.Max(Math.Min(delta, DeltaMax), DeltaMin);
            reference += (bit != 0 ? 1 : -1) * delta;

            reference = Math.Max(Math.Min(reference, 1f), -1f);

            return reference;
        }
    }
}
