using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    public enum InputBinding
    {
        Switch1 = 0,
        Switch2 = 1,
        Switch3 = 2,
        Ptt = 3,

        ModifierSwitch1 = 4,
        ModifierSwitch2 = 5,
        ModifierSwitch3 = 6,
        ModifierPtt = 7,

        OverlayToggle = 8, //all buttons that are "main" buttons will be in hundreds
        ModifierOverlayToggle = 9,

        Intercom = 10,
        ModifierIntercom =11,

    }

    public class InputDevice
    {
     
        public InputBinding InputBind { get; set; }

        public string DeviceName { get; set; }

        public int Button { get; set; }
        public Guid InstanceGuid { get; internal set; }
        public int ButtonValue { get; internal set; }
    }
}