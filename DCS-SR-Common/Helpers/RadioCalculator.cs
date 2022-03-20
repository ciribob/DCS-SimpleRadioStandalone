using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class RadioCalculator
    {
        public static readonly double HF_FREQUENCY_LOS_IGNORED = 3e+7;
        public static readonly int TransmissonPowerdBm = 40; //10 watts
        public static readonly int RxAntennaGain = 1;
        public static readonly int TxAntennaGain = 1;

        public static readonly double MagicPartiallySolvedFriis = 995267.9264;

        //From https://tsc-60.cellmail.com/tsc-60/TSC-118/rtn_ncs_products_arc164_pdf.pdf
        // AN/ARC-164 UHF Airborne Radio
        public static readonly double RXSensivity = -90; // -80dBm

        public static double FrequencyToWaveLength(double frequency)
        {
            // speed of light /  frequency (hz)
            return 299792458 / frequency;
        }

        public static double FriisTransmissionReceivedPower(double distance, double frequency)
        {
            //Friis equation http://www.daycounter.com/Calculators/Friis-Calculator.phtml
            //Prx= Ptx(dB)+ Gtx(dB)+ Grx(dB)  -  20log(4*PI*d/lambda);

            return TransmissonPowerdBm + RxAntennaGain + TxAntennaGain -
                   20 * Math.Log10(
                       4 * Math.PI * distance /
                       FrequencyToWaveLength(frequency)
                   );
        }

        // TODO - this equation will be used to mix in static audio to the transmission audio
        // static will creep in in the last 20 % and be ramped up in volume based on distance
        public static double FriisMaximumTransmissionRange(double frequency)
        {
            //Friis equation http://www.daycounter.com/Calculators/Friis-Calculator.phtml
            //Prx= Ptx(dB)+ Gtx(dB)+ Grx(dB)  -  20log(4*PI*d/lambda);
            //Re-arranged to give maximum distance at receiving power of -90 based on transmitting power
            //of 40 watts

            //Hard coded value 995267.9264 based on re-arranged Friis with 40dbm transmissing
            return (MagicPartiallySolvedFriis *
                    FrequencyToWaveLength(frequency)) / Math.PI;
        }

        public static double CustomValueFriisMaximumTransmissionRange(double frequency, RadioValues receiving, RadioValues transmitting)
        {
            double partialFriis = 2 * (-receiving.Sensitivity + transmitting.Power + 1 + 1 - 40) / 20 
                * 5 - (receiving.Sensitivity - transmitting.Power - 1 - 1) / 20;

            return (partialFriis * 
                FrequencyToWaveLength(frequency)) / Math.PI;
        }

        //we can hear if the received power is more than the RX sensivity
        //Eventually this will scale the audio volume with distance
        public static bool CanHearTransmission(double distance, double frequency)
        {
            // return FriisTransmissionReceivedPower(distance, frequency) > RXSensivity;
            return FriisMaximumTransmissionRange(frequency) > distance;
        }

        public static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        public static double CalculateDistanceHaversine(DCSLatLngPosition myLatLng, DCSLatLngPosition clientLatLng)
        {
            if (myLatLng.lat == clientLatLng.lat || myLatLng.lng == clientLatLng.lng)
            {
                //the above will cause a divide by 0 error so this is protection against that....
                //should be *almost* impossible...
                return Math.Abs(myLatLng.alt - clientLatLng.alt);
            }

            const double R = 6371e3; // meters
            var φ1 = DegreeToRadian(myLatLng.lat);
            var φ2 = DegreeToRadian(clientLatLng.lat);
            var Δφ = DegreeToRadian(clientLatLng.lat - myLatLng.lat);
            var Δλ = DegreeToRadian(clientLatLng.lng - myLatLng.lng);

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c;
            return Math.Abs(PythagDistance(d, myLatLng.alt - clientLatLng.alt));
        }

        //we have haversine great circle distance - but as they're aircraft we need to offset for height as that gives the real distance
        private static double PythagDistance(double distance, double height)
        {
            height = Math.Abs(height);
            distance = Math.Abs(distance);
            if (height == 0)
            {
                return distance;
            }

            //distance^2 and height^2 
            return Math.Sqrt((distance * distance) + (height * height));

        }
      
    }
}