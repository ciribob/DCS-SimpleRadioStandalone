using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
    internal class SynchedRadioSettings
    {
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static SynchedRadioSettings instance;
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, RadioValues> radioDefaults = DefaultRadioInformation.RadioDefaults;
        private static readonly Dictionary<string, string[]> aircraftDefaults = DefaultRadioInformation.AircraftDefaults;

        private readonly ConcurrentDictionary<string, RadioValues> radioSettings;
        private readonly ConcurrentDictionary<string, string[]> aircraftSettings;

        public SynchedRadioSettings()
        {
            radioSettings = new ConcurrentDictionary<string, RadioValues>();
            aircraftSettings = new ConcurrentDictionary<string, string[]>();
        }

        public static SynchedRadioSettings Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new SynchedRadioSettings();
                    }
                }
                return instance;
            }
        }

        public RadioValues GetRadioSetting(string radio)
        {
            return radioSettings.GetOrAdd(radio, radioDefaults.ContainsKey(radio) ? radioDefaults[radio] : radioDefaults[Radios.Unknown.ToString()]);
        }

        public string[] GetAircraftSetting(string aircraft)
        {
            return aircraftSettings.GetOrAdd(aircraft, aircraftDefaults.ContainsKey(aircraft) ? aircraftDefaults[aircraft] : aircraftDefaults["JF-17"]); //TODO: Need some sort of sane default value here
        }

        public void DecodeRadios(Dictionary<string, RadioValues> encoded)
        {
            foreach (KeyValuePair<string, RadioValues> kvp in encoded)
            {
                radioSettings.AddOrUpdate(kvp.Key, kvp.Value, (key, oldVal) => kvp.Value);
            }
        }

        public void DecodeAircraft(Dictionary<string, string[]> encoded)
        {
            foreach (KeyValuePair<string, string[]> kvp in encoded)
            {
                aircraftSettings.AddOrUpdate(kvp.Key, kvp.Value, (key, oldVal) => kvp.Value);
            }
        }
    }
}
