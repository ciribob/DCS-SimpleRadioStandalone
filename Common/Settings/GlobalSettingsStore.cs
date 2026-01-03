using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;

public enum GlobalSettingsKeys
{
    Version,
    MinimiseToTray,
    StartMinimised,

    RefocusDCS,
    ExpandControls,
    AutoConnectPrompt, //message about auto connect
    RadioOverlayTaskbarHide,

    AudioInputDeviceId,
    AudioOutputDeviceId,
    LastServer,
    MicBoost,
    SpeakerBoost,
    RadioX,
    RadioY,
    RadioSize,
    RadioOpacity,
    RadioWidth,
    RadioHeight,
    ClientX,
    ClientY,
    AwacsX,
    AwacsY,
    MicAudioOutputDeviceId,

    CliendIdShort, // not used anymore
    ClientIdLong,
    DCSLOSOutgoingUDP, //9086
    DCSIncomingUDP, //9084
    CommandListenerUDP, //=9040,
    OutgoingDCSUDPInfo, //7080
    OutgoingDCSUDPOther, //7082
    DCSIncomingGameGUIUDP, // 5068
    DCSLOSIncomingUDP, //9085

    // AGC,
    // AGCTarget,
    // AGCDecrement,
    // AGCLevelMax,

    Denoise,
    DenoiseAttenuation,
    IncomingAudioAGC,
    IncomingAudioAGCTarget,
    IncomingAudioAGCDecrement,
    IncomingAudioAGCLevelMax,
    
    IncomingAudioDenoise,
    IncomingAudioDenoiseAttenuation,

    LastSeenName,

    CheckForBetaUpdates,

    AllowMultipleInstances, // Allow for more than one SRS instance to be ran simultaneously. Config-file only!

    AutoConnectMismatchPrompt, //message about auto connect mismatch

    DisableWindowVisibilityCheck,
    PlayConnectionSounds,

    RequireAdmin,

    //LotATC
    LotATCIncomingUDP, //10710
    LotATCOutgoingUDP, //10711

    SettingsProfiles,
    AutoSelectSettingsProfile,

    VAICOMIncomingUDP, //33501 
    VAICOMTXInhibitEnabled,

    LotATCHeightOffset,

    DCSAutoConnectUDP, // 5069
    ShowTransmitterName,

    IdleTimeOut,
    AutoConnect,

    AllowRecording,
    RecordAudio,
    SingleFileMixdown,
    RecordingQuality,
    RecordingFormat,
    DisallowedAudioTone,
    VOX,
    VOXMode,
    VOXMinimumTime,
    VOXMinimumDB,

    AllowXInputController,

    LastPresetsFolder,
    DISEntityID
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

    TransponderIDENT = 133,
    ModifierTransponderIDENT = 233,

    RadioVolumeUp = 134,
    ModifierRadioVolumeUp = 234,

    RadioVolumeDown = 135,
    ModifierRadioVolumeDown = 235,

    IntercomPTT = 136,
    ModifierIntercomPTT = 236,

    AwacsOverlayToggle = 137,
    ModifierAwacsOverlayToggle = 237
}

public class GlobalSettingsStore
{
    private static readonly string CFG_FILE_NAME = "global.cfg";

    //private static readonly string PREVIOUS_CFG_FILE_NAME = "client.cfg";

    private static readonly object _lock = new();

    private static GlobalSettingsStore _instance;
    private readonly Configuration _configuration;

    //cache all the settings in their correct types for speed
    //fixes issue where we access settings a lot and have issues
    private readonly ConcurrentDictionary<string, object> _settingsCache = new();

    private readonly Dictionary<string, string[]> defaultArraySettings = new()
    {
        { GlobalSettingsKeys.SettingsProfiles.ToString(), new[] { "default.cfg" } }
    };

    private static readonly int CurrentVersion = 1;

