using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;

public class ServerSettingsStore
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

    public SynchronizedSettings SynchronizedSettings;
    public ExternalModeSettings ExternalModeSettings;
    public ServerSettings ServerSettings;
    
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

        SynchronizedSettings = new SynchronizedSettings(this);
        ExternalModeSettings = new ExternalModeSettings(this);
        ServerSettings = new ServerSettings(this);
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

    public void SetGeneralSetting(ServerSettingsKeys key, int value)
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
    
    public void SetServerSetting(ServerSettingsKeys key, int value)
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
}