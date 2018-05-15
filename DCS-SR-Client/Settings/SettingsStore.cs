using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Microsoft.Win32;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
    public enum SettingsKeys
    {
        //settings
        RadioEffects = 0,
        Radio1Channel = 1,
        Radio2Channel = 2,
        Radio3Channel = 3,
        Radio4Channel = 12,
        Radio5Channel = 13,
        Radio6Channel = 14,
        Radio7Channel = 15,
        Radio8Channel = 16,
        Radio9Channel = 17,
        Radio10Channel = 18,
        RadioSwitchIsPTT = 4,
        IntercomChannel = 5,

        RadioEncryptionEffects = 8, //Radio Encryption effects
        AutoConnectPrompt = 10, //message about auto connect
        RadioOverlayTaskbarHide = 11,
        RefocusDCS = 19,
        ExpandControls = 20,

        RadioEffectsClipping = 21,

        MinimiseToTray = 22,
        StartMinimised = 23,

        RadioRxEffects_Start = 40, // Recieving Radio Effects 
        RadioRxEffects_End = 41,
        RadioTxEffects_Start = 42, // Recieving Radio Effects 
        RadioTxEffects_End = 43,

        AutoSelectPresetChannel = 44, //auto select preset channel

        AudioInputDeviceId = 45,
        AudioOutputDeviceId = 46,
        LastServer = 47,
        MicBoost = 48,
        SpeakerBoost = 49,
        RadioX = 50,
        RadioY = 51,
        RadioSize = 52,
        RadioOpacity = 53,
        RadioWidth = 54,
        RadioHeight = 55,
        ClientX = 56,
        ClientY = 57,
        AwacsX = 58,
        AwacsY = 59,
        MicAudioOutputDeviceId = 60,


        CliendIdShort = 61,
        ClientIdLong = 62,
        DCSLOSOutgoingUDP = 63, //9086
        DCSIncomingUDP = 64, //9084
        CommandListenerUDP = 65, //=9040,
        OutgoingDCSUDPInfo = 66, //7080
        OutgoingDCSUDPOther = 67, //7082
        DCSIncomingGameGUIUDP = 68, // 5068
        DCSLOSIncomingUDP = 69, //9085

        AGC = 70,
        AGCTarget = 71,
        AGCDecrement=72,
        AGCLevelMax = 73,

        Denoise=74,
        DenoiseAttenuation = 75,

        AlwaysAllowHotasControls = 76,
        AllowDCSPTT = 77

    }

    public enum InputBinding
    {
        Intercom = 100,
        ModifierIntercom = 200,

        Switch1 = 101,
        ModifierSwitch1 = 201,

        Switch2 = 102,
        ModifierSwitch2 = 202,

        Switch3 = 103,
        ModifierSwitch3 = 203,

        Switch4 = 104,
        ModifierSwitch4 = 204,

        Switch5 = 105,
        ModifierSwitch5 = 205,

        Switch6 = 106,
        ModifierSwitch6 = 206,

        Switch7 = 107,
        ModifierSwitch7 = 207,

        Switch8 = 108,
        ModifierSwitch8 = 208,

        Switch9 = 109,
        ModifierSwitch9 = 209,

        Switch10 = 110,
        ModifierSwitch10 = 210,

        Ptt = 111,
        ModifierPtt = 211,

        OverlayToggle = 112,
        ModifierOverlayToggle = 212,

        Up100 = 113,
        ModifierUp100 = 213,

        Up10 = 114,
        ModifierUp10 = 214,

        Up1 = 115,
        ModifierUp1 = 215,

        Up01 = 116,
        ModifierUp01 = 216,

        Up001 = 117,
        ModifierUp001 = 217,

        Up0001 = 118,
        ModifierUp0001 = 218,

        Down100 = 119,
        ModifierDown100 = 219,

        Down10 = 120,
        ModifierDown10 = 220,

        Down1 = 121,
        ModifierDown1 = 221,

        Down01 = 122,
        ModifierDown01 = 222,

        Down001 = 123,
        ModifierDown001 = 223,

        Down0001 = 124,
        ModifierDown0001 = 224,

        NextRadio = 125,
        ModifierNextRadio = 225,

        PreviousRadio = 126,
        ModifierPreviousRadio = 226,

        ToggleGuard = 127,
        ModifierToggleGuard = 227,

        ToggleEncryption = 128,
        ModifierToggleEncryption = 228,

        EncryptionKeyIncrease = 129,
        ModifierEncryptionKeyIncrease = 229,

        EncryptionKeyDecrease = 130,
        ModifierEncryptionEncryptionKeyDecrease = 230,

        RadioChannelUp = 131,
        ModifierRadioChannelUp = 231,

        RadioChannelDown = 132,
        ModifierRadioChannelDown = 232,
    }


    public class SettingsStore
    {
        public static readonly string CFG_FILE_NAME = "client.cfg";

        private static readonly object _lock = new object();

        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Configuration _configuration;

        private SettingsStore()
        {
            try
            {
                _configuration = Configuration.LoadFromFile(CFG_FILE_NAME);

                foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
                {
                    var device = GetControlSetting(bind);

                    if (device != null)
                    {
                        InputDevices[bind] = device;
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                _configuration = new Configuration();
                _configuration.Add(new Section("Position Settings"));
                _configuration.Add(new Section("Client Settings"));
                _configuration.Add(new Section("Network Settings"));
            }
        }

        public Dictionary<InputBinding, InputDevice> InputDevices = new Dictionary<InputBinding, InputDevice>();

        public static SettingsStore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsStore();
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, string> defaultSettings = new Dictionary<string, string>()
        {
            {SettingsKeys.RadioEffects.ToString(), "true"},
            {SettingsKeys.RadioEffectsClipping.ToString(), "true"},
            {SettingsKeys.RadioSwitchIsPTT.ToString(), "false"},

            {SettingsKeys.RadioEncryptionEffects.ToString(), "true"},
            {SettingsKeys.AutoConnectPrompt.ToString(), "false"},
            {SettingsKeys.RadioOverlayTaskbarHide.ToString(), "false"},
            {SettingsKeys.RefocusDCS.ToString(), "false"},
            {SettingsKeys.ExpandControls.ToString(), "false"},

            {SettingsKeys.MinimiseToTray.ToString(), "true"},
            {SettingsKeys.StartMinimised.ToString(), "false"},

            {SettingsKeys.RadioRxEffects_Start.ToString(), "true"},
            {SettingsKeys.RadioRxEffects_End.ToString(), "true"},
            {SettingsKeys.RadioTxEffects_Start.ToString(), "true"},
            {SettingsKeys.RadioTxEffects_End.ToString(), "true"},

            {SettingsKeys.AutoSelectPresetChannel.ToString(), "true"},

            {SettingsKeys.AudioInputDeviceId.ToString(), ""},
            {SettingsKeys.AudioOutputDeviceId.ToString(), ""},
            {SettingsKeys.MicAudioOutputDeviceId.ToString(), ""},

            {SettingsKeys.LastServer.ToString(), "127.0.0.1"},

            {SettingsKeys.MicBoost.ToString(), "0.514"},
            {SettingsKeys.SpeakerBoost.ToString(), "0.514"},

            {SettingsKeys.RadioX.ToString(), "300"},
            {SettingsKeys.RadioY.ToString(), "300"},
            {SettingsKeys.RadioSize.ToString(), "1.0"},
            {SettingsKeys.RadioOpacity.ToString(), "1.0"},

            {SettingsKeys.RadioWidth.ToString(), "122"},
            {SettingsKeys.RadioHeight.ToString(), "270"},

            {SettingsKeys.ClientX.ToString(), "500"},
            {SettingsKeys.ClientY.ToString(), "500"},

            {SettingsKeys.AwacsX.ToString(), "300"},
            {SettingsKeys.AwacsY.ToString(), "300"},

            {SettingsKeys.CliendIdShort.ToString(), ShortGuid.NewGuid().ToString()},
            {SettingsKeys.ClientIdLong.ToString(), Guid.NewGuid().ToString()},

            {SettingsKeys.DCSLOSOutgoingUDP.ToString(), "9086"},
            {SettingsKeys.DCSIncomingUDP.ToString(), "9084"},
            {SettingsKeys.CommandListenerUDP.ToString(), "9040"},
            {SettingsKeys.OutgoingDCSUDPInfo.ToString(), "7080"},
            {SettingsKeys.OutgoingDCSUDPOther.ToString(), "7082"},
            {SettingsKeys.DCSIncomingGameGUIUDP.ToString(), "5068"},
            {SettingsKeys.DCSLOSIncomingUDP.ToString(), "9085"},

            {SettingsKeys.AGC.ToString(), "true"},
            {SettingsKeys.AGCTarget.ToString(), "30000"},
            {SettingsKeys.AGCDecrement.ToString(), "-60"},
            {SettingsKeys.AGCLevelMax.ToString(),"68" },

            {SettingsKeys.Denoise.ToString(),"true" },
            {SettingsKeys.DenoiseAttenuation.ToString(),"-30" },

            {SettingsKeys.AlwaysAllowHotasControls.ToString(),"false" },
            {SettingsKeys.AllowDCSPTT.ToString(),"true" },

        };

        public InputDevice GetControlSetting(InputBinding key)
        {
            var sectionString = "Control settings";

            if (!_configuration.Contains(key.ToString()))
            {
                return null;
            }

            try
            {
                var device = new InputDevice();
                device.DeviceName = _configuration[key.ToString()]["name"].StringValue;

                device.Button = _configuration[key.ToString()]["button"].IntValue;
                device.InstanceGuid =
                    Guid.Parse(_configuration[key.ToString()]["guid"].RawValue);
                device.InputBind = key;

                device.ButtonValue = _configuration[key.ToString()]["value"].IntValue;

                return device;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error reading input device saved settings ");
            }


            return null;
        }

        public void SetControlSetting(InputDevice device)
        {
            RemoveControlSetting(device.InputBind);

            _configuration.Add(new Section(device.InputBind.ToString()));

            //create the sections
            var section = _configuration[device.InputBind.ToString()];

            section.Add(new Setting("name", device.DeviceName.Replace("\0", "")));
            section.Add(new Setting("button", device.Button));
            section.Add(new Setting("value", device.ButtonValue));
            section.Add(new Setting("guid", device.InstanceGuid.ToString()));

            InputDevices[device.InputBind] = device;

            Save();
        }

        public void RemoveControlSetting(InputBinding binding
        )
        {
            if (_configuration.Contains(binding.ToString()))
            {
                _configuration.Remove(binding.ToString());
            }

            InputDevices.Remove(binding);
        }

        public Setting GetPositionSetting(SettingsKeys key)
        {
            return GetSetting("Position Settings", key.ToString());
        }

        public void SetPositionSetting(SettingsKeys key, double value)
        {
            SetSetting("Position Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
        }

        public Setting GetClientSetting(SettingsKeys key)
        {
            return GetSetting("Client Settings", key.ToString());
        }

        public void SetClientSetting(SettingsKeys key, string value)
        {
            SetSetting("Client Settings", key.ToString(), value);
        }

        public int GetNetworkSetting(SettingsKeys key)
        {
            return GetSetting("Network Settings", key.ToString()).IntValue;
        }

        public void SetNetworkSetting(SettingsKeys key, int value)
        {
            SetSetting("Network Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
        }

        private Setting GetSetting(string section, string setting)
        {
            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(setting))
            {
                if (defaultSettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, defaultSettings[setting]));

                    Save();
                }
                else
                {
                    _configuration[section]
                        .Add(new Setting(setting, ""));
                    Save();
                }
            }

            return _configuration[section][setting];
        }

        private void SetSetting(string section, string key, string setting)
        {
            if (setting == null)
            {
                setting = "";
            }
            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(key))
            {
                _configuration[section].Add(new Setting(key, setting));
            }
            else
            {
                _configuration[section][key].StringValue = setting;
            }

            Save();
        }

        private static SettingsStore _instance;

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    _configuration.SaveToFile(CFG_FILE_NAME);
                }
                catch (Exception ex)
                {
                    Logger.Error("Unable to save settings!");
                }
            }
        }
    }
}