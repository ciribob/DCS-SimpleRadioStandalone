using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class RadioInformation
    {
        public enum EncryptionMode
        {
            NO_ENCRYPTION = 0,
            ENCRYPTION_JUST_OVERLAY = 1,
            ENCRYPTION_FULL = 2,
            ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE = 3

            // 0  is no controls
            // 1 is FC3 Gui Toggle + Gui Enc key setting
            // 2 is InCockpit toggle + Incockpit Enc setting
            // 3 is Incockpit toggle + Gui Enc Key setting
        }

        public enum VolumeMode
        {
            COCKPIT = 0,
            OVERLAY = 1,
        }

        public enum FreqMode
        {
            COCKPIT = 0,
            OVERLAY = 1,
        }

        public enum RetransmitMode
        {
            COCKPIT = 0,
            OVERLAY = 1,
            DISABLED = 2,
        }

        public enum Modulation
        {
            AM = 0,
            FM = 1,
            INTERCOM = 2,
            DISABLED = 3,
            HAVEQUICK = 4,
            SATCOM = 5,
            MIDS = 6,
            SINCGARS = 7
        }

        public bool enc = false; // encryption enabled
        public byte encKey = 0;

        [JsonNetworkIgnoreSerialization]
        public EncryptionMode encMode = EncryptionMode.NO_ENCRYPTION;

        [JsonDCSIgnoreSerialization]
        [JsonNetworkIgnoreSerialization]
        public double freqMax = 1;

        [JsonDCSIgnoreSerialization]
        [JsonNetworkIgnoreSerialization]
        public double freqMin = 1;

        public double freq = 1;
        
        public Modulation modulation = Modulation.DISABLED;

        [JsonNetworkIgnoreSerialization]
        public string name = "";
        
        public double secFreq = 1;

        [JsonNetworkIgnoreSerialization]
        [JsonDCSIgnoreSerialization]
        public RetransmitMode rtMode = RetransmitMode.DISABLED;

        //should the radio restransmit?
        public bool retransmit = false;

        [JsonNetworkIgnoreSerialization]
        public float volume = 1.0f;

        [JsonNetworkIgnoreSerialization]
        [JsonDCSIgnoreSerialization]
        public FreqMode freqMode = FreqMode.COCKPIT;

        [JsonNetworkIgnoreSerialization]
        [JsonDCSIgnoreSerialization]
        public FreqMode guardFreqMode = FreqMode.COCKPIT;

        [JsonNetworkIgnoreSerialization]
        [JsonDCSIgnoreSerialization]
        public VolumeMode volMode = VolumeMode.COCKPIT;
        [JsonNetworkIgnoreSerialization]
        [JsonDCSIgnoreSerialization]
        public bool expansion = false;

        [JsonNetworkIgnoreSerialization]
        public int channel = -1;

        [JsonNetworkIgnoreSerialization]
        public bool simul = false;

        /**
         * Used to determine if we should send an update to the server or not
         * We only need to do that if something that would stop us Receiving happens which
         * is frequencies and modulation
         */

        public override bool Equals(object obj)
        {
            if ((obj == null) || (GetType() != obj.GetType()))
                return false;

            var compare = (RadioInformation) obj;

            if (!name.Equals(compare.name))
            {
                return false;
            }
            if (!DCSPlayerRadioInfo.FreqCloseEnough(freq , compare.freq))
            {
                return false;
            }
            if (modulation != compare.modulation)
            {
                return false;
            }
            if (enc != compare.enc)
            {
                return false;
            }
            if (encKey != compare.encKey)
            {
                return false;
            }
            if (retransmit != compare.retransmit)
            {
                return false;
            }
            if (!DCSPlayerRadioInfo.FreqCloseEnough(secFreq, compare.secFreq))
            {
                return false;
            }
            //if (volume != compare.volume)
            //{
            //    return false;
            //}
            //if (freqMin != compare.freqMin)
            //{
            //    return false;
            //}
            //if (freqMax != compare.freqMax)
            //{
            //    return false;
            //}


            return true;
        }

        internal RadioInformation Copy()
        {
            //probably can use memberswise clone
            return new RadioInformation()
            {
                channel = this.channel,
                enc = this.enc,
                encKey = this.encKey,
                encMode = this.encMode,
                expansion = this.expansion,
                freq = this.freq,
                freqMax = this.freqMax,
                freqMin = this.freqMin,
                freqMode = this.freqMode,
                guardFreqMode = this.guardFreqMode,
                modulation = this.modulation,
                secFreq = this.secFreq,
                name = this.name,
                simul = this.simul,
                volMode = this.volMode,
                volume = this.volume,
                retransmit = this.retransmit,
                rtMode = this.rtMode

            };
        }
    }
}