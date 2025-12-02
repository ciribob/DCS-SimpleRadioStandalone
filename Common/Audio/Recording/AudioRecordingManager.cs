using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

public class AudioRecordingManager
{
    private const int MAX_BUFFER_SECONDS = 3;

    // TODO: should this be something more dynamic or in a more global scope?
    private const int MAX_RADIOS = 11;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private static volatile AudioRecordingManager _instance = new();
    private static readonly object _lock = new();

    // full queues carry per-radio hydrated audio samples reconstructed from the raw data.
    private readonly List<AudioRecordingStreamHydrated> _clientFullQueues;

    // raw queues carry per-radio dehydrated audio samples.
    private readonly List<CircularFloatBuffer> _clientRawQueues;

    private readonly ConnectedClientsSingleton _connectedClientsSingleton = ConnectedClientsSingleton.Instance;
    private readonly List<AudioRecordingStreamHydrated> _playerFullQueues;
    private readonly List<CircularFloatBuffer> _playerRawQueues;
    private readonly List<AudioRecordingStream> _radioFullQueues;

    // TODO: drop in favor of AudioManager.OUTPUT_SAMPLE_RATE
    private int SampleRate { get; } = Constants.OUTPUT_SAMPLE_RATE;
    private int MaxSamples => SampleRate * MAX_BUFFER_SECONDS;

    private AudioRecordingWriterBase _audioRecordingWriter;
    private string _clientGuid; //player guid
    private bool _processThreadDone;

    private bool _stop;
    private AudioRecordingManager()
    {
        _stop = true;

        _clientRawQueues = new List<CircularFloatBuffer>();
        _playerRawQueues = new List<CircularFloatBuffer>();
        _clientFullQueues = new List<AudioRecordingStreamHydrated>();
        _playerFullQueues = new List<AudioRecordingStreamHydrated>();
        _radioFullQueues = new List<AudioRecordingStream>();
    }

    public IReadOnlyList<string> AvailableFormats { get; } = new List<string>() { "mp3", "opus" };

