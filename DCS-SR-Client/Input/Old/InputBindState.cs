using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Old
{
    public class InputBindState
    {
        public InputDevice MainDevice { get; set; }
        public bool MainDeviceState { get; set; }

        public InputDevice ModifierDevice { get; set; }

        public bool ModifierState { get; set; }

        //overall state of bind - True or false being on or false
        public bool IsActive { get; set; }
    }
}