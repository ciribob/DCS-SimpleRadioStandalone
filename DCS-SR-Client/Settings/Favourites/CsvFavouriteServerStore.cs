using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;

public class CsvFavouriteServerStore : IFavouriteServerStore
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string _fileNameAndPath;

    public CsvFavouriteServerStore()
    {
        // var path = GlobalSettingsStore.Instance.Path;
        //
        // if (path.Length == 0)
        // {
        var path = Environment.CurrentDirectory;
        // }

        _fileNameAndPath = Path.Combine(path, "FavouriteServers.csv");
    }

    public IEnumerable<ServerAddress> LoadFromStore()
    {
        try
        {
            if (File.Exists(_fileNameAndPath)) return ReadFile();
        }
        catch (Exception exception)
        {
            var message = $"Failed to load settings: {exception}";
            Logger.Error(exception, message);
            MessageBox.Show(message);
        }

        return Enumerable.Empty<ServerAddress>();
    }

    public bool SaveToStore(IEnumerable<ServerAddress> addresses)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var address in addresses)
                sb.AppendLine($"{address.Name},{address.Address},{address.IsDefault},{address.EAMCoalitionPassword}");
            File.WriteAllText(_fileNameAndPath, sb.ToString(), new UTF8Encoding(false, true));

            return true;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Failed to save preferences");
        }

        return false;
    }

    private IEnumerable<ServerAddress> ReadFile()
    {
        var allLines = File.ReadAllLines(_fileNameAndPath);
        IList<ServerAddress> addresses = new List<ServerAddress>();

        foreach (var line in allLines)
            try
            {
                var address = Parse(line);
                addresses.Add(address);
            }
            catch (Exception ex)
            {
                var message = $"Failed to parse address from csv, text: {line}";
                Logger.Error(ex, message);
            }

        return addresses;
    }

    private ServerAddress Parse(string line)
    {
        var split = line.Split(',');
        if (split.Length >= 3)
        {
            bool isDefault;

            if (bool.TryParse(split[2], out isDefault))
                return new ServerAddress(split[0], split[1],
                    split.Length >= 4 && !string.IsNullOrWhiteSpace(split[3]) ? split[3] : null, isDefault);
            throw new ArgumentException("isDefault parameter cannot be cast to a boolean");
        }

        throw new ArgumentOutOfRangeException(nameof(line), @"address must be at least 3 segments");
    }
}