    private readonly Dictionary<string, string> defaultGlobalSettings = new()
    {
        { GlobalSettingsKeys.Version.ToString(), "0" },
        { GlobalSettingsKeys.AutoConnect.ToString(), "true" },
        { GlobalSettingsKeys.AutoConnectPrompt.ToString(), "false" },
        { GlobalSettingsKeys.AutoConnectMismatchPrompt.ToString(), "true" },
        { GlobalSettingsKeys.RadioOverlayTaskbarHide.ToString(), "false" },
        { GlobalSettingsKeys.RefocusDCS.ToString(), "false" },
        { GlobalSettingsKeys.ExpandControls.ToString(), "false" },

        { GlobalSettingsKeys.MinimiseToTray.ToString(), "false" },
        { GlobalSettingsKeys.StartMinimised.ToString(), "false" },


        { GlobalSettingsKeys.AudioInputDeviceId.ToString(), "" },
        { GlobalSettingsKeys.AudioOutputDeviceId.ToString(), "" },
        { GlobalSettingsKeys.MicAudioOutputDeviceId.ToString(), "" },

        { GlobalSettingsKeys.LastServer.ToString(), "127.0.0.1" },

        { GlobalSettingsKeys.MicBoost.ToString(), "0.514" },
        { GlobalSettingsKeys.SpeakerBoost.ToString(), "0.514" },

        { GlobalSettingsKeys.RadioX.ToString(), "300" },
        { GlobalSettingsKeys.RadioY.ToString(), "300" },
        { GlobalSettingsKeys.RadioSize.ToString(), "1.0" },
        { GlobalSettingsKeys.RadioOpacity.ToString(), "1.0" },

        { GlobalSettingsKeys.RadioWidth.ToString(), "122" },
        { GlobalSettingsKeys.RadioHeight.ToString(), "270" },

        { GlobalSettingsKeys.ClientX.ToString(), "200" },
        { GlobalSettingsKeys.ClientY.ToString(), "200" },

        { GlobalSettingsKeys.AwacsX.ToString(), "300" },
        { GlobalSettingsKeys.AwacsY.ToString(), "300" },

        //    {GlobalSettingsKeys.CliendIdShort.ToString(), ShortGuid.NewGuid().ToString()},
        { GlobalSettingsKeys.ClientIdLong.ToString(), Guid.NewGuid().ToString() },

        { GlobalSettingsKeys.DCSLOSOutgoingUDP.ToString(), "9086" },
        { GlobalSettingsKeys.DCSIncomingUDP.ToString(), "9084" },
        { GlobalSettingsKeys.CommandListenerUDP.ToString(), "9040" },
        { GlobalSettingsKeys.OutgoingDCSUDPInfo.ToString(), "7080" },
        { GlobalSettingsKeys.OutgoingDCSUDPOther.ToString(), "7082" },
        { GlobalSettingsKeys.DCSIncomingGameGUIUDP.ToString(), "5068" },
        { GlobalSettingsKeys.DCSLOSIncomingUDP.ToString(), "9085" },
        { GlobalSettingsKeys.DCSAutoConnectUDP.ToString(), "5069" },

        { GlobalSettingsKeys.Denoise.ToString(), "true" },
        { GlobalSettingsKeys.DenoiseAttenuation.ToString(), "-30" },

        { GlobalSettingsKeys.IncomingAudioAGC.ToString(), "true" },
        { GlobalSettingsKeys.IncomingAudioAGCTarget.ToString(), "14000" },
        { GlobalSettingsKeys.IncomingAudioAGCDecrement.ToString(), "-60" },
        { GlobalSettingsKeys.IncomingAudioAGCLevelMax.ToString(), "40" },
                             
        { GlobalSettingsKeys.IncomingAudioDenoise.ToString(), "true" },
        { GlobalSettingsKeys.IncomingAudioDenoiseAttenuation.ToString(), "-30" },

        { GlobalSettingsKeys.LastSeenName.ToString(), "" },

        { GlobalSettingsKeys.CheckForBetaUpdates.ToString(), "false" },

        { GlobalSettingsKeys.AllowMultipleInstances.ToString(), "false" },

        { GlobalSettingsKeys.DisableWindowVisibilityCheck.ToString(), "false" },
        { GlobalSettingsKeys.PlayConnectionSounds.ToString(), "true" },

        { GlobalSettingsKeys.RequireAdmin.ToString(), "false" },

        { GlobalSettingsKeys.AutoSelectSettingsProfile.ToString(), "false" },

        { GlobalSettingsKeys.LotATCIncomingUDP.ToString(), "10710" },
        { GlobalSettingsKeys.LotATCOutgoingUDP.ToString(), "10711" },
        { GlobalSettingsKeys.LotATCHeightOffset.ToString(), "50" },


        { GlobalSettingsKeys.VAICOMIncomingUDP.ToString(), "33501" },
        { GlobalSettingsKeys.VAICOMTXInhibitEnabled.ToString(), "false" },
        { GlobalSettingsKeys.ShowTransmitterName.ToString(), "true" },

        { GlobalSettingsKeys.IdleTimeOut.ToString(), "600" }, // 10 mins

        { GlobalSettingsKeys.AllowRecording.ToString(), "false" },
        { GlobalSettingsKeys.RecordAudio.ToString(), "false" },
        { GlobalSettingsKeys.SingleFileMixdown.ToString(), "false" },
        { GlobalSettingsKeys.RecordingQuality.ToString(), "V3" },
        { GlobalSettingsKeys.RecordingFormat.ToString(), "mp3" },
        { GlobalSettingsKeys.DisallowedAudioTone.ToString(), "false" },

        { GlobalSettingsKeys.VOX.ToString(), "false" },
        { GlobalSettingsKeys.VOXMode.ToString(), "3" },
        { GlobalSettingsKeys.VOXMinimumTime.ToString(), "300" },
        { GlobalSettingsKeys.VOXMinimumDB.ToString(), "-59.0" },


        { GlobalSettingsKeys.AllowXInputController.ToString(), "false" },
        { GlobalSettingsKeys.LastPresetsFolder.ToString(), string.Empty },
        
        { GlobalSettingsKeys.DISEntityID.ToString(), "-1" }
    };

    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private GlobalSettingsStore()
    {
        //check commandline
        var args = Environment.GetCommandLineArgs();

        foreach (var arg in args)
            if (arg.Trim().StartsWith("-cfg="))
            {
                Path = arg.Trim().Replace("-cfg=", "").Trim();
                if (!Path.EndsWith("\\")) Path = Path + "\\";
                Logger.Info($"Found -cfg loading: {Path + ConfigFileName}");
            }

        try
        {
            var count = 0;
            while (IsFileLocked(new FileInfo(Path + ConfigFileName)) && count < 10)
            {
                Thread.Sleep(200);
                count++;
            }

            _configuration = Configuration.LoadFromFile(Path + ConfigFileName);
            UpgradeSettings();
        }
        catch (FileNotFoundException)
        {
            Logger.Info(
                $"Did not find client config file at path ${Path}/${ConfigFileName}, initialising with default config");

            _configuration = new Configuration
            {
                new Section("Position Settings"),
                new Section("Client Settings"),
                new Section("Network Settings")
            };
            SetClientSetting(GlobalSettingsKeys.Version, CurrentVersion);
            Save();
        }
        catch (ParserException ex)
        {
            Logger.Error(ex,
                "Failed to parse client config, potentially corrupted. Creating backing and re-initialising with default config");

            //TODO show error?
            // MessageBox.Show("Failed to read client config, it might have become corrupted.\n" +
            //                 "SRS will create a backup of your current config file (client.cfg.bak) and initialise using default settings.",
            //     "Config error",
            //     MessageBoxButton.OK,
            //     MessageBoxImage.Error);

            try
            {
                File.Copy(Path + ConfigFileName, Path + ConfigFileName + ".bak", true);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to create backup of corrupted config file, ignoring");
            }

            _configuration = new Configuration
            {
                new Section("Position Settings"),
                new Section("Client Settings"),
                new Section("Network Settings")
            };
            SetClientSetting(GlobalSettingsKeys.Version, CurrentVersion);

            Save();
        }

        ProfileSettingsStore = new ProfileSettingsStore(this);
    }

