using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Common;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils
{
    public static class RadioHelper
    {
        public static void ToggleGuard(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio != null && radioId > 0)
            {
                if ((radio.freqMode == RadioInformation.FreqMode.OVERLAY 
                     || radio.guardFreqMode == RadioInformation.FreqMode.OVERLAY) 
                    && radio.modulation != RadioInformation.Modulation.MIDS)
                {
                    if (radio.secFreq > 0)
                    {
                        radio.secFreq = 0; // 0 indicates we want it overridden + disabled
                    }
                    else
                    {
                        radio.secFreq = 1; //indicates we want it back
                    }

                    //make radio data stale to force resysnc
                    ClientStateSingleton.Instance.LastSent = 0;
                }
            }
        }

        public static void SetGuard(int radioId, bool enabled)
        {
            var radio = GetRadio(radioId);

            if (radio != null && radioId > 0)
            {
                if ((radio.freqMode == RadioInformation.FreqMode.OVERLAY
                     || radio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                    && radio.modulation != RadioInformation.Modulation.MIDS)
                {
                    if (!enabled)
                    {
                        radio.secFreq = 0; // 0 indicates we want it overridden + disabled
                    }
                    else
                    {
                        radio.secFreq = 1; //indicates we want it back
                    }

                    //make radio data stale to force resysnc
                    ClientStateSingleton.Instance.LastSent = 0;
                }
            }
        }

        public static bool UpdateRadioFrequency(double frequency, int radioId, bool delta = true, bool inMHz = true)
        {
            bool inLimit = true;
            const double MHz = 1000000;

            if (inMHz)
            {
                frequency = frequency * MHz;
            }

            var radio = GetRadio(radioId);

            if (radio != null && radioId > 0)
            {
                if (radio.modulation != RadioInformation.Modulation.DISABLED
                    && radio.modulation != RadioInformation.Modulation.INTERCOM
                    && radio.freqMode == RadioInformation.FreqMode.OVERLAY)
                {
                    if (delta)
                    {

                        if (GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RotaryStyleIncrement))
                        {
                            // Easier to simply shift the decimal place value to the ones position for finding numeral at specific position
                            double adjustedFrequency = Math.Abs((int)Math.Round(radio.freq / frequency));

                            double deltaPosition = (adjustedFrequency %  10) - (adjustedFrequency % 1) / 1; // calculate the value of the position where the delta will be applied
                            double rollOverValue = frequency < 0 ? 0 : 9;
                            double futureValue = frequency + radio.freq; // used for checking 10Mhz increments 

                            if (Math.Abs(frequency) <= 1000000)
                            {
                                frequency = deltaPosition == rollOverValue ? frequency *= -9 : frequency;
                            }
                            else if (frequency < 0 && radio.freqMin > futureValue)
                            {
                                frequency = 0;
                            }
                            else if (futureValue > radio.freqMax)
                            {
                                frequency = 0;
                            }
                        }
                        
                        radio.freq = (int)Math.Round(radio.freq + frequency);
                    }
                    else
                    {
                        radio.freq = (int)Math.Round(frequency);
                    }

                    //make sure we're not over or under a limit
                    if (radio.freq > radio.freqMax)
                    {
                        inLimit = false;
                        radio.freq = radio.freqMax;
                    }
                    else if (radio.freq < radio.freqMin)
                    {
                        inLimit = false;
                        radio.freq = radio.freqMin;
                    }

                    //set to no channel
                    radio.channel = -1;

                    //make radio data stale to force resysnc
                    ClientStateSingleton.Instance.LastSent = 0;
                }
            }
            return inLimit;
        }

        public static bool SelectRadio(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio != null)
            {
                if (radio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.DcsPlayerRadioInfo.control ==
                    DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                {
                    ClientStateSingleton.Instance.DcsPlayerRadioInfo.selected = (short) radioId;
                    return true;
                }
            }

            return false;
        }

        public static RadioInformation GetRadio(int radio)
        {
            var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

            if ((dcsPlayerRadioInfo != null) && dcsPlayerRadioInfo.IsCurrent() &&
                radio < dcsPlayerRadioInfo.radios.Length && (radio >= 0))
            {
                return dcsPlayerRadioInfo.radios[radio];
            }

            return null;
        }

        public static void ToggleEncryption(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio != null && radioId > 0)
            {
                if (radio.modulation != RadioInformation.Modulation.DISABLED && radio.modulation != RadioInformation.Modulation.MIDS) // disabled or MIDS
                {
                    //update stuff
                    if (radio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                    {
                        if (radio.enc)
                        {
                            radio.enc = false;
                        }
                        else
                        {
                            radio.enc = true;
                        }

                        //make radio data stale to force resysnc
                        ClientStateSingleton.Instance.LastSent = 0;
                    }
                }
            }
        }

        public static void SetEncryptionKey(int radioId, int encKey)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null &&
                currentRadio.modulation != RadioInformation.Modulation.DISABLED 
                && currentRadio.modulation != RadioInformation.Modulation.MIDS) // disabled & mids
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED) // disabled
                {
                    //update stuff
                    if ((currentRadio.encMode ==
                         RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE) ||
                        (currentRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY))
                    {
                        if (encKey > 252)
                            encKey = 252;
                        else if (encKey < 1)
                            encKey = 1;

                        currentRadio.encKey = (byte) encKey;
                        //make radio data stale to force resysnc
                        ClientStateSingleton.Instance.LastSent = 0;
                    }
                }
            }
        }

        public static void SelectNextRadio()
        {
            var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

            if ((dcsPlayerRadioInfo != null) && dcsPlayerRadioInfo.IsCurrent() &&
                dcsPlayerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
            {
                if (dcsPlayerRadioInfo.selected < 0
                    || dcsPlayerRadioInfo.selected > dcsPlayerRadioInfo.radios.Length
                    || dcsPlayerRadioInfo.selected + 1 > dcsPlayerRadioInfo.radios.Length)
                {
                    SelectRadio(1);

                    return;
                }
                else
                {
                    int currentRadio = dcsPlayerRadioInfo.selected;

                    //find next radio
                    for (int i = currentRadio + 1; i < dcsPlayerRadioInfo.radios.Length; i++)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }

                    //search up to current radio
                    for (int i = 1; i < currentRadio; i++)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }
                }
            }
        }

        public static void SelectPreviousRadio()
        {
            var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

            if ((dcsPlayerRadioInfo != null) && dcsPlayerRadioInfo.IsCurrent() &&
                dcsPlayerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
            {
                if (dcsPlayerRadioInfo.selected < 0
                    || dcsPlayerRadioInfo.selected > dcsPlayerRadioInfo.radios.Length)
                {
                    dcsPlayerRadioInfo.selected = 1;
                    return;
                }
                else
                {
                    int currentRadio = dcsPlayerRadioInfo.selected;

                    //find previous radio
                    for (int i = currentRadio - 1; i > 0; i--)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }

                    //search down to current radio
                    for (int i = dcsPlayerRadioInfo.radios.Length; i > currentRadio; i--)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }
                }
            }
        }

        public static void IncreaseEncryptionKey(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null)
            {
                SetEncryptionKey(radioId, currentRadio.encKey + 1);
            }
        }

        public static void DecreaseEncryptionKey(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null)
            {
                SetEncryptionKey(radioId, currentRadio.encKey - 1);
            }
        }

        public static void SelectRadioChannel(PresetChannel selectedPresetChannel, int radioId)
        {
            if (UpdateRadioFrequency((double) selectedPresetChannel.Value, radioId, false, false))
            {
                var radio = GetRadio(radioId);

                if (radio != null) radio.channel = selectedPresetChannel.Channel;
            }
        }

        public static void RadioChannelUp(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null && radioId > 0)
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.DcsPlayerRadioInfo.control ==
                    DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                {
                    var fixedChannels = ClientStateSingleton.Instance.FixedChannels;

                    //now get model
                    if (fixedChannels != null && fixedChannels[radioId - 1] != null)
                    {
                        var radioChannels = fixedChannels[radioId - 1];

                        if (radioChannels.PresetChannels.Count > 0)
                        {
                            int next = currentRadio.channel + 1;

                            if (radioChannels.PresetChannels.Count < next || currentRadio.channel < 1)
                            {
                                //set to first radio
                                SelectRadioChannel(radioChannels.PresetChannels[0], radioId);
                                radioChannels.SelectedPresetChannel = radioChannels.PresetChannels[0];
                            }
                            else
                            {
                                var preset = radioChannels.PresetChannels[next - 1];

                                SelectRadioChannel(preset, radioId);
                                radioChannels.SelectedPresetChannel = preset;
                            }
                        }
                    }
                }
            }
        }

        public static void RadioChannelDown(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null && radioId > 0)
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.DcsPlayerRadioInfo.control ==
                    DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                {
                    var fixedChannels = ClientStateSingleton.Instance.FixedChannels;

                    //now get model
                    if (fixedChannels != null && fixedChannels[radioId - 1] != null)
                    {
                        var radioChannels = fixedChannels[radioId - 1];

                        if (radioChannels.PresetChannels.Count > 0)
                        {
                            int previous = currentRadio.channel - 1;

                            if (previous < 1)
                            {
                                //set to last radio
                                SelectRadioChannel(radioChannels.PresetChannels.Last(), radioId);
                                radioChannels.SelectedPresetChannel = radioChannels.PresetChannels.Last();
                            }
                            else
                            {
                                var preset = radioChannels.PresetChannels[previous - 1];
                                //set to previous radio
                                SelectRadioChannel(preset, radioId);
                                radioChannels.SelectedPresetChannel = preset;
                            }
                        }
                    }
                }
            }
        }

        public static void SetRadioVolume(float volume, int radioId)
        {
            if (volume > 1.0)
            {
                volume = 1.0f;
            }else if (volume < 0)
            {
                volume = 0;
            }

            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null
                && currentRadio.modulation != RadioInformation.Modulation.DISABLED
                && currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {
                currentRadio.volume = volume;
            }
        }

        public static void ToggleRetransmit(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio != null && radioId > 0)
            {
                if (radio.rtMode == RadioInformation.RetransmitMode.OVERLAY)
                {
                    radio.retransmit = !radio.retransmit;

                    //make radio data stale to force resysnc
                    ClientStateSingleton.Instance.LastSent = 0;
                }
            }

        }

        public static void RadioVolumeUp(short radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null
                && currentRadio.modulation != RadioInformation.Modulation.DISABLED
                && currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {

                var volume = currentRadio.volume;

                volume += 0.1f; 

                if (volume > 1.0)
                {
                    volume = 1.0f;
                }
              
                currentRadio.volume = volume;
            }
         

        }

        public static void RadioVolumeDown(short radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null
                && currentRadio.modulation != RadioInformation.Modulation.DISABLED
                && currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {

                var volume = currentRadio.volume;

                volume -= 0.1f;

                if (volume < 0)
                {
                    volume = 0f;
                }

                currentRadio.volume = volume;
            }
        }
    }
}