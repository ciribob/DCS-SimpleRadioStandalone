using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Input;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;

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

    RadioEffectsRatio,
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
    NoiseGainDB,
    HFNoiseGainDB,
    PerRadioModelEffects,

    PTTStartDelay,

    RotaryStyleIncrement,
    IntercomTransmissionStartSelection,
    IntercomTransmissionEndSelection,
    AMCollisionVolume,
    AmbientCockpitNoiseEffect,
    AmbientCockpitNoiseEffectVolume,
    AmbientCockpitIntercomNoiseEffect,
    DisableExpansionRadios,
    ServerPresetSelection,
    AllowServerEAMRadioPreset, //sets if the awacs custom radio config can be used
    InstructorMode // allows tuning to Intercoms, only for receiving - cant send
}

public enum ServerPresetConfiguration
{
    USE_SERVER_ONLY_IF_SET,
    USE_CLIENT_ONLY,
    USE_CLIENT_AND_SERVER_IF_SET
}

public class ProfileSettingsStore
{
    private static readonly object _lock = new();

    public static readonly Dictionary<string, string> DefaultSettingsProfileSettings = new()
    {
        { ProfileSettingsKeys.RadioEffectsRatio.ToString(), "1.0" },
        { ProfileSettingsKeys.RadioEffectsClipping.ToString(), "false" },

        { ProfileSettingsKeys.RadioEncryptionEffects.ToString(), "true" },
        { ProfileSettingsKeys.NATOTone.ToString(), "true" },
        { ProfileSettingsKeys.HAVEQUICKTone.ToString(), "true" },

        { ProfileSettingsKeys.RadioRxEffects_Start.ToString(), "true" },
        { ProfileSettingsKeys.RadioRxEffects_End.ToString(), "true" },

        {
            ProfileSettingsKeys.RadioTransmissionStartSelection.ToString(),
            CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_START + ".wav"
        },
        {
            ProfileSettingsKeys.RadioTransmissionEndSelection.ToString(),
            CachedAudioEffect.AudioEffectTypes.RADIO_TRANS_END + ".wav"
        },


        { ProfileSettingsKeys.RadioTxEffects_Start.ToString(), "true" },
        { ProfileSettingsKeys.RadioTxEffects_End.ToString(), "true" },
        { ProfileSettingsKeys.MIDSRadioEffect.ToString(), "true" },

        { ProfileSettingsKeys.AutoSelectPresetChannel.ToString(), "true" },

        { ProfileSettingsKeys.AlwaysAllowHotasControls.ToString(), "false" },
        { ProfileSettingsKeys.AllowDCSPTT.ToString(), "true" },
        { ProfileSettingsKeys.RadioSwitchIsPTT.ToString(), "false" },
        { ProfileSettingsKeys.RadioSwitchIsPTTOnlyWhenValid.ToString(), "false" },
        { ProfileSettingsKeys.AlwaysAllowTransponderOverlay.ToString(), "false" },

        { ProfileSettingsKeys.PTTReleaseDelay.ToString(), "0" },
        { ProfileSettingsKeys.PTTStartDelay.ToString(), "0" },

        { ProfileSettingsKeys.RadioBackgroundNoiseEffect.ToString(), "true" },

        { ProfileSettingsKeys.NATOToneVolume.ToString(), "1.2" },
        { ProfileSettingsKeys.HQToneVolume.ToString(), "0.3" },

        { ProfileSettingsKeys.NoiseGainDB.ToString(), "0" },
        { ProfileSettingsKeys.HFNoiseGainDB.ToString(), "0" },
        { ProfileSettingsKeys.PerRadioModelEffects.ToString(), "true" },

        { ProfileSettingsKeys.AMCollisionVolume.ToString(), "1.0" },

        { ProfileSettingsKeys.RotaryStyleIncrement.ToString(), "false" },

        { ProfileSettingsKeys.AmbientCockpitNoiseEffect.ToString(), "true" },
        {
            ProfileSettingsKeys.AmbientCockpitNoiseEffectVolume.ToString(), "1.0"
        }, //relative volume as the incoming volume is variable
        { ProfileSettingsKeys.AmbientCockpitIntercomNoiseEffect.ToString(), "false" },
        { ProfileSettingsKeys.DisableExpansionRadios.ToString(), "false" },

        //server-only
        //client-only
        //both
        {
            ProfileSettingsKeys.ServerPresetSelection.ToString(),
            nameof(ServerPresetConfiguration.USE_CLIENT_AND_SERVER_IF_SET)
        },
        { ProfileSettingsKeys.AllowServerEAMRadioPreset.ToString(), "true" },
        { ProfileSettingsKeys.InstructorMode.ToString(), "false" },
    
    };

