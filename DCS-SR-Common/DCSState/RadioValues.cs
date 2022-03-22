using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState
{
    public class RadioValues
    {
        public byte Power { get; }
        public int Sensitivity { get; } // this could probably be changed to a byte to express the abs value

        public RadioValues(byte power, int sensitivity = -90)
        {
            Power = power;
            Sensitivity = sensitivity;
        }
    }
}