    public static AudioRecordingManager Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new AudioRecordingManager();
                }

            return _instance;
        }
    }

    private void ProcessQueues()
    {
        var timer = new Stopwatch();
        long tickTime;

        var isRecording = false;

        var clientBuffer = new float[MaxSamples];
        var playerBuffer = new float[MaxSamples];

        _processThreadDone = false;

        timer.Start();

        // wait until we have some audio to record. avoids a bunch of dead air at recording start.
        while (!_stop && !isRecording)
        {
            tickTime = timer.ElapsedMilliseconds;

            Thread.Sleep(500);

            // if we're recording audio, check to see if any mixdown queue has data. if so,
            // start recording. if we're not recording, just run this thread doing nothing.
            for (var i = 0; i < MAX_RADIOS; i++)
                if (_playerRawQueues[i].Count > 0 || _clientRawQueues[i].Count > 0)
                {
                    for (var j = 0; j < MAX_RADIOS; j++)
                    {
                        _clientFullQueues[j].StartRecording(tickTime);
                        _playerFullQueues[j].StartRecording(tickTime);
                    }

                    isRecording = true;
                    break;
                }
        }

        _logger.Info("Transmission recording started.");

        // record audio. pull samples from the raw queues and hydrate them in the full queues.
        // once we've swept through all the queues, update the output file(s) as necessary.
        // the processing occurs at a beat rate, or tick, of ~500ms. in each tick, we pull all
        // samples that have arrived from the user of the manager, inject dead air as needed,
        // and stream available audio to the recording files.
        //
        // the algorithm assumes that, to first order, the dead air is reflected in intervals
        // between writes to the raw queues (by AppendPlayerAudio() and AppendClientAudio()
        // methods). that is, raw queue writes for clips separated by, say, 2s of dead air will
        // be roughly 2s apart. this should naturally happen as long as the rest of the client
        // is providing audio as it occurs and not buffering.
        //
        // this assumption can be lifted by tracking more detailed timing information on when
        // groups of samples in the queue appear in the "real" audio stream in addition to the
        // samples themselves.
        while (!_stop && isRecording)
        {
            try
            {
                tickTime = timer.ElapsedMilliseconds;
                for (var i = 0; i < MAX_RADIOS; i++)
                {
                    var playerAudioLength = _playerRawQueues[i].Read(playerBuffer, 0, playerBuffer.Length);
                    _playerFullQueues[i].WriteRawSamples(tickTime, playerBuffer, playerAudioLength);

                    var clientAudioLength = _clientRawQueues[i].Read(clientBuffer, 0, clientBuffer.Length);
                    _clientFullQueues[i].WriteRawSamples(tickTime, clientBuffer, clientAudioLength);
                }

                _audioRecordingWriter.ProcessAudio();
            }
            catch (Exception ex)
            {
                _logger.Error($"Recording process failed: {ex}");
            }

            Thread.Sleep(500);
        }

        _logger.Info("Transmission recording ended, draining audio.");

        // drain audio. will spin for a bit to let everyone catch their breath. _stop should
        // prevent any additional samples from coming in. pad out all the full queues to have
        // the same number of samples. the shutdown of _audioRecordingWriter will drain this
        // to any recording files.

        Thread.Sleep(500);

        var maxSamples = int.MinValue;
        var minSamples = int.MaxValue;
        for (var i = 0; i < MAX_RADIOS; i++)
        {
            var samples = _playerFullQueues[i].Count();
            maxSamples = Math.Max(maxSamples, samples);
            minSamples = Math.Min(minSamples, samples);

            samples = _clientFullQueues[i].Count();
            maxSamples = Math.Max(maxSamples, samples);
            minSamples = Math.Min(minSamples, samples);
        }

        var fillBuf = new float[maxSamples - minSamples];
        for (var i = 0; i < MAX_RADIOS; i++)
        {
            _playerFullQueues[i].Write(fillBuf, maxSamples - _playerFullQueues[i].Count());
            _clientFullQueues[i].Write(fillBuf, maxSamples - _clientFullQueues[i].Count());
        }

        timer.Stop();

        _logger.Info("Stop recording thread");

        _processThreadDone = true;
    }

    private static AudioRecordingWriterBase CreateWriter(IReadOnlyList<AudioRecordingStream> streams, int sampleRate, int maxSamples)
    {
        var desired = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.RecordingFormat).StringValue;
        switch (desired)
        {
            case "mp3":
                return new AudioRecordingLameWriter(streams, sampleRate, maxSamples);

            case "opus":
            default:
                return new AudioRecordingOpusWriter(streams, sampleRate, maxSamples);
        }
    }

    public void Start(string clientGuid)
    {
        _clientGuid = clientGuid;
        if (!GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio))
        {
            _processThreadDone = true;
            _logger.Info("Transmission recording disabled");
            return;
        }

        _logger.Info("Transmission recording waiting for audio.");

        // clear out existing queue lists and rebuild them from scratch. queues include
        // dehydrated (w/o dead air) and hydrated (w/ dead air) per-radio sample queues for
        // client and player sources. also, set up a mixer to combine player and client
        // sources into a per-radio audio stream.

        _clientRawQueues.Clear();
        _playerRawQueues.Clear();
        _clientFullQueues.Clear();
        _playerFullQueues.Clear();
        _radioFullQueues.Clear();

        for (var i = 0; i < MAX_RADIOS; i++)
        {
            _clientRawQueues.Add(new CircularFloatBuffer(MaxSamples));
            _playerRawQueues.Add(new CircularFloatBuffer(MaxSamples));

            _clientFullQueues.Add(new AudioRecordingStreamHydrated(MaxSamples, $"{i}.c"));
            _playerFullQueues.Add(new AudioRecordingStreamHydrated(MaxSamples, $"{i}.p"));

            var streams = new List<AudioRecordingStream>
            {
                _clientFullQueues[i],
                _playerFullQueues[i]
            };
            _radioFullQueues.Add(new AudioRecordingStreamMixer(streams, $"-Radio-{i}"));
        }

        // setup the recording writer to emit a single file that contains a mix of all radios
        // or multiple files that contains per radio traffic. stop any existing writer first.

        _audioRecordingWriter?.Stop();

        if (GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.SingleFileMixdown))
        {
            // write single mixed file. create a writer with a single stream source: a mixer
            // that combines all per-radio streams.

            var streams = new List<AudioRecordingStream>
            {
                new AudioRecordingStreamMixer(_radioFullQueues, "-All")
            };
            _audioRecordingWriter = CreateWriter(streams, SampleRate, MaxSamples);
        }
        else
        {
            // write per-radio audio files. create a write with N streams, one for each of the
            // radios.

            _audioRecordingWriter = CreateWriter(_radioFullQueues, SampleRate, MaxSamples);
        }

        _stop = false;
        _processThreadDone = false;

        new Thread(ProcessQueues).Start();
    }

    public void Stop()
    {
        if (!_stop)
        {
            _stop = true;
            for (var i = 0; !_processThreadDone && i < 10; i++) Thread.Sleep(200);
            _audioRecordingWriter?.Stop();
            _audioRecordingWriter = null;
            _logger.Info("Transmission recording stopped.");
        }
    }

    public void AppendPlayerAudio(float[] transmission, int length, int radioId)
    {
        //only record if we need too
        if (!_stop && GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio))
            _playerRawQueues[radioId]?.Write(transmission, 0, length);
    }

    internal void AppendClientAudio(int radioId, IReadOnlyList<TransmissionSegment> segments)
    {
        if (_stop || GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio) == false)
            return;
        
        // Audio is preprocessed, all we need to do is run the filtering and mixdown.
        var floatPool = ArrayPool<float>.Shared;

        var mixLength = 0;
        float[] mixBuffer = null;
        var disallowedTone = GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.DisallowedAudioTone);
        foreach (var segment in segments)
        {
            if (_connectedClientsSingleton.TryGetValue(segment.OriginalClientGuid, out var client))
            {
                var segmentSpan = segment.Audio.AsSpan();
                if (mixLength < segmentSpan.Length)
                {
                    var resizedBuffer = floatPool.Rent(segmentSpan.Length);
                    if (mixBuffer != null)
                    {
                        mixBuffer.AsSpan(0, mixLength).CopyTo(resizedBuffer);
                        floatPool.Return(mixBuffer);
                    }

                    // Make sure the newly allocated area is clear.
                    resizedBuffer.AsSpan(mixLength, segmentSpan.Length - mixLength).Clear();
                    mixBuffer = resizedBuffer;
                    mixLength = segmentSpan.Length;
                }

                if (client.AllowRecord
                    || segment.OriginalClientGuid == _clientGuid) // Assume that client intends to record their outgoing transmissions
                {
                    AudioManipulationHelper.MixArraysClipped(mixBuffer.AsSpan(0, segmentSpan.Length), segmentSpan);
                }
                else if (disallowedTone)
                {
                    var audioTone = AudioManipulationHelper.SineWaveOut(segmentSpan.Length, SampleRate, 0.25);
                    AudioManipulationHelper.MixArraysClipped(mixBuffer.AsSpan(0, segmentSpan.Length), audioTone.AsSpan(0, audioTone.Length));
                }
            }
        }

        if (mixLength > 0)
        {
            _clientRawQueues[radioId].Write(mixBuffer, 0, mixLength);
            floatPool.Return(mixBuffer);
        }
    }
}