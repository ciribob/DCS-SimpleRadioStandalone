using NAudio.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    internal class SidechainCompressor : AttRelEnvelope
    {
        private double EnvelopeDB { get; set; } = DC_OFFSET;
        public double MakeUpGain { get; set; } = 0;

        public double Threshold { get; set; } = 0;

        public double Ratio { get; set; } = 1;

        public SidechainCompressor(double attackMilliseconds, double releaseMilliseconds, double sampleRate)
            : base(attackMilliseconds, releaseMilliseconds, sampleRate)
        {
        }

        public double Process(double sideIn, double signalIn)
        {
            // sidechain

            // rectify input
            double rectSideIn = Math.Abs(sideIn); // n.b. was fabs

            // if desired, one could use another EnvelopeDetector to smooth
            // the rectified signal.

            rectSideIn += DC_OFFSET; // add DC offset to avoid log( 0 )
            double keydB = Decibels.LinearToDecibels(rectSideIn); // convert linear -> dB

            // threshold
            double overdB = keydB - Threshold; // delta over threshold
            if (overdB < 0.0)
                overdB = 0.0;

            // attack/release

            overdB += DC_OFFSET; // add DC offset to avoid denormal

            EnvelopeDB = Run(overdB, EnvelopeDB); // run attack/release envelope

            overdB = EnvelopeDB - DC_OFFSET; // subtract DC offset

            // Regarding the DC offset: In this case, since the offset is added before 
            // the attack/release processes, the envelope will never fall below the offset,
            // thereby avoiding denormals. However, to prevent the offset from causing
            // constant gain reduction, we must subtract it from the envelope, yielding
            // a minimum value of 0dB.

            // transfer function
            double gr = overdB * (Ratio - 1.0);	// gain reduction (dB)
            gr = Decibels.DecibelsToLinear(gr) * Decibels.DecibelsToLinear(MakeUpGain); // convert dB -> linear

            // output gain
            return signalIn * gr;	// apply gain reduction to input
        }
    }
}
