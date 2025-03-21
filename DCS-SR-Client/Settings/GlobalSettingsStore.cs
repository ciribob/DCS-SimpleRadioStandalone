using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using NAudio.Lame;
using NLog;
using SharpConfig;
using WebRtcVadSharp;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
  
    public enum GlobalSettingsKeys
    {
        MinimiseToTray,
        StartMinimised,

        RefocusDCS,
        ExpandControls,
        AutoConnectPrompt, //message about auto connect
        RadioOverlayTaskbarHide,

        AudioInputDeviceId,
        AudioOutputDeviceId,
        LastServer,
        MicBoost ,
        SpeakerBoost,
        RadioX ,
        RadioY ,
        RadioSize,
        RadioOpacity,
        RadioWidth ,
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

        AGC,
        AGCTarget,
        AGCDecrement,
        AGCLevelMax,

        Denoise,
        DenoiseAttenuation,

        LastSeenName,

        CheckForBetaUpdates,

        AllowMultipleInstances, // Allow for more than one SRS instance to be ran simultaneously. Config-file only!

        AutoConnectMismatchPrompt, //message about auto connect mismatch

        DisableWindowVisibilityCheck ,
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
        DisallowedAudioTone,
        VOX,
        VOXMode,
        VOXMinimumTime,
        VOXMinimumDB,

        AllowXInputController,

        LastPresetsFolder
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

        private static readonly string PREVIOUS_CFG_FILE_NAME = "client.cfg";

        private static readonly object _lock = new object();

        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Configuration _configuration;

        public string ConfigFileName { get; } = CFG_FILE_NAME;

        private  ProfileSettingsStore _profileSettingsStore;
        public ProfileSettingsStore ProfileSettingsStore => _profileSettingsStore;

        //cache all the settings in their correct types for speed
        //fixes issue where we access settings a lot and have issues
        private ConcurrentDictionary<string, object> _settingsCache = new ConcurrentDictionary<string, object>();

        public string Path { get; } = "";

        private GlobalSettingsStore()
        {

            //Try migrating
            MigrateSettings();

            //check commandline
            var args = Environment.GetCommandLineArgs();
            
            foreach (var arg in args)
            {
                if (arg.Trim().StartsWith("-cfg="))
                {
                    Path = arg.Trim().Replace("-cfg=", "").Trim();
                    if (!Path.EndsWith("\\"))
                    {
                        Path = Path + "\\";
                    }
                    Logger.Info($"Found -cfg loading: {Path +ConfigFileName}");
                }
            }

            try
            {
                int count = 0;
                while (IsFileLocked(new FileInfo(ConfigFileName)) && count < 10)
                {
                    Thread.Sleep(200);
                    count++;
                }
                _configuration = Configuration.LoadFromFile(ConfigFileName);
            }
            catch (FileNotFoundException)
            {
                Logger.Info($"Did not find client config file at path ${Path}/${ConfigFileName}, initialising with default config");

                _configuration = new Configuration();
                _configuration.Add(new Section("Position Settings"));
                _configuration.Add(new Section("Client Settings"));
                _configuration.Add(new Section("Network Settings"));

                Save();
            }
            catch (ParserException ex)
            {
                Logger.Error(ex, "Failed to parse client config, potentially corrupted. Creating backing and re-initialising with default config");

                MessageBox.Show("Failed to read client config, it might have become corrupted.\n" +
                    "SRS will create a backup of your current config file (client.cfg.bak) and initialise using default settings.",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                try
                {
                    File.Copy(Path+ConfigFileName, Path + ConfigFileName +".bak", true);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to create backup of corrupted config file, ignoring");
                }

                _configuration = new Configuration();
                _configuration.Add(new Section("Position Settings"));
                _configuration.Add(new Section("Client Settings"));
                _configuration.Add(new Section("Network Settings"));

                Save();
            }

            _profileSettingsStore = new ProfileSettingsStore(this);
        }

        public static bool IsFileLocked(FileInfo file)
        {
            if (!file.Exists)
            {
                return false;
            }

            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
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

        private void MigrateSettings()
        {
            try 
            { 
                if (File.Exists(Path + PREVIOUS_CFG_FILE_NAME) && !File.Exists(Path + CFG_FILE_NAME))
                {
                    Logger.Info($"Migrating {Path + PREVIOUS_CFG_FILE_NAME} to {Path + CFG_FILE_NAME}");
                    File.Copy(Path + PREVIOUS_CFG_FILE_NAME, Path + CFG_FILE_NAME, true);
                    Logger.Info($"Migrated {Path + PREVIOUS_CFG_FILE_NAME} to {Path + CFG_FILE_NAME}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"Error migrating global settings");
            }
        }

        public static GlobalSettingsStore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GlobalSettingsStore();

                    //stops cyclic init
                    
                }
                return _instance;
            }
        }

        public void SetClientSetting(GlobalSettingsKeys key, string[] strArray)
        {
            SetSetting("Client Settings", key.ToString(), strArray);
        }

        private readonly Dictionary<string, string> defaultGlobalSettings = new Dictionary<string, string>()
        {
            {GlobalSettingsKeys.AutoConnect.ToString(), "true"},
            {GlobalSettingsKeys.AutoConnectPrompt.ToString(), "false"},
            {GlobalSettingsKeys.AutoConnectMismatchPrompt.ToString(), "true"},
            {GlobalSettingsKeys.RadioOverlayTaskbarHide.ToString(), "false"},
            {GlobalSettingsKeys.RefocusDCS.ToString(), "false"},
            {GlobalSettingsKeys.ExpandControls.ToString(), "false"},

            {GlobalSettingsKeys.MinimiseToTray.ToString(), "false"},
            {GlobalSettingsKeys.StartMinimised.ToString(), "false"},


            {GlobalSettingsKeys.AudioInputDeviceId.ToString(), ""},
            {GlobalSettingsKeys.AudioOutputDeviceId.ToString(), ""},
            {GlobalSettingsKeys.MicAudioOutputDeviceId.ToString(), ""},

            {GlobalSettingsKeys.LastServer.ToString(), "127.0.0.1"},

            {GlobalSettingsKeys.MicBoost.ToString(), "0.514"},
            {GlobalSettingsKeys.SpeakerBoost.ToString(), "0.514"},

            {GlobalSettingsKeys.RadioX.ToString(), "300"},
            {GlobalSettingsKeys.RadioY.ToString(), "300"},
            {GlobalSettingsKeys.RadioSize.ToString(), "1.0"},
            {GlobalSettingsKeys.RadioOpacity.ToString(), "1.0"},

            {GlobalSettingsKeys.RadioWidth.ToString(), "122"},
            {GlobalSettingsKeys.RadioHeight.ToString(), "270"},

            {GlobalSettingsKeys.ClientX.ToString(), "200"},
            {GlobalSettingsKeys.ClientY.ToString(), "200"},

            {GlobalSettingsKeys.AwacsX.ToString(), "300"},
            {GlobalSettingsKeys.AwacsY.ToString(), "300"},

        //    {GlobalSettingsKeys.CliendIdShort.ToString(), ShortGuid.NewGuid().ToString()},
            {GlobalSettingsKeys.ClientIdLong.ToString(), Guid.NewGuid().ToString()},

            {GlobalSettingsKeys.DCSLOSOutgoingUDP.ToString(), "9086"},
            {GlobalSettingsKeys.DCSIncomingUDP.ToString(), "9084"},
            {GlobalSettingsKeys.CommandListenerUDP.ToString(), "9040"},
            {GlobalSettingsKeys.OutgoingDCSUDPInfo.ToString(), "7080"},
            {GlobalSettingsKeys.OutgoingDCSUDPOther.ToString(), "7082"},
            {GlobalSettingsKeys.DCSIncomingGameGUIUDP.ToString(), "5068"},
            {GlobalSettingsKeys.DCSLOSIncomingUDP.ToString(), "9085"},
            {GlobalSettingsKeys.DCSAutoConnectUDP.ToString(), "5069"},
            

            {GlobalSettingsKeys.AGC.ToString(), "true"},
            {GlobalSettingsKeys.AGCTarget.ToString(), "30000"},
            {GlobalSettingsKeys.AGCDecrement.ToString(), "-60"},
            {GlobalSettingsKeys.AGCLevelMax.ToString(),"68" },

            {GlobalSettingsKeys.Denoise.ToString(),"true" },
            {GlobalSettingsKeys.DenoiseAttenuation.ToString(),"-30" },

            {GlobalSettingsKeys.LastSeenName.ToString(), ""},

            {GlobalSettingsKeys.CheckForBetaUpdates.ToString(), "false"},

            {GlobalSettingsKeys.AllowMultipleInstances.ToString(), "false"},

            {GlobalSettingsKeys.DisableWindowVisibilityCheck.ToString(), "false"},
            {GlobalSettingsKeys.PlayConnectionSounds.ToString(), "true"},

            {GlobalSettingsKeys.RequireAdmin.ToString(),"true" },

            {GlobalSettingsKeys.AutoSelectSettingsProfile.ToString(),"false" },

            {GlobalSettingsKeys.LotATCIncomingUDP.ToString(), "10710"},
            {GlobalSettingsKeys.LotATCOutgoingUDP.ToString(), "10711"},
            {GlobalSettingsKeys.LotATCHeightOffset.ToString(), "50"},



            {GlobalSettingsKeys.VAICOMIncomingUDP.ToString(), "33501"},
            {GlobalSettingsKeys.VAICOMTXInhibitEnabled.ToString(), "false"},
            {GlobalSettingsKeys.ShowTransmitterName.ToString(), "true"},

            {GlobalSettingsKeys.IdleTimeOut.ToString(), "600"}, // 10 mins

            {GlobalSettingsKeys.AllowRecording.ToString(), "false" },
            {GlobalSettingsKeys.RecordAudio.ToString(), "false" },
            {GlobalSettingsKeys.SingleFileMixdown.ToString(), "false" },
            {GlobalSettingsKeys.RecordingQuality.ToString(), "V3" },
            {GlobalSettingsKeys.DisallowedAudioTone.ToString(), "false"},

            {GlobalSettingsKeys.VOX.ToString(), "false" },
            {GlobalSettingsKeys.VOXMode.ToString(), "3" },
            {GlobalSettingsKeys.VOXMinimumTime.ToString(), "300" },
            {GlobalSettingsKeys.VOXMinimumDB.ToString(), "-59.0" },


            {GlobalSettingsKeys.AllowXInputController.ToString(), "false"},
            {GlobalSettingsKeys.LastPresetsFolder.ToString(), string.Empty},

        };

        private readonly Dictionary<string, string[]> defaultArraySettings = new Dictionary<string, string[]>()
        {
            {GlobalSettingsKeys.SettingsProfiles.ToString(), new string[]{"default.cfg"} }
        };

        public Setting GetPositionSetting(GlobalSettingsKeys key)
        {
            return GetSetting("Position Settings", key.ToString());
        }

        public void SetPositionSetting(GlobalSettingsKeys key, double value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Position Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
        }

        private int GetClientSettingInt(GlobalSettingsKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val))
            {
                return (int)val;
            }

            var setting = GetSetting("Client Settings", key.ToString());
            if (setting.RawValue.Length == 0)
            {
                return 0;
            }
            _settingsCache[key.ToString()] = setting.IntValue;
            return setting.IntValue;
        }

        private double GetClientSettingDouble(GlobalSettingsKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val))
            {
                return (double)val;
            }

            var setting = GetSetting("Client Settings", key.ToString());
            if (setting.RawValue.Length == 0)
            {
                return 0D;
            }
            _settingsCache[key.ToString()] = setting.DoubleValue;
            return setting.DoubleValue;
        }
        private bool GetClientSettingBool(GlobalSettingsKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val))
            {
                return (bool)val;
            }

            var setting = GetSetting("Client Settings", key.ToString());
            if (setting.RawValue.Length == 0)
            {
                return false;
            }
            _settingsCache[key.ToString()] = setting.BoolValue;
            return setting.BoolValue;
        }

        private Setting GetClientSetting(GlobalSettingsKeys key)
        {
            return GetSetting("Client Settings", key.ToString());
        }

        private void SetClientSetting(GlobalSettingsKeys key, string value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Client Settings", key.ToString(), value);
        }

        private void SetClientSetting(GlobalSettingsKeys key, bool value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Client Settings", key.ToString(), value);
        }

        private void SetClientSetting(GlobalSettingsKeys key, int value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Client Settings", key.ToString(), value);
        }

        private void SetClientSetting(GlobalSettingsKeys key, double value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Client Settings", key.ToString(), value);
        }

        public int GetNetworkSetting(GlobalSettingsKeys key)
        {
            var networkSetting = GetSetting("Network Settings", key.ToString());

            if (networkSetting == null || networkSetting.RawValue.Length == 0)
            {
                var defaultSetting  = defaultGlobalSettings[key.ToString()];
                networkSetting.IntValue = int.Parse(defaultSetting, CultureInfo.InvariantCulture);
            }

            return networkSetting.IntValue;
        }

        public void SetNetworkSetting(GlobalSettingsKeys key, int value)
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
                if (defaultGlobalSettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, defaultGlobalSettings[setting]));

                    Save();
                }
                else if(defaultArraySettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, defaultArraySettings[setting]));

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

        private void SetSetting(string section, string key, object setting)
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
                
                if (setting is bool)
                {
                    _configuration[section][key].BoolValue = (bool) setting ;
                }
                else if (setting.GetType() == typeof(string))
                {
                    _configuration[section][key].StringValue = setting as string;
                }
                else if(setting is string[])
                {
                    _configuration[section][key].StringValueArray = setting as string[];
                }
                else if (setting is int)
                {
                    _configuration[section][key].IntValue = (int)setting;
                }
                else if (setting is double)
                {
                    _configuration[section][key].DoubleValue = (double)setting;
                }
                else
                {
                    Logger.Error("Unknown Setting Type - Not Saved ");
                }
                
            }

            Save();
        }

        private static GlobalSettingsStore _instance;

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

        
        #region Bulk Properties
        public bool MinimiseToTray
        {
            get => GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray);
            set => SetClientSetting(GlobalSettingsKeys.MinimiseToTray, value.ToString());
        }
        public bool StartMinimised
        {
            get => GetClientSettingBool(GlobalSettingsKeys.StartMinimised);
            set => SetClientSetting(GlobalSettingsKeys.StartMinimised, value.ToString());
        }
        public bool RefocusDCS
        {
            get => GetClientSettingBool(GlobalSettingsKeys.RefocusDCS);
            set => SetClientSetting(GlobalSettingsKeys.RefocusDCS, value.ToString());
        }
        public bool ExpandControls
        {
            get => GetClientSettingBool(GlobalSettingsKeys.ExpandControls);
            set => SetClientSetting(GlobalSettingsKeys.ExpandControls, value.ToString());
        }
        public bool AutoConnectPrompt
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);
            set => SetClientSetting(GlobalSettingsKeys.AutoConnectPrompt, value.ToString());
        }
        public bool RadioOverlayTaskbarHide
        {
            get => GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
            set => SetClientSetting(GlobalSettingsKeys.RadioOverlayTaskbarHide, value.ToString());
        }
        public string AudioInputDeviceId
        {
            get => GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, value.ToString());
        }
        public string AudioOutputDeviceId
        {
            get => GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, value.ToString());
        }
        public string LastServer
        {
            get => GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, value.ToString());
        }
        public float MicBoost 
        {
            get => GetClientSetting(GlobalSettingsKeys.MicBoost).FloatValue;
            set => SetClientSetting(GlobalSettingsKeys.MicBoost, value.ToString(CultureInfo.InvariantCulture));
        }
        public float  SpeakerBoost
        {
            get => GetClientSetting(GlobalSettingsKeys.SpeakerBoost).FloatValue;
            set => SetClientSetting(GlobalSettingsKeys.SpeakerBoost, value.ToString(CultureInfo.InvariantCulture));
        }
        public double RadioX 
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.SpeakerBoost);
            set => SetClientSetting(GlobalSettingsKeys.SpeakerBoost, value.ToString(CultureInfo.InvariantCulture));
        }
        public double RadioY 
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.SpeakerBoost);
            set => SetClientSetting(GlobalSettingsKeys.SpeakerBoost, value.ToString(CultureInfo.InvariantCulture));
        }
        public double RadioSize
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.RadioSize);
            set => SetClientSetting(GlobalSettingsKeys.RadioSize, value.ToString(CultureInfo.InvariantCulture));
        }
        public double RadioOpacity
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.RadioOpacity);
            set => SetClientSetting(GlobalSettingsKeys.RadioOpacity, value.ToString(CultureInfo.InvariantCulture));
        }
        public double RadioWidth 
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.RadioWidth);
            set => SetClientSetting(GlobalSettingsKeys.RadioWidth, value.ToString(CultureInfo.InvariantCulture));
        }
        public double RadioHeight
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.RadioHeight);
            set => SetClientSetting(GlobalSettingsKeys.RadioHeight, value.ToString(CultureInfo.InvariantCulture));
        }
        public double ClientX
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.ClientX);
            set => SetClientSetting(GlobalSettingsKeys.ClientX, value.ToString(CultureInfo.InvariantCulture));
        }
        public double ClientY
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.ClientY);
            set => SetClientSetting(GlobalSettingsKeys.ClientY, value.ToString(CultureInfo.InvariantCulture));
        }
        public double AwacsX
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.AwacsX);
            set => SetClientSetting(GlobalSettingsKeys.AwacsX, value.ToString(CultureInfo.InvariantCulture));
        }
        public double AwacsY
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.AwacsY);
            set => SetClientSetting(GlobalSettingsKeys.AwacsY, value.ToString(CultureInfo.InvariantCulture));
        }
        public string MicAudioOutputDeviceId
        {
            get => GetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, value.ToString());
        }
        public string ClientIdLong
        {
            get => GetClientSetting(GlobalSettingsKeys.AudioInputDeviceId).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, value.ToString());
        }
        public int DCSLOSOutgoingUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.DCSLOSOutgoingUDP);
            set => SetClientSetting(GlobalSettingsKeys.DCSLOSOutgoingUDP, value.ToString());
        }
        public int DCSIncomingUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.DCSIncomingUDP);
            set => SetClientSetting(GlobalSettingsKeys.DCSIncomingUDP, value.ToString());
        }
        public int CommandListenerUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.CommandListenerUDP);
            set => SetClientSetting(GlobalSettingsKeys.CommandListenerUDP, value.ToString());
        }
        public int OutgoingDCSUDPInfo
        {
            get => GetClientSettingInt(GlobalSettingsKeys.OutgoingDCSUDPInfo);
            set => SetClientSetting(GlobalSettingsKeys.OutgoingDCSUDPInfo, value.ToString());
        }
        public int OutgoingDCSUDPOther
        {
            get => GetClientSettingInt(GlobalSettingsKeys.OutgoingDCSUDPOther);
            set => SetClientSetting(GlobalSettingsKeys.OutgoingDCSUDPOther, value.ToString());
        }
        public int DCSIncomingGameGUIUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.DCSIncomingGameGUIUDP);
            set => SetClientSetting(GlobalSettingsKeys.DCSIncomingGameGUIUDP, value.ToString());
        }
        public int DCSLOSIncomingUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.DCSLOSIncomingUDP);
            set => SetClientSetting(GlobalSettingsKeys.DCSLOSIncomingUDP, value.ToString());
        }
        public bool AGC
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AGC);
            set => SetClientSetting(GlobalSettingsKeys.AGC, value.ToString());
        }
        public int AGCTarget
        {
            get => GetClientSettingInt(GlobalSettingsKeys.AGCTarget);
            set => SetClientSetting(GlobalSettingsKeys.AGCTarget, value.ToString());
        }
        public int AGCDecrement
        {
            get => GetClientSettingInt(GlobalSettingsKeys.AGCDecrement);
            set => SetClientSetting(GlobalSettingsKeys.AGCDecrement, value.ToString());
        }
        public int AGCLevelMax
        {
            get => GetClientSettingInt(GlobalSettingsKeys.AGCLevelMax);
            set => SetClientSetting(GlobalSettingsKeys.AGCLevelMax, value.ToString());
        }
        public bool Denoise
        {
            get => GetClientSettingBool(GlobalSettingsKeys.Denoise);
            set => SetClientSetting(GlobalSettingsKeys.Denoise, value.ToString());
        }
        public int DenoiseAttenuation
        {
            get => GetClientSettingInt(GlobalSettingsKeys.DenoiseAttenuation);
            set => SetClientSetting(GlobalSettingsKeys.DenoiseAttenuation, value.ToString());
        }
        public string LastSeenName
        {
            get => GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.LastPresetsFolder, value.ToString());
        }
        public bool CheckForBetaUpdates
        {
            get => GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates);
            set => SetClientSetting(GlobalSettingsKeys.CheckForBetaUpdates, value.ToString());
        }
        public bool AllowMultipleInstances
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AllowMultipleInstances);
            set => SetClientSetting(GlobalSettingsKeys.AllowMultipleInstances, value.ToString());
        }
        public bool AutoConnectMismatchPrompt
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);
            set => SetClientSetting(GlobalSettingsKeys.AutoConnectMismatchPrompt, value.ToString());
        }
        public bool DisableWindowVisibilityCheck 
        {
            get => GetClientSettingBool(GlobalSettingsKeys.DisableWindowVisibilityCheck);
            set => SetClientSetting(GlobalSettingsKeys.DisableWindowVisibilityCheck, value.ToString());
        }
        public bool PlayConnectionSounds
        {
            get => GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds);
            set => SetClientSetting(GlobalSettingsKeys.PlayConnectionSounds, value.ToString());
        }
        public bool RequireAdmin
        {
            get => GetClientSettingBool(GlobalSettingsKeys.RequireAdmin);
            set => SetClientSetting(GlobalSettingsKeys.RequireAdmin, value.ToString());
        }
        public int LotATCIncomingUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.LotATCIncomingUDP);
            set => SetClientSetting(GlobalSettingsKeys.LotATCIncomingUDP, value.ToString());
        }
        public int LotATCOutgoingUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.LotATCOutgoingUDP);
            set => SetClientSetting(GlobalSettingsKeys.LotATCOutgoingUDP, value.ToString());
        }

        
        /// <summary>
        /// This is a dangerous property to use.
        /// Ensure it is being used properly.
        /// </summary>
        public string[] SettingsProfiles
        {
            get => GetClientSetting(GlobalSettingsKeys.SettingsProfiles).StringValueArray;
            set => SetClientSetting(GlobalSettingsKeys.SettingsProfiles, value.ToString());
        }
        
        
        public bool AutoSelectSettingsProfile
        {
            get => GetClientSettingBool(GlobalSettingsKeys.VAICOMTXInhibitEnabled);
            set => SetClientSetting(GlobalSettingsKeys.VAICOMTXInhibitEnabled, value.ToString());
        }
        public int VAICOMIncomingUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.LotATCOutgoingUDP);
            set => SetClientSetting(GlobalSettingsKeys.LotATCOutgoingUDP, value.ToString());
        }
        public bool VAICOMTXInhibitEnabled
        {
            get => GetClientSettingBool(GlobalSettingsKeys.VAICOMTXInhibitEnabled);
            set => SetClientSetting(GlobalSettingsKeys.VAICOMTXInhibitEnabled, value.ToString());
        }
        public double LotATCHeightOffset
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.LotATCHeightOffset);
            set => SetClientSetting(GlobalSettingsKeys.LotATCHeightOffset, value.ToString(CultureInfo.InvariantCulture));
        }
        public int DCSAutoConnectUDP
        {
            get => GetClientSettingInt(GlobalSettingsKeys.DCSAutoConnectUDP);
            set => SetClientSetting(GlobalSettingsKeys.DCSAutoConnectUDP, value.ToString());
        }
        public bool ShowTransmitterName
        {
            get => GetClientSettingBool(GlobalSettingsKeys.ShowTransmitterName);
            set => SetClientSetting(GlobalSettingsKeys.ShowTransmitterName, value.ToString());
        }
        public int  IdleTimeOut
        {
            get => GetClientSettingInt(GlobalSettingsKeys.IdleTimeOut);
            set => SetClientSetting(GlobalSettingsKeys.IdleTimeOut, value.ToString());
        }
        public bool AutoConnect
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AutoConnect);
            set => SetClientSetting(GlobalSettingsKeys.AutoConnect, value.ToString());
        }
        public bool AllowRecording
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AllowRecording);
            set => SetClientSetting(GlobalSettingsKeys.AllowRecording, value.ToString());
        }
        public bool RecordAudio
        {
            get => GetClientSettingBool(GlobalSettingsKeys.RecordAudio);
            set => SetClientSetting(GlobalSettingsKeys.RecordAudio, value.ToString());
        }
        public bool SingleFileMixdown
        {
            get => GetClientSettingBool(GlobalSettingsKeys.SingleFileMixdown);
            set => SetClientSetting(GlobalSettingsKeys.SingleFileMixdown, value.ToString());
        }
        public LAMEPreset RecordingQuality
        {
            get => (LAMEPreset)GetClientSettingInt(GlobalSettingsKeys.RecordingQuality);
            set => SetClientSetting(GlobalSettingsKeys.RecordingQuality, (int)value);
        }
        public bool DisallowedAudioTone
        {
            get => GetClientSettingBool(GlobalSettingsKeys.DisallowedAudioTone);
            set => SetClientSetting(GlobalSettingsKeys.DisallowedAudioTone, value.ToString());
        }
        public bool VOX
        {
            get => GetClientSettingBool(GlobalSettingsKeys.VOX);
            set => SetClientSetting(GlobalSettingsKeys.VOX, value.ToString());
        }
        public OperatingMode VOXMode
        {
            get => (OperatingMode)GetClientSettingInt(GlobalSettingsKeys.VOXMode);
            set => SetClientSetting(GlobalSettingsKeys.VOXMode, (int)value);
        }
        public int VOXMinimumTime
        {
            get => GetClientSettingInt(GlobalSettingsKeys.VOXMinimumTime);
            set => SetClientSetting(GlobalSettingsKeys.VOXMinimumTime, value.ToString());
        }
        public double VOXMinimumDB
        {
            get => GetClientSettingDouble(GlobalSettingsKeys.VOXMinimumDB);
            set => SetClientSetting(GlobalSettingsKeys.VOXMinimumDB, value.ToString(CultureInfo.InvariantCulture));
        }
        public bool AllowXInputController
        {
            get => GetClientSettingBool(GlobalSettingsKeys.AllowXInputController);
            set => SetClientSetting(GlobalSettingsKeys.AllowXInputController, value.ToString());
        }
        public string LastPresetsFolder
        {
            get => GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;
            set => SetClientSetting(GlobalSettingsKeys.LastPresetsFolder, value.ToString());
        }
        #endregion
    }
}