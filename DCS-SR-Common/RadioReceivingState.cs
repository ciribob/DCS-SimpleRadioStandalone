using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class RadioReceivingState
    {
        public double LastReceviedAt { get; set; }

        public bool IsReceiving()
        {
            return (Environment.TickCount - LastReceviedAt) < 200;
        }

        public bool IsSecondary { get; set; }
        public int ReceivedOn { get; set; }

        public bool PlayedEndOfTransmission { get; set; }
        public bool Encrypted { get; set; }
    }
}
