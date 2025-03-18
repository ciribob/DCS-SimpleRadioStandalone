using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Microsoft.Win32;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
    public enum ProfileSettingsKeys
    {
        Radio1Channel,
        Radio2Channel,
        Radio3Channel,
        Radio4Channel,
        Radio5Channel,
        Radio6Channel,
        Radio7Channel,
        Radio8Channel,
        Radio9Channel,
        Radio10Channel,
        IntercomChannel,

        RadioEffects,
        RadioEncryptionEffects, //Radio Encryption effects
        RadioEffectsClipping,
        NATOTone,

        RadioRxEffects_Start, // Recieving Radio Effects
        RadioRxEffects_End,
        RadioTxEffects_Start, // Recieving Radio Effects
        RadioTxEffects_End,

        AutoSelectPresetChannel, //auto select preset channel

        AlwaysAllowHotasControls,
        AllowDCSPTT,
        RadioSwitchIsPTT,


        AlwaysAllowTransponderOverlay,
        RadioSwitchIsPTTOnlyWhenValid,

        MIDSRadioEffect, //if on and Radio TX effects are on the MIDS tone is used
        
        PTTReleaseDelay,

        RadioTransmissionStartSelection,
        RadioTransmissionEndSelection,
        HAVEQUICKTone,
        RadioBackgroundNoiseEffect,
        NATOToneVolume,
        HQToneVolume,
        FMNoiseVolume,
        VHFNoiseVolume,
        UHFNoiseVolume,
        HFNoiseVolume,

        PTTStartDelay,

        RotaryStyleIncrement,
        IntercomTransmissionStartSelection,
        IntercomTransmissionEndSelection,
        AMCollisionVolume,
        AmbientCockpitNoiseEffect,
        AmbientCockpitNoiseEffectVolume,
        AmbientCockpitIntercomNoiseEffect,
        DisableExpansionRadios
    }

    public class ProfileSettingsStore
    {
        private static readonly object _lock = new object();
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //cache all the settings in their correct types for speed
        //fixes issue where we access settings a lot and have issues
        private ConcurrentDictionary<string, object> _settingsCache = new ConcurrentDictionary<string, object>();

        public string CurrentProfileName
        {
            get => _currentProfileName;
            set
            {
                _settingsCache.Clear();
                _currentProfileName = value;

            }
        }

        public string Path { get; }

        public static readonly Dictionary<string, string> DefaultSettingsProfileSettings = new Dictionary<string, string>()
        {
            {ProfileSettingsKeys.RadioEffects.ToString(), "true"},
            {ProfileSettingsKeys.RadioEffectsClipping.ToString(), "false"},

            {ProfileSettingsKeys.RadioEncryptionEffects.ToString(), "true"},
            {ProfileSettingsKeys.NATOTone.ToString(), "true"},
            {ProfileSettingsKeys.HAVEQUICKTone.ToString(), "true"},

            {ProfileSettingsKeys.RadioRxEffects_Start.ToString(), "true"},
            {ProfileSettingsKeys.RadioRxEffects_End.ToString(), "true"},

            {ProfileSettingsKeys.RadioTransmissionStartSelection.ToString(), CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START+".wav"},
            {ProfileSettingsKeys.RadioTransmissionEndSelection.ToString(), CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END+".wav"},


            {ProfileSettingsKeys.RadioTxEffects_Start.ToString(), "true"},
            {ProfileSettingsKeys.RadioTxEffects_End.ToString(), "true"},
            {ProfileSettingsKeys.MIDSRadioEffect.ToString(), "true"},

            {ProfileSettingsKeys.AutoSelectPresetChannel.ToString(), "true"},

            {ProfileSettingsKeys.AlwaysAllowHotasControls.ToString(),"false" },
            {ProfileSettingsKeys.AllowDCSPTT.ToString(),"true" },
            {ProfileSettingsKeys.RadioSwitchIsPTT.ToString(), "false"},
            {ProfileSettingsKeys.RadioSwitchIsPTTOnlyWhenValid.ToString(), "false"},
            {ProfileSettingsKeys.AlwaysAllowTransponderOverlay.ToString(), "false"},

            {ProfileSettingsKeys.PTTReleaseDelay.ToString(), "0"},
            {ProfileSettingsKeys.PTTStartDelay.ToString(), "0"},

            {ProfileSettingsKeys.RadioBackgroundNoiseEffect.ToString(), "true"},

            {ProfileSettingsKeys.NATOToneVolume.ToString(), "1.2"},
            {ProfileSettingsKeys.HQToneVolume.ToString(), "0.3"},

            {ProfileSettingsKeys.VHFNoiseVolume.ToString(), "0.15"},
            {ProfileSettingsKeys.HFNoiseVolume.ToString(), "0.15"},
            {ProfileSettingsKeys.UHFNoiseVolume.ToString(), "0.15"},
            {ProfileSettingsKeys.FMNoiseVolume.ToString(), "0.4"},

            {ProfileSettingsKeys.AMCollisionVolume.ToString(), "1.0"},

            {ProfileSettingsKeys.RotaryStyleIncrement.ToString(), "false"},

            {ProfileSettingsKeys.AmbientCockpitNoiseEffect.ToString(), "true"},
            {ProfileSettingsKeys.AmbientCockpitNoiseEffectVolume.ToString(), "1.0"}, //relative volume as the incoming volume is variable
            {ProfileSettingsKeys.AmbientCockpitIntercomNoiseEffect.ToString(), "false"},
            {ProfileSettingsKeys.DisableExpansionRadios.ToString(), "false"},
        };


        public List<string> ProfileNames
        {
            get
            {
                return new List<string>(InputProfiles.Keys);
            }
            
        }

        public Dictionary<InputBinding, InputDevice> GetCurrentInputProfile()
        {
            return InputProfiles[GetProfileName(CurrentProfileName)];
        }

        public Configuration GetCurrentProfile()
        {
            return InputConfigs[GetProfileCfgFileName(CurrentProfileName)];
        }
        public Dictionary<string, Dictionary<InputBinding, InputDevice>> InputProfiles { get; set; } = new Dictionary<string, Dictionary<InputBinding, InputDevice>>();

        private Dictionary<string, Configuration> InputConfigs = new Dictionary<string, Configuration>();

        private readonly GlobalSettingsStore _globalSettings;
        private string _currentProfileName = "default";

        public ProfileSettingsStore(GlobalSettingsStore globalSettingsStore)
        {
            this._globalSettings = globalSettingsStore;
            this.Path = _globalSettings.Path;

            MigrateOldSettings();

            var profiles = GetProfiles();
            foreach (var profile in profiles)
            {
                Configuration _configuration = null;
                try
                {
                    int count = 0;
                    while (GlobalSettingsStore.IsFileLocked(new FileInfo(Path + GetProfileCfgFileName(profile))) && count <10)
                    {
                        Thread.Sleep(200);
                        count++;
                    }
                    _configuration = Configuration.LoadFromFile(Path+GetProfileCfgFileName(profile));
                    InputConfigs[GetProfileCfgFileName(profile)] = _configuration;

                    var inputProfile = new Dictionary<InputBinding, InputDevice>();
                    InputProfiles[GetProfileName(profile)] = inputProfile;

                    foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
                    {
                        var device = GetControlSetting(bind, _configuration);

                        if (device != null)
                        {
                            inputProfile[bind] = device;
                        }
                    }

                    _configuration.SaveToFile(Path+GetProfileCfgFileName(profile), Encoding.UTF8);
                
                }
                catch (FileNotFoundException)
                {
                    Logger.Info(
                        $"Did not find input config file at path {profile}, initialising with default config");
                }
                catch (ParserException)
                {
                    Logger.Info(
                        $"Error with input config - creating a new default ");
                }

                if (_configuration == null)
                {
                    _configuration = new Configuration();
                    var inputProfile = new Dictionary<InputBinding, InputDevice>();
                    InputProfiles[GetProfileName(profile)] = inputProfile;
                    InputConfigs[GetProfileCfgFileName(profile)] = new Configuration();
                    _configuration.SaveToFile(Path+GetProfileCfgFileName(profile), Encoding.UTF8);

                }
            }

            //add default
            if (!InputProfiles.ContainsKey(GetProfileName("default")))
            {
                InputConfigs[GetProfileCfgFileName("default")] = new Configuration();

                var inputProfile = new Dictionary<InputBinding, InputDevice>();
                InputProfiles[GetProfileName("default")] = inputProfile;

                InputConfigs[GetProfileCfgFileName("default")].SaveToFile(GetProfileCfgFileName("default"));
            }
        }

        private void MigrateOldSettings()
        {
            try
            {
                //combine global.cfg and input-default.cfg
                if (File.Exists(Path+"input-default.cfg") && File.Exists(Path + "global.cfg") && !File.Exists("default.cfg"))
                {
                    //Copy the current GLOBAL settings - not all relevant but will be ignored
                    File.Copy(Path + "global.cfg", Path + "default.cfg");

                    var inputText = File.ReadAllText(Path + "input-default.cfg", Encoding.UTF8);

                    File.AppendAllText(Path + "default.cfg", inputText, Encoding.UTF8);

                    Logger.Info(
                        $"Migrated the previous input-default.cfg and global settings to the new profile");
                }
                else
                {
                    Logger.Info(
                        $"No need to migrate - migration complete");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"Error migrating input profiles");
            }
          
        }

        public List<string> GetProfiles()
        {
            var profiles = _globalSettings.SettingsProfiles;

            if (profiles == null || profiles.Length == 0 || !profiles.Contains("default"))
            {
                profiles = new[] { "default" };
                _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles);
            }

            return new List<string>(profiles);
        }

        public void AddNewProfile(string profileName)
        {
            var profiles = InputProfiles.Keys.ToList();
            profiles.Add(profileName);

            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            InputConfigs[GetProfileCfgFileName(profileName)] = new Configuration();

            var inputProfile = new Dictionary<InputBinding, InputDevice>();
            InputProfiles[GetProfileName(profileName)] = inputProfile;
        }

        private string GetProfileCfgFileName(string prof)
        {
            if (prof.Contains(".cfg"))
            {
                return prof;
            }

            return  prof + ".cfg";
        }

        private string GetProfileName(string cfg)
        {
            if (cfg.Contains(".cfg"))
            {
                return cfg.Replace(".cfg","");
            }

            return cfg;
        }

        public InputDevice GetControlSetting(InputBinding key, Configuration configuration)
        {
            if (!configuration.Contains(key.ToString()))
            {
                return null;
            }

            try
            {
                var device = new InputDevice();
                device.DeviceName = configuration[key.ToString()]["name"].StringValue;

                device.Button = configuration[key.ToString()]["button"].IntValue;
                device.InstanceGuid =
                    Guid.Parse(configuration[key.ToString()]["guid"].RawValue);
                device.InputBind = key;

                device.ButtonValue = configuration[key.ToString()]["value"].IntValue;

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

            var configuration = GetCurrentProfile();

            configuration.Add(new Section(device.InputBind.ToString()));

            //create the sections
            var section = configuration[device.InputBind.ToString()];

            section.Add(new Setting("name", device.DeviceName.Replace("\0", "")));
            section.Add(new Setting("button", device.Button));
            section.Add(new Setting("value", device.ButtonValue));
            section.Add(new Setting("guid", device.InstanceGuid.ToString()));

            var inputDevices = GetCurrentInputProfile();

            inputDevices[device.InputBind] = device;

            Save();
        }

        public void RemoveControlSetting(InputBinding binding)
        {
            var configuration = GetCurrentProfile();

            if (configuration.Contains(binding.ToString()))
            {
                configuration.Remove(binding.ToString());
            }

            var inputDevices = GetCurrentInputProfile();
            inputDevices.Remove(binding);

            Save();
        }

        private Setting GetSetting(string section, string setting)
        {
            var _configuration = GetCurrentProfile();

            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(setting))
            {
                if (DefaultSettingsProfileSettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, DefaultSettingsProfileSettings[setting]));

                    Save();
                }
                else if (DefaultSettingsProfileSettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, DefaultSettingsProfileSettings[setting]));

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

        public bool GetClientSettingBool(ProfileSettingsKeys key)
        {

            if (_settingsCache.TryGetValue(key.ToString(), out var val))
            {
                return (bool)val;
            }

            var setting = GetSetting("Client Settings", key.ToString());
            if (setting.RawValue.Length == 0)
            {
                _settingsCache[key.ToString()] = false;
                return false;
            }

            _settingsCache[key.ToString()] = setting.BoolValue;

            return setting.BoolValue;
        }

        public float GetClientSettingFloat(ProfileSettingsKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(),out var val))
            {
                if (val == null)
                {
                    return 0f;
                }
                return (float) val;
            }

            var setting =  GetSetting("Client Settings", key.ToString()).FloatValue;

            _settingsCache[key.ToString()] = setting;

            return setting;
        }

        public string GetClientSettingString(ProfileSettingsKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val))
            {
                return (string)val;
            }

            var setting = GetSetting("Client Settings", key.ToString()).RawValue;

            _settingsCache[key.ToString()] = setting;

            return setting;
        }


        public void SetClientSettingBool(ProfileSettingsKeys key, bool value)
        {
            SetSetting("Client Settings", key.ToString(), value);

            _settingsCache.TryRemove(key.ToString(), out var res);
        }

        public void SetClientSettingFloat(ProfileSettingsKeys key, float value)
        {
            SetSetting("Client Settings", key.ToString(), value);

            _settingsCache.TryRemove(key.ToString(), out var res);
        }
        public void SetClientSettingString(ProfileSettingsKeys key, string value)
        {
            SetSetting("Client Settings", key.ToString(), value);
            _settingsCache.TryRemove(key.ToString(), out var res);
        }

        private void SetSetting(string section, string key, object setting)
        {
            var _configuration = GetCurrentProfile();

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
                if (setting is bool)
                {
                    _configuration[section][key].BoolValue = (bool)setting;
                }
                else if (setting is float)
                {
                    _configuration[section][key].FloatValue = (float)setting;
                }
                else if (setting is double)
                {
                    _configuration[section][key].DoubleValue = (double)setting;
                }
                else if (setting is int)
                {
                    _configuration[section][key].DoubleValue = (int)setting;
                }
                else if (setting.GetType() == typeof(string))
                {
                    _configuration[section][key].StringValue = setting as string;
                }
                else if (setting is string[])
                {
                    _configuration[section][key].StringValueArray = setting as string[];
                }
                else
                {
                    Logger.Error("Unknown Setting Type - Not Saved ");
                }

            }

            Save();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var configuration = GetCurrentProfile();
                    configuration.SaveToFile(Path+GetProfileCfgFileName(CurrentProfileName));
                }
                catch (Exception)
                {
                    Logger.Error("Unable to save settings!");
                }
            }
        }

        public void RemoveProfile(string profile)
        {
            InputConfigs.Remove(GetProfileCfgFileName(profile));
            InputProfiles.Remove(GetProfileName(profile));

            var profiles = InputProfiles.Keys.ToList();
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            try
            {
                File.Delete(Path+GetProfileCfgFileName(profile));
            }
            catch
            { }

            CurrentProfileName = "default";
        }

        public void RenameProfile(string oldName,string newName)
        {
            InputConfigs[GetProfileCfgFileName(newName)] = InputConfigs[GetProfileCfgFileName(oldName)];
            InputProfiles[GetProfileName(newName)]= InputProfiles[GetProfileName(oldName)];

            InputConfigs.Remove(GetProfileCfgFileName(oldName));
            InputProfiles.Remove(GetProfileName(oldName));

            var profiles = InputProfiles.Keys.ToList();
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            CurrentProfileName = "default";

            InputConfigs[GetProfileCfgFileName(newName)].SaveToFile(GetProfileCfgFileName(newName));

            try
            {
                File.Delete(Path+GetProfileCfgFileName(oldName));
            }
            catch
            { }
        }

        public void CopyProfile(string profileToCopy, string profileName)
        {
            var config = Configuration.LoadFromFile(Path+GetProfileCfgFileName(profileToCopy));
            InputConfigs[GetProfileCfgFileName(profileName)] = config;

            var inputProfile = new Dictionary<InputBinding, InputDevice>();
            InputProfiles[GetProfileName(profileName)] = inputProfile;

            foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
            {
                var device = GetControlSetting(bind, config);

                if (device != null)
                {
                    inputProfile[bind] = device;
                }
            }

            var profiles = InputProfiles.Keys.ToList();
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            CurrentProfileName = "default";

            InputConfigs[GetProfileCfgFileName(profileName)].SaveToFile(Path+GetProfileCfgFileName(profileName));

        }
    }
}
