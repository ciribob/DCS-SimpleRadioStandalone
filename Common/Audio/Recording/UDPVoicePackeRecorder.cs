using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

public class UDPVoicePackeRecorder
{
    private readonly string _directory;
    private readonly object _lock = new object();

    /// <summary>
    /// The maximum allowed gap between recordings in the same archive.
    /// If a new packet arrives after this gap, a new archive will be created.
    /// </summary>
    public TimeSpan MaxArchiveGap { get; set; } = TimeSpan.FromMinutes(15);

    public UDPVoicePackeRecorder(string directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory) ? "VoiceRecordings" : directory;
        if (!Directory.Exists(_directory))
            Directory.CreateDirectory(_directory);
    }

    // Inner class for minimal packet serialization
    private class SerializableVoicePacket
    {
        public long Time { get; set; } // Epoch time in milliseconds
        public SRClientBase Client { get; set; }
        public UDPVoicePacket Outgoing_Transmission { get; set; }


        public SerializableVoicePacket(SRClientBase client, UDPVoicePacket packet)
        {
            Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Set to current epoch time (ms)
            Client = client;
            Outgoing_Transmission = packet;
        }
    }

    public void SavePacket(SRClientBase client, UDPVoicePacket packet)
    {
        if (packet == null || packet.Frequencies == null || packet.Frequencies.Length == 0)
            throw new ArgumentException("Packet or Frequencies are null/empty");

        double frequency = packet.Frequencies.First();
        string freqString = frequency.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);

        // Use project enum for modulation string
        string modulation = "none";
        if (packet.Modulations != null && packet.Modulations.Length > 0)
        {
            try
            {
                var modValue = packet.Modulations[0];
                modulation = ((Modulation)modValue).ToString();
            }
            catch
            {
                modulation = packet.Modulations[0].ToString();
            }
        }

        // Assemble filename: <coalition>-<freq>-<modulation>.json
        string jsonFileName = $"{client.Coalition}_{freqString}_{modulation}.json";

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string archivePath = null;
        long archiveStart = now, archiveEnd = now;

        lock (_lock)
        {
            // 1. Find latest archive
            var files = Directory.GetFiles(_directory, "packets_archive_*.gz");
            string latestArchive = files
                .OrderByDescending(f => f)
                .FirstOrDefault();

            bool appendToExisting = false;

            if (latestArchive != null)
            {
                // 2. Extract start and end times from filename
                var name = Path.GetFileNameWithoutExtension(latestArchive);
                var parts = name.Split('_');
                if (parts.Length == 4 &&
                    long.TryParse(parts[2], out long start) &&
                    long.TryParse(parts[3], out long end))
                {
                    // 3. Read last packet time from archive
                    long lastPacketTime = end;
                    try
                    {
                        using (FileStream gzStream = new FileStream(latestArchive, FileMode.Open, FileAccess.Read))
                        using (GZipStream decompress = new GZipStream(gzStream, CompressionMode.Decompress))
                        using (var archive = new ZipArchive(decompress, ZipArchiveMode.Read, false))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                using (var entryStream = entry.Open())
                                using (var reader = new StreamReader(entryStream))
                                {
                                    var json = reader.ReadToEnd();
                                    var data_packets = JsonSerializer.Deserialize<List<SerializableVoicePacket>>(json);
                                    if (data_packets != null && data_packets.Count > 0)
                                    {
                                        var maxTime = data_packets.Max(p => p.Time);
                                        if (maxTime > lastPacketTime)
                                            lastPacketTime = maxTime;
                                    }
                                }
                            }
                        }
                    }
                    catch { /* ignore errors, fallback to filename times */ }

                    // Use the class property for the gap
                    if (now - lastPacketTime <= MaxArchiveGap.TotalMilliseconds)
                    {
                        appendToExisting = true;
                        archivePath = latestArchive;
                        archiveStart = start;
                        archiveEnd = now;
                    }
                }
            }

            if (!appendToExisting)
            {
                archiveStart = now;
                archiveEnd = now;
                archivePath = Path.Combine(_directory, $"packets_archive_{archiveStart}_{archiveEnd}.gz");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            // Extract only the required JSON file if it exists in the archive
            if (appendToExisting && File.Exists(archivePath))
            {
                using (FileStream gzStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
                using (GZipStream decompress = new GZipStream(gzStream, CompressionMode.Decompress))
                using (var archive = new ZipArchive(decompress, ZipArchiveMode.Read, false))
                {
                    var entry = archive.GetEntry(jsonFileName);
                    if (entry != null)
                    {
                        string outPath = Path.Combine(tempDir, jsonFileName);
                        entry.ExtractToFile(outPath, true);
                    }
                }
            }

            string freqJsonPath = Path.Combine(tempDir, jsonFileName);
            List<SerializableVoicePacket> packets;
            if (File.Exists(freqJsonPath))
            {
                string existing = File.ReadAllText(freqJsonPath);
                packets = JsonSerializer.Deserialize<List<SerializableVoicePacket>>(existing) ?? new List<SerializableVoicePacket>();
            }
            else
            {
                packets = new List<SerializableVoicePacket>();
            }

            packets.Add(new SerializableVoicePacket(client, packet));

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(freqJsonPath, JsonSerializer.Serialize(packets, options));

            // Repack all JSON files into a new GZ archive
            string tempGzPath = Path.Combine(tempDir, $"packets_archive_{archiveStart}_{archiveEnd}.gz");
            using (FileStream gzStream = new FileStream(tempGzPath, FileMode.Create, FileAccess.Write))
            using (GZipStream compress = new GZipStream(gzStream, CompressionLevel.Optimal))
            using (var archive = new ZipArchive(compress, ZipArchiveMode.Create, false))
            {
                // Add all existing entries from the original archive except the one we're updating
                if (appendToExisting && File.Exists(archivePath))
                {
                    using (FileStream origGzStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
                    using (GZipStream origDecompress = new GZipStream(origGzStream, CompressionMode.Decompress))
                    using (var origArchive = new ZipArchive(origDecompress, ZipArchiveMode.Read, false))
                    {
                        foreach (var entry in origArchive.Entries)
                        {
                            if (entry.FullName != jsonFileName)
                            {
                                var newEntry = archive.CreateEntry(entry.FullName);
                                using (var entryStream = entry.Open())
                                using (var newEntryStream = newEntry.Open())
                                {
                                    entryStream.CopyTo(newEntryStream);
                                }
                            }
                        }
                    }
                }

                // Add or update the current JSON file
                var updatedEntry = archive.CreateEntry(jsonFileName);
                using (var entryStream = updatedEntry.Open())
                using (var fileStream = File.OpenRead(freqJsonPath))
                {
                    fileStream.CopyTo(entryStream);
                }
            }

            // Move/rename the archive if needed
            if (appendToExisting && archivePath != tempGzPath)
            {
                File.Delete(archivePath);
            }
            File.Copy(tempGzPath, Path.Combine(_directory, $"packets_archive_{archiveStart}_{archiveEnd}.gz"), true);
            Directory.Delete(tempDir, true);
        }
    }
}