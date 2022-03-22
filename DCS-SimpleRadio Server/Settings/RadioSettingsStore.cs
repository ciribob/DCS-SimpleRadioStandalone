using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Settings
{
    internal class RadioSettingsStore
    {

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static readonly string DEFAULT_RADIO_CONFIG_FILE = "radioconfiguration.json";
        public static readonly string DEFAULT_AIRCRAFT_CONFIG_FILE = "aircraftconfiguration.json";
        private string _configRadioFile = DEFAULT_RADIO_CONFIG_FILE;
        private string _configAircraftFile = DEFAULT_AIRCRAFT_CONFIG_FILE;

        private static Dictionary<string, RadioValues> radioValues;
        private static Dictionary<string, string[]> aircraftValues;

        private static RadioSettingsStore instance;
        private static readonly object _lock = new object();

        public RadioSettingsStore()
        {
            var args = Environment.GetCommandLineArgs();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-rad="))
                {
                    _configRadioFile = arg.Replace("-rad=", "").Trim();
                }
            }

            radioValues = DefaultRadioInformation.RadioDefaults;
            aircraftValues = DefaultRadioInformation.AircraftDefaults;

            try
            {
                var deserializedRadios = JsonConvert.DeserializeObject<Dictionary<string, RadioValues>>((File.ReadAllText(_configRadioFile)));

                foreach (KeyValuePair<string, RadioValues> kvp in deserializedRadios)
                {
                    if(radioValues.ContainsKey(kvp.Key))
                    {
                        radioValues[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        radioValues.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            catch(FileNotFoundException ex)
            {
                _logger.Info("Unable to find radio configuration file, using default values");
                radioValues = DefaultRadioInformation.RadioDefaults;
                aircraftValues = DefaultRadioInformation.AircraftDefaults;

                SaveRadio();
            }
            catch(JsonReaderException ex)
            {
                _logger.Error(ex, "Failed to parse radio configuration, potentially corrupted.");

                MessageBox.Show("Failed to read server config, it might have become corrupted.",
                    "Radio configuration error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                radioValues = DefaultRadioInformation.RadioDefaults;
                aircraftValues = DefaultRadioInformation.AircraftDefaults;

                SaveRadio();
            }


            try
            {
                var deserializedAircraft = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(File.ReadAllText(_configAircraftFile));

                foreach(KeyValuePair<string, string[]> kvp in deserializedAircraft)
                {
                    if(deserializedAircraft.ContainsKey(kvp.Key))
                    {
                        aircraftValues[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        aircraftValues.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                _logger.Info("Unable to find aircraft configuration file, using default values");
                radioValues = DefaultRadioInformation.RadioDefaults;
                aircraftValues = DefaultRadioInformation.AircraftDefaults;

                SaveAircraft();
            }
            catch (JsonReaderException ex)
            {
                _logger.Error(ex, "Failed to parse radio configuration, potentially corrupted.");

                MessageBox.Show("Failed to read server config, it might have become corrupted.",
                    "Radio configuration error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                radioValues = DefaultRadioInformation.RadioDefaults;
                aircraftValues = DefaultRadioInformation.AircraftDefaults;

                SaveAircraft();
            }
        }

        public static RadioSettingsStore Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new RadioSettingsStore();
                    }
                }
                return instance;
            }
        }

        public void SaveRadio()
        {
            lock (_lock)
            {
                try
                {
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    jsonSerializer.Formatting = Formatting.Indented;

                    using (StreamWriter sw = new StreamWriter(_configRadioFile))
                    using (JsonWriter jsonWriter = new JsonTextWriter(sw))
                    {
                        jsonSerializer.Serialize(jsonWriter, radioValues);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to save settings!");
                }
            }
        }

        public void SaveAircraft()
        {
            lock (_lock)
            {
                try
                {
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    jsonSerializer.Formatting = Formatting.Indented;

                    using (StreamWriter sw = new StreamWriter(_configAircraftFile))
                    using (JsonWriter jsonWriter = new JsonTextWriter(sw))
                    {
                        jsonSerializer.Serialize(jsonWriter, aircraftValues);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to save settings!");
                }
            }
        }

        public Dictionary<string, RadioValues> GetRadioSettings()
        {
            return radioValues;
        }

        public Dictionary<string, string[]> GetAircraftSettings()
        {
            return aircraftValues;
        }
    }
}
