using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Old
{
    public class SyncedServerSettings
    {
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static SyncedServerSettings instance;
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, string> defaults = DefaultServerSettings.Defaults;

        private readonly ConcurrentDictionary<string, string> _settings;

        //cache of processed settings as bools to make lookup slightly quicker
        private readonly ConcurrentDictionary<string, bool> _settingsBool;

        public List<double> GlobalFrequencies { get; set; } = new List<double>();

        // Node Limit of 0 means no retransmission
        public int RetransmitNodeLimit { get; set; } = 0;

        public SyncedServerSettings()
        {
            _settings = new ConcurrentDictionary<string, string>();
            _settingsBool = new ConcurrentDictionary<string, bool>();
        }

        public static SyncedServerSettings Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new SyncedServerSettings();
                    }
                }
                return instance;
            }
        }

        public string GetSetting(ServerSettingsKeys key)
        {
            string setting = key.ToString();

            return _settings.GetOrAdd(setting, defaults.ContainsKey(setting) ? defaults[setting] : "");
        }

        public bool GetSettingAsBool(ServerSettingsKeys key)
        {
            var strKey = key.ToString();
            if (_settingsBool.TryGetValue(strKey, out bool res))
            {
                return res;
            }
            else
            {
                res = Convert.ToBoolean(GetSetting(key));
                _settingsBool[strKey] = res;
                return res;
            }
        }

        public void Decode(Dictionary<string, string> encoded)
        {
            foreach (KeyValuePair<string, string> kvp in encoded)
            {
                _settings.AddOrUpdate(kvp.Key, kvp.Value, (key, oldVal) => kvp.Value);
                
                if (kvp.Key.Equals(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString()))
                {
                    var freqStringList = kvp.Value.Split(',');

                    var newList = new List<double>();
                    foreach (var freq in freqStringList)
                    {
                        if (double.TryParse(freq.Trim(), out var freqDouble))
                        {
                            freqDouble *= 1e+6; //convert to Hz from MHz
                            newList.Add(freqDouble);
                            Logger.Debug("Adding Server Global Frequency: " + freqDouble);
                        }
                    }

                    GlobalFrequencies = newList;
                }
                else if(kvp.Key.Equals(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT.ToString()))
                {
                    if (!int.TryParse(kvp.Value, out var nodeLimit))
                    {
                        nodeLimit = 0;
                    }
                    else
                    {
                        RetransmitNodeLimit = nodeLimit;
                    }
                }
            }
            //cache will be refilled 
            _settingsBool.Clear();
        }
    }
}
