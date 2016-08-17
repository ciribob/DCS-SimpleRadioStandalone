using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class RadioCalculator
    {
        public static readonly int TransmissonPowerdBm = 40; //10 watts
        public static readonly int RxAntennaGain = 1;
        public static readonly int TxAntennaGain = 1;

        //From https://tsc-60.cellmail.com/tsc-60/TSC-118/rtn_ncs_products_arc164_pdf.pdf
        // AN/ARC-164 UHF Airborne Radio
        public static readonly double RXSensivity = -90; // -80dBm

        public static double FrequencyToWaveLength(double frequency)
        {
            // speed of light /  frequency (hz)
            return 299792458/frequency;
        }

        public static double FriisTransmissionReceivedPower(double distance, double frequency)
        {
            //Friis equation http://www.daycounter.com/Calculators/Friis-Calculator.phtml
            //Prx= Ptx(dB)+ Gtx(dB)+ Grx(dB)  -  20log(4*PI*d/lambda);

            return (TransmissonPowerdBm + RxAntennaGain + TxAntennaGain) -
                   (20*Math.Log10(
                       (4*Math.PI*distance) / 
                            FrequencyToWaveLength(frequency)
                       )
                       );
        }

        //we can hear if the received power is more than the RX sensivity
        //Eventually this will scale the audio volume with distance
        public static bool CanHearTransmission(double distance, double frequency)
        {
            return FriisTransmissionReceivedPower(distance, frequency) > RXSensivity;
        }

        public static double CalculateDistance(DcsPosition from, DcsPosition too)
        {
            return Math.Abs(Math.Sqrt( Math.Pow((too.x - from.x),2) + Math.Pow((too.y - from.y), 2) + Math.Pow((too.z - from.z), 2)));
        }
    }
}
