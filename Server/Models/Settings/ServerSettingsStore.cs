using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows;
using NLog;
using SharpConfig;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Settings
{
    public class ServerSettingsStore
    {
        public static readonly string CFG_FILE_NAME = "server.cfg";
        public static readonly string CFG_BACKUP_FILE_NAME = "server.cfg.bak";

        private static ServerSettingsStore instance;
        private static readonly object _lock = new object();

        private readonly Configuration _configuration;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private string cfgFile = CFG_FILE_NAME;

        public ServerSettingsStore()
        {
            //check commandline
            var args = Environment.GetCommandLineArgs();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-cfg="))
                {
                    cfgFile = arg.Replace("-cfg=", "").Trim();
                }
            }

            try
            {
                _configuration = Configuration.LoadFromFile(cfgFile);
            }
            catch (FileNotFoundException ex)
            {
                _logger.Info("Did not find server config file, initialising with default config",ex);

                _configuration = new Configuration();
                _configuration.Add(new Section("General Settings"));
                _configuration.Add(new Section("Server Settings"));
                _configuration.Add(new Section("External AWACS Mode Settings"));

                Save();
            }
            catch (ParserException ex)
            {
                _logger.Error(ex, "Failed to parse server config, potentially corrupted. Creating backing and re-initialising with default config");

                MessageBox.Show("Failed to read server config, it might have become corrupted.\n" +
                    "SRS will create a backup of your current config file (server.cfg.bak) and initialise using default settings.",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                try
                {
                    File.Copy(cfgFile, CFG_BACKUP_FILE_NAME, true);
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
                    if (instance == null)
                    {
                        instance = new ServerSettingsStore();
                    }
                }
                return instance;
            }
        }

        public Setting GetGeneralSetting(ServerSettingsKeys key)
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

        public Setting GetServerSetting(ServerSettingsKeys key)
        {
            return GetSetting("Server Settings", key.ToString());
        }

        public void SetServerSetting(ServerSettingsKeys key, int value)
        {
            SetSetting("Server Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetServerSetting(ServerSettingsKeys key, bool value)
        {
            SetSetting("Server Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
        }

        public Setting GetExternalAWACSModeSetting(ServerSettingsKeys key)
        {
            return GetSetting("External AWACS Mode Settings", key.ToString());
        }

        public void SetExternalAWACSModeSetting(ServerSettingsKeys key, string value)
        {
            SetSetting("External AWACS Mode Settings", key.ToString(), value);
        }

        private Setting GetSetting(string section, string setting)
        {
            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(setting))
            {
                if (DefaultServerSettings.Defaults.ContainsKey(setting))
                {
                    _configuration[section].Add(new Setting(setting, DefaultServerSettings.Defaults[setting]));
                }
                else
                {
                    _configuration[section].Add(new Setting(setting, ""));
                }

                Save();
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

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    _configuration.SaveToFile(cfgFile);
                } catch (Exception ex)
                {
                    _logger.Error(ex,"Unable to save settings!");
                }
            }
        }

        public int GetServerPort()
        {
            if (!_configuration.Contains("Server Settings"))
            {
                return GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;
            }

            // Migrate from old "port" setting value to new "SERVER_PORT" one
            if (_configuration["Server Settings"].Contains("port"))
            {
                Setting oldSetting = _configuration["Server Settings"]["port"];
                if (!string.IsNullOrWhiteSpace(oldSetting.StringValue))
                {
                    _logger.Info($"Migrating old port value {oldSetting.StringValue} to current SERVER_PORT server setting");

                    _configuration["Server Settings"][ServerSettingsKeys.SERVER_PORT.ToString()].StringValue = oldSetting.StringValue;
                }

                _logger.Info("Removing old port value from server settings");

                _configuration["Server Settings"].Remove(oldSetting);

                Save();
            }

            return GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;
        }

        public Dictionary<string, string> ToDictionary()
        {
            if (!_configuration.Contains("General Settings"))
            {
                _configuration.Add("General Settings");
            }

            Dictionary<string, string> settings = new Dictionary<string, string>(_configuration["General Settings"].SettingCount);

            foreach (Setting setting in _configuration["General Settings"])
            {
                settings[setting.Name] = setting.StringValue;
            }

            return settings;
        }

        public IPAddress GetServerIP()
        {
            var str = GetServerSetting(ServerSettingsKeys.SERVER_IP).RawValue;

            if(IPAddress.TryParse(str, out IPAddress address))
            {
                return address;
            }
            else
            {
                return IPAddress.Any;
            }
        }
    }
}