using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Newtonsoft.Json.Linq;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels
{
    public class FilePresetChannelsStore : IPresetChannelsStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private string PresetsFolder { get
            {
                var folder = _globalSettings.GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    folder = Directory.GetCurrentDirectory();
                }

                return folder;
            }
        }

        public IEnumerable<PresetChannel> LoadFromStore(string radioName, bool mids = false)
        {
            var file = FindRadioFile(NormaliseString(radioName));

            if (file != null)
            {
                if (mids)
                {
                    return ReadMidsFrequenciesFromFile(file);
                }
                else
                {
                    return ReadFrequenciesFromFile(file);
                }
            }

            return new List<PresetChannel>();
        }

        public string CreatePresetFile(string radioName)
        {
            var normalisedName = NormaliseString(radioName);
            var file = FindRadioFile(normalisedName);

            if (file == null)
            {
                var path = Path.ChangeExtension(Path.Combine(PresetsFolder, normalisedName), "txt");
                try
                {
                    File.Create(path);
                    Logger.Log(LogLevel.Info, $"Created radio file {path} ");
                    return path;
                }
                catch
                {
                    Logger.Log(LogLevel.Error, $"Error creating radio file {path} ");
                }

                return null;
            }

            return file;
        }

        private List<PresetChannel> ReadFrequenciesFromFile(string filePath)
        {
            List<PresetChannel> channels = new List<PresetChannel>();
            string[] lines = File.ReadAllLines(filePath);

            const double MHz = 1000000;
            if (lines?.Length > 0)
            {
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0)
                    {
                        try
                        {
                            var split =  trimmed.Split('|');

                            var name = "";
                            double frequency = 0;
                            if (split.Length >= 2)
                            {
                                name = split[0]; 
                                frequency = Double.Parse(split[1], CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                name = trimmed;
                                frequency = Double.Parse(trimmed, CultureInfo.InvariantCulture);
                            }

                            channels.Add(new PresetChannel()
                            {
                                Text = name,
                                Value = frequency * MHz,
                            });
                        }
                        catch (Exception)
                        {
                            Logger.Log(LogLevel.Info, "Error parsing frequency  "+trimmed);
                        }
                    }
                }
            }

            int i = 1;
            foreach (var channel in channels)
            {
                channel.Text = i + ": " + channel.Text;
                i++;
            }

            return channels;
        }

        private List<PresetChannel> ReadMidsFrequenciesFromFile(string filePath)
        {

            List<PresetChannel> channels = new List<PresetChannel>();
            string[] lines = File.ReadAllLines(filePath);

            const double MHz = ( 100000.0);
            const double MidsOffsetMHz = (1030.0 * 1000000.0);
            if (lines?.Length > 0)
            {
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0)
                    {
                        try
                        {
                            var split = trimmed.Split('|');

                            var name = "";
                            int midsChannel = 0;
                            if (split.Length >= 2)
                            {
                                name = split[0];
                                midsChannel = Int32.Parse(split[1], CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                name = trimmed;
                                midsChannel = Int32.Parse(trimmed, CultureInfo.InvariantCulture);
                            }

                            if (midsChannel > 0 && midsChannel < 126)
                            {
                                channels.Add(new PresetChannel()
                                {
                                    Text = name + " | "+midsChannel,
                                    Value = (midsChannel * MHz) + MidsOffsetMHz
                                });
                            }
                        }
                        catch (Exception)
                        {
                            Logger.Log(LogLevel.Info, "Error parsing frequency  " + trimmed);
                        }
                    }
                }
            }

            int i = 1;
            foreach (var channel in channels)
            {
                channel.Text = i + ": " + channel.Text;
                i++;
            }

            return channels;
        }

        private string FindRadioFile(string radioName)
        {
            var files = Directory.EnumerateFiles(PresetsFolder);

            foreach (var fileAndPath in files)
            {
                if (Path.GetExtension(fileAndPath).ToLowerInvariant() == ".txt")
                {
                    var name = Path.GetFileNameWithoutExtension(fileAndPath);

                    if (NormaliseString(name) == radioName)
                    {
                        return fileAndPath;
                    }
                }
            }
            return null;
        }

        private string NormaliseString(string str)
        {
            //only allow alphanumeric, remove all spaces etc
            return Regex.Replace(str, "[^a-zA-Z0-9]", "").ToLower();
        }
    }
}