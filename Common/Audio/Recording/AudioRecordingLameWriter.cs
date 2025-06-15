using System;
using System.Collections.Generic;
using System.IO;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.Wave;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

internal class AudioRecordingLameWriter
{
    protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly byte[] _byteBuf;

    private readonly float[] _floatBuf;
    private readonly int _maxSamples;
    private readonly List<string> _mp3FilePaths;
    private readonly List<LameMP3FileWriter> _mp3FileWriters;
    private readonly int _sampleRate;

    private readonly List<AudioRecordingStream> _streams;
    private readonly WaveFormat _waveFormat;

    // construct an audio file writer that uses LAME to encode the streams in an .mp3 file using
    // the specified sample rate. the streams list provides the audio streams that supply the
    // audio data, each stream is written to its own file (named per the current date and time
    // and the stream tag). the writer will process the specified maximum number of samples per
    // call to ProcessAudio().
    public AudioRecordingLameWriter(List<AudioRecordingStream> streams, int sampleRate, int maxSamples)
    {
        _streams = streams;
        _sampleRate = sampleRate;
        _maxSamples = maxSamples;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _mp3FilePaths = new List<string>();
        _mp3FileWriters = new List<LameMP3FileWriter>();

        _floatBuf = new float[maxSamples];
        _byteBuf = new byte[maxSamples * sizeof(float)];

        if (!Directory.Exists("Recordings"))
        {
            _logger.Info("Recordings directory missing, creating directory");
            Directory.CreateDirectory("Recordings");
        }
    }

    // attempt to write up to the max samples from each stream to their output files. this will
    // start the writer if it is not currently started.
    public void ProcessAudio()
    {
        if (_mp3FileWriters.Count == 0) Start();

        try
        {
            // read from each of the streams and write the results to the file associated with
            // the stream. note that a "stream" here can be a mix of multiple audio streams.
            for (var i = 0; i < _streams.Count; i++)
            {
                var byteCount = _streams[i].Read(_floatBuf, _maxSamples) * sizeof(float);
                Buffer.BlockCopy(_floatBuf, 0, _byteBuf, 0, byteCount);
                _mp3FileWriters[i].Write(_byteBuf, 0, byteCount);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Unable to write audio samples to output file: {ex.Message}");
        }
    }

    public void Start()
    {
        // streams are stored in Recordings directory, named "<date>-<time><tag>.mp3"
        var sanitisedDate = string.Join("-", DateTime.Now.ToShortDateString().Split(Path.GetInvalidFileNameChars()));
        var sanitisedTime = string.Join("-", DateTime.Now.ToLongTimeString().Split(Path.GetInvalidFileNameChars()));
        var filePathBase = $"Recordings\\{sanitisedDate}-{sanitisedTime}";

        var lamePreset = LAMEPreset.ABR_320;// ABR_320(LAMEPreset)Enum.Parse(typeof(LAMEPreset),
            //GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.RecordingQuality).RawValue);

        for (var i = 0; i < _streams.Count; i++)
        {
            var tag = _streams[i].Tag;
            if (tag == null || tag.Length == 0) tag = "";
            _mp3FilePaths.Add(filePathBase + tag + ".mp3");
            _mp3FileWriters.Add(new LameMP3FileWriter(_mp3FilePaths[i], _waveFormat, lamePreset));
        }
    }

    public void Stop()
    {
        // this should be sufficient to get any left over samples into the files. might be
        // better to process until streams have no more data, but laziness intensifies...
        ProcessAudio();
        ProcessAudio();

        foreach (var writer in _mp3FileWriters)
        {
            writer.Flush();
            writer.Dispose();
        }

        _mp3FileWriters.Clear();
        _mp3FilePaths.Clear();
    }
}