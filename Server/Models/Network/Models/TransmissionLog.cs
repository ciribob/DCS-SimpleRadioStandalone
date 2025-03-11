using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network.Models
{
    class TransmissionLog
    {
        public string TransmissionFrequency { get; set; }
        public DateTime TransmissionStart { get; set; }
        public DateTime TransmissionEnd { get; set; }

        public TransmissionLog(DateTime time, string frequency)
        {
            TransmissionFrequency = frequency;
            TransmissionStart = time;
            TransmissionEnd = time;
        }

        public bool IsComplete() => DateTime.Now.Ticks - TransmissionEnd.Ticks > 4000000 ? true : false;
    }
}
