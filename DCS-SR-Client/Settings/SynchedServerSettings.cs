using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
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

        private List<double> GlobalFrequencies { get; set; } = new List<double>();

        // Node Limit of 0 means no retransmission
        private int RetransmitNodeLimit { get; set; } = 0;

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

        private string GetSetting(ServerSettingsKeys key)
        {
            string setting = key.ToString();

            return _settings.GetOrAdd(setting, defaults.ContainsKey(setting) ? defaults[setting] : "");
        }

        private bool GetSettingAsBool(ServerSettingsKeys key)
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

        #region Bulk Properties
        /// <summary>
        /// These only have Getters as all of this is read only/ what the server told us we can do.
        /// The Decode() method is the setter for all of these.
        /// </summary>

        public bool ClientExportEnabled => GetSettingAsBool(ServerSettingsKeys.CLIENT_EXPORT_ENABLED);
        public bool CoalitionAudioSecurityEnabled => GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
        public bool DistanceCheckingEnabled => GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED);
        public bool ExternalAwacsModeAllowed => GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE);
        public string ExternalAwacsModeBluePassword => GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD);
        public string ExternalAwacsModeRedPassword => GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD);
        public bool IrlRadioRxInterferenceEnabled => GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
        public bool IrlRadioStaticEnabled => GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_STATIC);
        public bool IrlRadioTxEnabled => GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX);
        public bool LosCheckingEnabled => GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);
        public bool RadioExpansionAllowed => GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION);
        public int ServerPort => int.Parse(GetSetting(ServerSettingsKeys.SERVER_PORT), NumberStyles.Integer);
        public bool SpectatorsAudioDisabled => GetSettingAsBool(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED);
        public string ClientExportFilePath => GetSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH);
        public bool CheckForBetaUpdates => GetSettingAsBool(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES);
        public bool RadioEncryptionAllowed => GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION);
        public List<double> TestFrequncyList => GetSetting(ServerSettingsKeys.TEST_FREQUENCIES)
                    .Split(',', StringSplitOptions.TrimEntries).ToList().ConvertAll(double.Parse);
        public bool ShowTurnedListenersCountEnabled => GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT);
        public List<double> GlobalLobbyFreqs => GetSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES)
            .Split(',', StringSplitOptions.TrimEntries).ToList().ConvertAll(double.Parse);
        public bool LotAtcExportEnabled => GetSettingAsBool(ServerSettingsKeys.LOTATC_EXPORT_ENABLED);
        public int LotAtcExportPort => int.Parse(GetSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT), NumberStyles.Integer);
        public string LotAtcExportIp => GetSetting(ServerSettingsKeys.LOTATC_EXPORT_IP);
        public bool UpnpEnabled => GetSettingAsBool(ServerSettingsKeys.UPNP_ENABLED);
        public bool ShowTransmitterNameEnabled => GetSettingAsBool(ServerSettingsKeys.SHOW_TRANSMITTER_NAME);
        public int RetransmissionNodeLimit => int.Parse(GetSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT), NumberStyles.Integer);
        public bool StrictRadioEncryptionEnabled => GetSettingAsBool(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION);
        public bool TransmissionLogEnabled => GetSettingAsBool(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED);
        public int TransmissionLogRetention => int.Parse(GetSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION), NumberStyles.Integer);
        public bool RadioEffectOverrideEnabled => GetSettingAsBool(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE);
        public string ServerIp => GetSetting(ServerSettingsKeys.SERVER_IP);
        #endregion
    }
}