    public string ConfigFileName { get; } = CFG_FILE_NAME;
    public ProfileSettingsStore ProfileSettingsStore { get; }

    public static string Path { get; set; } = "";

    public static GlobalSettingsStore Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GlobalSettingsStore();

            //stops cyclic init
            return _instance;
        }
    }

    public static bool IsFileLocked(FileInfo file)
    {
        if (!file.Exists) return false;

        try
        {
            using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
            {
                stream.Close();
            }
        }
        catch (IOException)
        {
            //the file is unavailable because it is:
            //still being written to
            //or being processed by another thread
            //or does not exist (has already been processed)
            return true;
        }

        //file is not locked
        return false;
    }

    public void SetClientSetting(GlobalSettingsKeys key, string[] strArray)
    {
        SetSetting("Client Settings", key.ToString(), strArray);
    }

    public SharpConfig.Setting GetPositionSetting(GlobalSettingsKeys key)
    {
        return GetSetting("Position Settings", key.ToString());
    }

    public void SetPositionSetting(GlobalSettingsKeys key, double value)
    {
        _settingsCache.TryRemove(key.ToString(), out _);
        SetSetting("Position Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
    }

    public int GetClientSettingInt(GlobalSettingsKeys key)
    {
        if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (int)val;

        var setting = GetSetting("Client Settings", key.ToString());
        if (setting.RawValue.Length == 0) return 0;
        _settingsCache[key.ToString()] = setting.IntValue;
        return setting.IntValue;
    }

    public double GetClientSettingDouble(GlobalSettingsKeys key)
    {
        if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (double)val;

        var setting = GetSetting("Client Settings", key.ToString());
        if (setting.RawValue.Length == 0) return 0D;
        _settingsCache[key.ToString()] = setting.DoubleValue;
        return setting.DoubleValue;
    }

    public bool GetClientSettingBool(GlobalSettingsKeys key)
    {
        if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (bool)val;

        var setting = GetSetting("Client Settings", key.ToString());
        if (setting.RawValue.Length == 0) return false;
        _settingsCache[key.ToString()] = setting.BoolValue;
        return setting.BoolValue;
    }

    public SharpConfig.Setting GetClientSetting(GlobalSettingsKeys key)
    {
        return GetSetting("Client Settings", key.ToString());
    }

    public void SetClientSetting(GlobalSettingsKeys key, string value, bool raw = false)
    {
        _settingsCache.TryRemove(key.ToString(), out _);
        SetSetting("Client Settings", key.ToString(), value, raw);
    }

    public void SetClientSetting(GlobalSettingsKeys key, bool value)
    {
        _settingsCache.TryRemove(key.ToString(), out _);
        SetSetting("Client Settings", key.ToString(), value);
    }

    public void SetClientSetting(GlobalSettingsKeys key, int value)
    {
        _settingsCache.TryRemove(key.ToString(), out _);
        SetSetting("Client Settings", key.ToString(), value);
    }

    public void SetClientSetting(GlobalSettingsKeys key, double value)
    {
        _settingsCache.TryRemove(key.ToString(), out _);
        SetSetting("Client Settings", key.ToString(), value);
    }


    public int GetNetworkSetting(GlobalSettingsKeys key)
    {
        var networkSetting = GetSetting("Network Settings", key.ToString());

        if (networkSetting == null || networkSetting.RawValue.Length == 0)
        {
            var defaultSetting = defaultGlobalSettings[key.ToString()];
            networkSetting.IntValue = int.Parse(defaultSetting, CultureInfo.InvariantCulture);
        }

        return networkSetting.IntValue;
    }

    public void SetNetworkSetting(GlobalSettingsKeys key, int value)
    {
        SetSetting("Network Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
    }

    private SharpConfig.Setting GetSetting(string section, string setting)
    {
        if (!_configuration.Contains(section)) _configuration.Add(section);

        if (!_configuration[section].Contains(setting))
        {
            if (defaultGlobalSettings.ContainsKey(setting))
            {
                //save
                _configuration[section]
                    .Add(new SharpConfig.Setting(setting, defaultGlobalSettings[setting]));

                Save();
            }
            else if (defaultArraySettings.ContainsKey(setting))
            {
                //save
                _configuration[section]
                    .Add(new SharpConfig.Setting(setting, defaultArraySettings[setting]));

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

    private void SetSetting(string section, string key, object setting, bool raw = false)
    {
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
            else if (setting.GetType() == typeof(string))
            {
                if (raw)
                    _configuration[section][key].RawValue = setting as string;
                else
                    _configuration[section][key].StringValue = setting as string;
            }
            else if (setting is string[])
                _configuration[section][key].StringValueArray = setting as string[];
            else if (setting is int)
                _configuration[section][key].IntValue = (int)setting;
            else if (setting is double)
                _configuration[section][key].DoubleValue = (double)setting;
            else
                Logger.Error("Unknown Setting Type - Not Saved ");
        }

        Save();
    }


    private void Save()
    {
        lock (_lock)
        {
            try
            {
                _configuration.SaveToFile(Path + ConfigFileName);
            }
            catch (Exception)
            {
                Logger.Error("Unable to save settings!");
            }
        }
    }

    private void UpgradeSettings()
    {
        var loadedVersion = GetClientSettingInt(GlobalSettingsKeys.Version);
        if (loadedVersion == 0)
        {
            // Update to V1:
            // * Force enable AGC again.
            // * Fix up the values.
            // SetClientSetting(GlobalSettingsKeys.AGC, true);
            // SetClientSetting(GlobalSettingsKeys.AGCLevelMax, 45);
            // SetClientSetting(GlobalSettingsKeys.AGCTarget, 14000);

            // Upgrade done
            SetClientSetting(GlobalSettingsKeys.Version, 1);

            Save();
            loadedVersion = 1;
        }
    }
}
