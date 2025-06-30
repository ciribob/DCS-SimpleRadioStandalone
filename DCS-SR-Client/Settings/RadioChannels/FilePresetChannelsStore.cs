using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;

public partial class FilePresetChannelsStore : IPresetChannelsStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
    public static  readonly double MHz = 1000000.0;
    public static readonly double MidsOffsetMHz = 1030.0 * 1000000.0;

    private string PresetsFolder
    {
        get
        {
            var folder = _globalSettings.GetClientSetting(GlobalSettingsKeys.LastPresetsFolder).RawValue;
            if (string.IsNullOrWhiteSpace(folder)) folder = Directory.GetCurrentDirectory();

            return folder;
        }
    }

    public IEnumerable<PresetChannel> LoadFromStore(string radioName, bool mids = false)
    {
        var file = FindRadioFile(NormaliseString(radioName));

        if (file != null)
        {
            if (mids) return ReadMidsFrequenciesFromFile(file);

            return ReadFrequenciesFromFile(file);
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
        var channels = new List<PresetChannel>();
        var lines = File.ReadAllLines(filePath);
        
        if (lines?.Length > 0)
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    try
                    {
                        var split = trimmed.Split('|');

                        var name = "";
                        double frequency = 0;
                        if (split.Length >= 2)
                        {
                            name = split[0];
                            frequency = double.Parse(split[1], CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            name = trimmed;
                            frequency = double.Parse(trimmed, CultureInfo.InvariantCulture);
                        }

                        channels.Add(new PresetChannel
                        {
                            Text = name,
                            Value = frequency * MHz
                        });
                    }
                    catch (Exception)
                    {
                        Logger.Log(LogLevel.Info, "Error parsing frequency  " + trimmed);
                    }
            }

        return channels;
    }

    private List<PresetChannel> ReadMidsFrequenciesFromFile(string filePath)
    {
        var channels = new List<PresetChannel>();
        var lines = File.ReadAllLines(filePath);

       
        if (lines?.Length > 0)
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    try
                    {
                        var split = trimmed.Split('|');

                        var name = "";
                        var midsChannel = 0;
                        if (split.Length >= 2)
                        {
                            name = split[0];
                            midsChannel = int.Parse(split[1], CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            name = trimmed;
                            midsChannel = int.Parse(trimmed, CultureInfo.InvariantCulture);
                        }

                        if (midsChannel > 0 && midsChannel < 126)
                            channels.Add(new PresetChannel
                            {
                                Text = name,
                                Value = midsChannel * MHz + MidsOffsetMHz,
                                MidsChannel = midsChannel
                            });
                    }
                    catch (Exception)
                    {
                        Logger.Log(LogLevel.Info, "Error parsing frequency  " + trimmed);
                    }
            }

        return channels;
    }

    private string FindRadioFile(string radioName)
    {
        var files = Directory.EnumerateFiles(PresetsFolder);

        foreach (var fileAndPath in files)
            if (Path.GetExtension(fileAndPath).ToLowerInvariant() == ".txt")
            {
                var name = Path.GetFileNameWithoutExtension(fileAndPath);

                if (radioName.StartsWith(NormaliseString(name))) return fileAndPath;
            }

        return null;
    }

    public static string NormaliseString(string str)
    {
        //only allow alphanumeric, remove all spaces etc
        return NormaliseRegex().Replace(str, "").ToLower();
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex NormaliseRegex();
}