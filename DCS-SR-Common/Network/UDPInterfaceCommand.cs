using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network
{
    public class UDPInterfaceCommand
    {
        public enum UDPCommandType
        {
            FREQUENCY_DELTA = 0,
            ACTIVE_RADIO = 1,
            TOGGLE_GUARD = 2,
            CHANNEL_UP = 3,
            CHANNEL_DOWN = 4,
            SET_VOLUME = 5,
            TRANSPONDER_POWER = 6,
            TRANSPONDER_M1_CODE = 7,
            TRANSPONDER_M3_CODE = 8,
            TRANSPONDER_M4 = 9,
            TRANSPONDER_IDENT = 10,
            GUARD = 11, // SET guard
            FREQUENCY_SET = 12,
            TRANSPONDER_M2_CODE = 13,
        }

        public int RadioId { get; set; }
        public double Frequency { get; set; }
        public UDPCommandType Command { get; set; }
        public float Volume { get; set; }

        public bool Enabled { get; set; }

        public int Code { get; set; }
    }
}