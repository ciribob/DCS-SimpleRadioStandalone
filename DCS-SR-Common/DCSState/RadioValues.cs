using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState
{
    public class RadioValues
    {
        public int Power { get; }
        public int Sensitivity { get; }

        public RadioValues(int power, int sensitivity = -90)
        {
            Power = power;
            Sensitivity = sensitivity;
        }
    }
}
