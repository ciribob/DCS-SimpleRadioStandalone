using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    // https://dsp.stackexchange.com/a/93451
    internal class FirstOrderFilter : IFilter
    {
        private double b0;
        private double b1;
        private double a1;

        private float x_n1;
        private float y_n1;

        private void SetCoefficients(double aa0, double bb0, double bb1, double aa1)
        {
            b0 = bb0 / aa0;
            b1 = bb1 / aa0;
            a1 = aa1 / aa0;
        }

        public static FirstOrderFilter LowPass(float sampleRate, float cutoffFrequency)
        {
            // H(s) = 1 / (1 + s)
            var filter = new FirstOrderFilter();
            var w0 = 2 * Math.PI * cutoffFrequency / sampleRate;
            var (sinw0, cosw0) = Math.SinCos(w0);

            var a0 = sinw0 + 1 + cosw0;
            var a1 = sinw0 - 1 - cosw0;
            var b0 = sinw0;
            var b1 = sinw0;

            filter.SetCoefficients(a0, b0, b1, a1);
            return filter;
        }

        public static FirstOrderFilter HighPass(float sampleRate, float cutoffFrequency)
        {
            // H(s) = s / (1 + s)
            var filter = new FirstOrderFilter();
            var w0 = 2 * Math.PI * cutoffFrequency / sampleRate;
            var (sinw0, cosw0) = Math.SinCos(w0);

            var a0 = sinw0 + 1 + cosw0;
            var a1 = sinw0 - 1 - cosw0;
            var b0 = 1 + cosw0;
            var b1 = -1 - cosw0;

            filter.SetCoefficients(a0, b0, b1, a1);
            return filter;
        }

        public float Transform(float input)
        {
            y_n1 = (float)(b0 * input + b1 * x_n1 - a1 * y_n1);
            x_n1 = input;

            return y_n1;
        }
    }
}
