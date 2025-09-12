using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;


public class ServerSettingsStore : INotifyPropertyChanged
{
    public static readonly string AWACS_RADIOS_CUSTOM_FILE = "awacs-radios-custom.json";
    public static readonly string CFG_BACKUP_FILE_NAME = "server.cfg.bak";

    private static ServerSettingsStore instance;
    private static readonly object _lock = new();

    //Can be overridden by a command line flag - hence being static
    //if overwritten, it will contain a full path
    public static string CFG_FILE_NAME = "server.cfg";

    private readonly Configuration _configuration;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private ServerChannelPresetHelper _serverChannelPresetHelper;
    private List<DCSRadioCustom> _customRadios = null;

    public ServerSettingsStore()
    {
        try
        {
            _configuration = Configuration.LoadFromFile(CFG_FILE_NAME);
        }
        catch (FileNotFoundException ex)
        {
            _logger.Info("Did not find server config file, initialising with default config", ex);

            _configuration = new Configuration();
            _configuration.Add(new Section("General Settings"));
            _configuration.Add(new Section("Server Settings"));
            _configuration.Add(new Section("External AWACS Mode Settings"));

            Save();
        }
        catch (ParserException ex)
        {
            _logger.Error(ex,
                "Failed to parse server config, potentially corrupted. Creating backing and re-initialising with default config");

            try
            {
                File.Copy(CFG_FILE_NAME, CFG_BACKUP_FILE_NAME, true);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to create backup of corrupted config file, ignoring");
            }

            _configuration = new Configuration();
            _configuration.Add(new Section("General Settings"));
            _configuration.Add(new Section("Server Settings"));
            _configuration.Add(new Section("External AWACS Mode Settings"));

            Save();
        }
    }

    public static ServerSettingsStore Instance
    {
        get
        {
            lock (_lock)
            {
                if (instance == null) instance = new ServerSettingsStore();
            }

            return instance;
        }
    }

    public List<string> GetAllSettings()
    {
        var list = new List<string>();
        foreach (var section in _configuration)
        foreach (var setting in section)
            list.Add($"{setting.Name} = {setting.RawValue}");

        return list;
    }

    public SharpConfig.Setting GetGeneralSetting(ServerSettingsKeys key)
    {
        return GetSetting("General Settings", key.ToString());
    }