    public static readonly List<string> ServerPresetSettings;

    private readonly GlobalSettingsStore _globalSettings;

    //cache all the settings in their correct types for speed
    //fixes issue where we access settings a lot and have issues
    private readonly ConcurrentDictionary<string, object> _settingsCache = new();

    private readonly Dictionary<string, Configuration> InputConfigs = new();
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private string _currentProfileName = "default";

    static ProfileSettingsStore()
    {
        ServerPresetSettings = new List<string>();
        foreach (var setting in Enum.GetNames(typeof(ServerPresetConfiguration))) ServerPresetSettings.Add(setting);
    }

    public ProfileSettingsStore(GlobalSettingsStore globalSettingsStore)
    {
        _globalSettings = globalSettingsStore;
        Path = GlobalSettingsStore.Path;

        var profiles = GetProfiles();
        foreach (var profile in profiles)
        {
            Configuration _configuration = null;
            try
            {
                var count = 0;
                while (GlobalSettingsStore.IsFileLocked(new FileInfo(Path + GetProfileCfgFileName(profile))) &&
                       count < 10)
                {
                    Thread.Sleep(200);
                    count++;
                }

                _configuration = Configuration.LoadFromFile(Path + GetProfileCfgFileName(profile));
                InputConfigs[GetProfileCfgFileName(profile)] = _configuration;

                var inputProfile = new Dictionary<InputBinding, InputDevice>();
                InputProfiles[GetProfileName(profile)] = inputProfile;

                foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
                {
                    var device = GetControlSetting(bind, _configuration);

                    if (device != null) inputProfile[bind] = device;
                }

                _configuration.SaveToFile(Path + GetProfileCfgFileName(profile), Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                Logger.Info(
                    $"Did not find input config file at path {profile}, initialising with default config");
            }
            catch (ParserException)
            {
                Logger.Info(
                    "Error with input config - creating a new default ");
            }

            if (_configuration == null)
            {
                _configuration = new Configuration();
                var inputProfile = new Dictionary<InputBinding, InputDevice>();
                InputProfiles[GetProfileName(profile)] = inputProfile;
                InputConfigs[GetProfileCfgFileName(profile)] = new Configuration();
                _configuration.SaveToFile(Path + GetProfileCfgFileName(profile), Encoding.UTF8);
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

    public string CurrentProfileName
    {
        get => _currentProfileName;
        set
        {
            _settingsCache.Clear();
            _currentProfileName = value;

            //TODO check if this is needed
            //EventBus.Instance.PublishOnUIThreadAsync(new ProfileChangedMessage());
        }
    }

    public string Path { get; }


    public List<string> ProfileNames => new(InputProfiles.Keys);
    public Dictionary<string, Dictionary<InputBinding, InputDevice>> InputProfiles { get; set; } = new();

    public Dictionary<InputBinding, InputDevice> GetCurrentInputProfile()
    {
        return InputProfiles[GetProfileName(CurrentProfileName)];
    }

    public Configuration GetCurrentProfile()
    {
        return InputConfigs[GetProfileCfgFileName(CurrentProfileName)];
    }

    public List<string> GetProfiles()
    {
        var profiles = _globalSettings.GetClientSetting(GlobalSettingsKeys.SettingsProfiles).StringValueArray;

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
        if (prof.Contains(".cfg")) return prof;

        return prof + ".cfg";
    }

    private string GetProfileName(string cfg)
    {
        if (cfg.Contains(".cfg")) return cfg.Replace(".cfg", "");

        return cfg;
    }

    public InputDevice GetControlSetting(InputBinding key, Configuration configuration)
    {
        if (!configuration.Contains(key.ToString())) return null;

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

        section.Add(new SharpConfig.Setting("name", device.DeviceName.Replace("\0", "")));
        section.Add(new SharpConfig.Setting("button", device.Button));
        section.Add(new SharpConfig.Setting("value", device.ButtonValue));
        section.Add(new SharpConfig.Setting("guid", device.InstanceGuid.ToString()));

        var inputDevices = GetCurrentInputProfile();

        inputDevices[device.InputBind] = device;

        Save();
    }

    public void RemoveControlSetting(InputBinding binding)
    {
        var configuration = GetCurrentProfile();

        if (configuration.Contains(binding.ToString())) configuration.Remove(binding.ToString());

        var inputDevices = GetCurrentInputProfile();
        inputDevices.Remove(binding);

        Save();
    }

    private SharpConfig.Setting GetSetting(string section, string setting)
    {
        var _configuration = GetCurrentProfile();

        if (!_configuration.Contains(section)) _configuration.Add(section);

        if (!_configuration[section].Contains(setting))
        {
            if (DefaultSettingsProfileSettings.ContainsKey(setting))
            {
                //save
                _configuration[section]
                    .Add(new SharpConfig.Setting(setting, DefaultSettingsProfileSettings[setting]));

                Save();
            }
            else if (DefaultSettingsProfileSettings.ContainsKey(setting))
            {
                //save
                _configuration[section]
                    .Add(new SharpConfig.Setting(setting, DefaultSettingsProfileSettings[setting]));

                Save();
            }
            else
            {
                _configuration[section]
                    .Add(new SharpConfig.Setting(setting, ""));
                Save();
            }
        }

        return _configuration[section][setting];
    }

    public bool GetClientSettingBool(ProfileSettingsKeys key)
    {
        if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (bool)val;

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
        if (_settingsCache.TryGetValue(key.ToString(), out var val))
        {
            if (val == null) return 0f;
            return (float)val;
        }

        var setting = GetSetting("Client Settings", key.ToString()).FloatValue;

        _settingsCache[key.ToString()] = setting;

        return setting;
    }

    public string GetClientSettingString(ProfileSettingsKeys key)
    {
        if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (string)val;

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

        if (setting == null) setting = "";
        if (!_configuration.Contains(section)) _configuration.Add(section);

        if (!_configuration[section].Contains(key))
        {
            _configuration[section].Add(new SharpConfig.Setting(key, setting));
        }
        else
        {
            if (setting is bool)
                _configuration[section][key].BoolValue = (bool)setting;
            else if (setting is float)
                _configuration[section][key].FloatValue = (float)setting;
            else if (setting is double)
                _configuration[section][key].DoubleValue = (double)setting;
            else if (setting is int)
                _configuration[section][key].DoubleValue = (int)setting;
            else if (setting.GetType() == typeof(string))
                _configuration[section][key].StringValue = setting as string;
            else if (setting is string[])
                _configuration[section][key].StringValueArray = setting as string[];
            else
                Logger.Error("Unknown Setting Type - Not Saved ");
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
                configuration.SaveToFile(Path + GetProfileCfgFileName(CurrentProfileName));
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
            File.Delete(Path + GetProfileCfgFileName(profile));
        }
        catch
        {
        }

        CurrentProfileName = "default";
    }

    public void RenameProfile(string oldName, string newName)
    {
        InputConfigs[GetProfileCfgFileName(newName)] = InputConfigs[GetProfileCfgFileName(oldName)];
        InputProfiles[GetProfileName(newName)] = InputProfiles[GetProfileName(oldName)];

        InputConfigs.Remove(GetProfileCfgFileName(oldName));
        InputProfiles.Remove(GetProfileName(oldName));

        var profiles = InputProfiles.Keys.ToList();
        _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

        CurrentProfileName = "default";

        InputConfigs[GetProfileCfgFileName(newName)].SaveToFile(GetProfileCfgFileName(newName));

        try
        {
            File.Delete(Path + GetProfileCfgFileName(oldName));
        }
        catch
        {
        }
    }

    public void CopyProfile(string profileToCopy, string profileName)
    {
        var config = Configuration.LoadFromFile(Path + GetProfileCfgFileName(profileToCopy));
        InputConfigs[GetProfileCfgFileName(profileName)] = config;

        var inputProfile = new Dictionary<InputBinding, InputDevice>();
        InputProfiles[GetProfileName(profileName)] = inputProfile;

        foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
        {
            var device = GetControlSetting(bind, config);

            if (device != null) inputProfile[bind] = device;
        }

        var profiles = InputProfiles.Keys.ToList();
        _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

        CurrentProfileName = "default";

        InputConfigs[GetProfileCfgFileName(profileName)].SaveToFile(Path + GetProfileCfgFileName(profileName));
    }
}