    public void SetGeneralSetting(ServerSettingsKeys key, bool value)
    {
        SetSetting("General Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
    }

    public void SetGeneralSetting(ServerSettingsKeys key, string value)
    {
        SetSetting("General Settings", key.ToString(), value.Trim());
    }

    public SharpConfig.Setting GetServerSetting(ServerSettingsKeys key)
    {
        return GetSetting("Server Settings", key.ToString());
    }

    public void SetServerSetting(ServerSettingsKeys key, bool value)
    {
        SetSetting("Server Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
    }

    public void SetServerSetting(ServerSettingsKeys key, string value)
    {
        SetSetting("Server Settings", key.ToString(), value.Trim());
    }

    public SharpConfig.Setting GetExternalAWACSModeSetting(ServerSettingsKeys key)
    {
        return GetSetting("External AWACS Mode Settings", key.ToString());
    }

    public void SetExternalAWACSModeSetting(ServerSettingsKeys key, string value)
    {
        SetSetting("External AWACS Mode Settings", key.ToString(), value);
        SetSetting(ServerSettingsKeysExtensions.ExternalSection, key.ToString(), value);
    }

    private SharpConfig.Setting GetSetting(string section, string setting)
    {
        if (!_configuration.Contains(section)) _configuration.Add(section);

        if (!_configuration[section].Contains(setting))
        {
            if (DefaultServerSettings.Defaults.ContainsKey(setting))
                _configuration[section].Add(new SharpConfig.Setting(setting, DefaultServerSettings.Defaults[setting]));
            else
                _configuration[section].Add(new SharpConfig.Setting(setting, ""));

            Save();
        }

        return _configuration[section][setting];
    }

    private void SetSetting(string section, string key, string setting)
    {
        if (setting == null) setting = "";

        if (!_configuration.Contains(section)) _configuration.Add(section);

        if (!_configuration[section].Contains(key))
            _configuration[section].Add(new SharpConfig.Setting(key, setting));
        else
            _configuration[section][key].StringValue = setting;

        Save();
    }

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
                _logger.Error(ex, "Unable to save settings!");
            }
        }
    }

    public int GetServerPort()
    {
        if (!_configuration.Contains("Server Settings"))
            return GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;

        // Migrate from old "port" setting value to new "SERVER_PORT" one
        if (_configuration["Server Settings"].Contains("port"))
        {
            var oldSetting = _configuration["Server Settings"]["port"];
            if (!string.IsNullOrWhiteSpace(oldSetting.StringValue))
            {
                _logger.Info(
                    $"Migrating old port value {oldSetting.StringValue} to current SERVER_PORT server setting");

                _configuration["Server Settings"][ServerSettingsKeys.SERVER_PORT.ToString()].StringValue =
                    oldSetting.StringValue;
            }

            _logger.Info("Removing old port value from server settings");

            _configuration["Server Settings"].Remove(oldSetting);

            Save();
        }

        return GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;
    }

    public Dictionary<string, string> ToDictionary()
    {
        if (!_configuration.Contains("General Settings")) _configuration.Add("General Settings");

        var settings =
            new Dictionary<string, string>(_configuration["General Settings"].SettingCount);

        foreach (var setting in _configuration["General Settings"]) settings[setting.Name] = setting.StringValue;

        if (GetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS_ENABLED).BoolValue)
        {
            //load presets
            if (_serverChannelPresetHelper == null)
            {
                _serverChannelPresetHelper = new ServerChannelPresetHelper(Path.GetDirectoryName(CFG_FILE_NAME));
                _serverChannelPresetHelper.LoadPresets();
            }

            //I apologise to the programming gods - but this keeps it backwards compatible :/
            settings[nameof(ServerSettingsKeys.SERVER_PRESETS)] =
                JsonSerializer.Serialize(_serverChannelPresetHelper.Presets, new JsonSerializerOptions()
                {
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    IncludeFields = true,
                });
        }
        else
        {
            settings[nameof(ServerSettingsKeys.SERVER_PRESETS)] =
                JsonSerializer.Serialize(new Dictionary<string, List<ServerPresetChannel>>());
        }
        
        if (GetGeneralSetting(ServerSettingsKeys.SERVER_EAM_RADIO_PRESET_ENABLED).BoolValue)
        {
            //lazy init
            if (_customRadios == null)
            {
                _customRadios = new List<DCSRadioCustom>();

                try
                {
                    var path = Path.Combine(Path.Combine(Path.GetDirectoryName(CFG_FILE_NAME), "Presets", AWACS_RADIOS_CUSTOM_FILE));

                    if (File.Exists(path))
                    {
                        var customRadioText = File.ReadAllText(path);
               
                        _customRadios  = JsonSerializer.Deserialize<List<DCSRadioCustom>>(customRadioText,
                            new JsonSerializerOptions()
                            {
                                AllowTrailingCommas = true,
                                PropertyNameCaseInsensitive = true,
                                ReadCommentHandling = JsonCommentHandling.Skip,
                                IncludeFields = true,
                            });

                        if (_customRadios.Count != Constants.MAX_RADIOS)
                        {
                            _customRadios =  new List<DCSRadioCustom>();
                            _logger.Error($"Custom Radios has {_customRadios.Count} custom radios and needs exactly {Constants.MAX_RADIOS}");
                        }
                    }
                    else
                    {
                        _logger.Error($"Custom Radios file not found at {path}");
                    }
                }
                catch (Exception ex)
                {
                    _customRadios = new List<DCSRadioCustom>();
                    _logger.Error($"Unable to parse custom radio file. Error: {ex.Message}");
                }
               
            }
            
            //I apologise to the programming gods - but this keeps it backwards compatible :/
            settings[nameof(ServerSettingsKeys.SERVER_EAM_RADIO_PRESET)] =
                JsonSerializer.Serialize(_customRadios, new JsonSerializerOptions()
                {
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    IncludeFields = true,
                });
        }
        else
        {
            settings[nameof(ServerSettingsKeys.SERVER_EAM_RADIO_PRESET)] =
                JsonSerializer.Serialize(new List<DCSRadioCustom>());
        }

        return settings;
    }

    public IPAddress GetServerIP()
    {
        var str = GetServerSetting(ServerSettingsKeys.SERVER_IP).RawValue;

        if (IPAddress.TryParse(str, out var address)) return address;

        return IPAddress.Any;
    }

    
    // Settings as Properties
    #region Properties
    
    private dynamic GetSetting(ServerSettingsKeys key)
    {
        if (key.GetSettingType() == typeof(int)) return (int)GetSetting(key.GetSettingSection(), key.ToString()).IntValue;
        if (key.GetSettingType() == typeof(bool)) return (bool)GetSetting(key.GetSettingSection(), key.ToString()).BoolValue;
        if (key.GetSettingType() == typeof(string)) return (string)GetSetting(key.GetSettingSection(), key.ToString()).StringValue;
        return null;
    }
    private void SetSetting(ServerSettingsKeys key, object value, [CallerMemberName] string? propertyName = null)
    {
        if (value != null)
        {
            if (key.GetSettingType() == typeof(bool))
            {
                SetSetting(key.GetSettingSection(), key.ToString(), value.ToString()!.ToLowerInvariant());
            }
            else
            {
                SetSetting(key.GetSettingSection(), key.ToString(), value.ToString());
            }
            
            OnPropertyChanged(propertyName);
        }
        
    }
    
        #region General Settings
        public bool IsClientExportEnabled
        {
            get => GetSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED);
            set => SetSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, value);
        }

        public bool IsCoalitionAudioSecurityEnabled
        {
            get => GetSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
            set => SetSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value);
        }
        public bool IsDistanceLimitEnabled
        {
            get => GetSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
            set => SetSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value);
        }
        public bool IsExternalModeEnabled
        {
            get => GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE);
            set => SetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, value);
        }

        public List<double> GlobalLobbyFrequencies
        {
            get
            {
                string temp = GetSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES);
                if (string.IsNullOrEmpty(temp)) return new List<double>();
                return temp.Split(',').Select(double.Parse).ToList();
            }
            set => SetSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, value);
        }

        public bool IsIrlRadioRxEffectsEnabled
        {
            get => GetSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
            set => SetSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, value);
        }
        public bool IsIrlRadioTxEffectsEnabled
        {
            get => GetSetting(ServerSettingsKeys.IRL_RADIO_TX);
            set => SetSetting(ServerSettingsKeys.IRL_RADIO_TX, value);
        }
        public bool IsLineOfSightEnabled
        {
            get => GetSetting(ServerSettingsKeys.LOS_ENABLED);
            set => SetSetting(ServerSettingsKeys.LOS_ENABLED, value);
        }
        public bool IsLotAtcExportEnabled
        {
            get => GetSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED);
            set => SetSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, value);
        }
        public bool IsRadioEffectOverrideOnGlobalEnabled
        {
            get => GetSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE);
            set => SetSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, value);
        }
        public bool IsRadioEncryptionAllowed
        {
            get => GetSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION);
            set => SetSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, value);
        }
        public bool IsRadioExpansionAllowed
        {
            get => GetSetting(ServerSettingsKeys.RADIO_EXPANSION);
            set => SetSetting(ServerSettingsKeys.RADIO_EXPANSION, value);
        }
        public int RetransmissionNodeLimit
        {
            get => GetSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT);
            set => SetSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, value);
        }
        public bool IsServerPresetsEnabled
        {
            get => GetSetting(ServerSettingsKeys.SERVER_PRESETS_ENABLED);
            set => SetSetting(ServerSettingsKeys.SERVER_PRESETS_ENABLED, value);
        }
        public bool IsShowTransmitterNameEnabled
        {
            get => GetSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME);
            set => SetSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, value);
        }
        public bool IsShowTunedCountEnabled
        {
            get => GetSetting(ServerSettingsKeys.SHOW_TUNED_COUNT);
            set => SetSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, value);
        }
        public bool IsSpectatorAudioDisabled
        {
            get => GetSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED);
            set => SetSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, value);
        }
        public bool IsStrictRadioEncryptionEnabled
        {
            get => GetSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION);
            set => SetSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, value);
        }
        public bool IsTransmissionLogEnabled
        {
            get => GetSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED);
            set => SetSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, value);
        }
        public int TransmissionLogRetentionLimit
        {
            get => GetSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION);
            set => SetSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, value);
        }

        public List<double> TestFrequencies
        {
            get
            {
                string temp = GetSetting(ServerSettingsKeys.TEST_FREQUENCIES);
                if (string.IsNullOrEmpty(temp)) return new List<double>();
                return temp.Split(',').Select(double.Parse).ToList();
            }
            set => SetSetting(ServerSettingsKeys.TEST_FREQUENCIES, value);
        }

        #endregion

        #region Server Settings
        
        public string ClientExportFilePath
        {
            get => GetSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH);
            set => SetSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, value);
        }

        public string ServerPresetsPath
        {
            get => GetSetting(ServerSettingsKeys.SERVER_PRESETS);
            set => SetSetting(ServerSettingsKeys.SERVER_PRESETS, value);
        }

        public bool IsCheckForBetaUpdatesEnabled
        {
            get => GetSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES);
            set => SetSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value);
        }

        public bool IsHttpServerEnabled
        {
            get => GetSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED);
            set => SetSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED, value);
        }

        public bool IsUpnpEnabled
        {
            get => GetSetting(ServerSettingsKeys.UPNP_ENABLED);
            set => SetSetting(ServerSettingsKeys.UPNP_ENABLED, value);
        }

        public int HttpServerPort
        {
            get => GetSetting(ServerSettingsKeys.HTTP_SERVER_PORT);
            set => SetSetting(ServerSettingsKeys.HTTP_SERVER_PORT, value);
        }

        public string LotAtcExportIp
        {
            get => GetSetting(ServerSettingsKeys.LOTATC_EXPORT_IP);
            set => SetSetting(ServerSettingsKeys.LOTATC_EXPORT_IP, value);
        }

        public int LotAtcExportPort
        {
            get => GetSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT);
            set => SetSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT, value);
        }

        public string ServerBindIp
        {
            get => GetSetting(ServerSettingsKeys.SERVER_IP);
            set => SetSetting(ServerSettingsKeys.SERVER_IP, value);
        }

        public int ServerPort
        {
            get => GetSetting(ServerSettingsKeys.SERVER_PORT);
            set => SetSetting(ServerSettingsKeys.SERVER_PORT, value);
        }

        #endregion

        #region External AWACS Mode Settings
        public string ExternalModePassBlue
        {
            get => GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD);
            set => SetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, value);
        }

        public string ExternalModePassRed
        {
            get => GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD);
            set => SetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, value);
        }

        #endregion
    #endregion
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}