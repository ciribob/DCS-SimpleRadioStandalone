﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
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
    private readonly int _maxSamples;
    private readonly List<AudioRecordingStreamHydrated> _playerFullQueues;
    private readonly List<CircularFloatBuffer> _playerRawQueues;
    private readonly List<AudioRecordingStream> _radioFullQueues;

    // TODO: drop in favor of AudioManager.OUTPUT_SAMPLE_RATE
    private readonly int _sampleRate;

    private readonly ClientEffectsPipeline pipeline = new();

    private AudioRecordingLameWriter _audioRecordingWriter;
    private string _clientGuid; //player guid
    private bool _processThreadDone;

    private bool _stop;

    private AudioRecordingManager()
    {
        _sampleRate = Constants.OUTPUT_SAMPLE_RATE;
        _maxSamples = _sampleRate * MAX_BUFFER_SECONDS;

        _stop = true;

        _clientRawQueues = new List<CircularFloatBuffer>();
        _playerRawQueues = new List<CircularFloatBuffer>();
        _clientFullQueues = new List<AudioRecordingStreamHydrated>();
        _playerFullQueues = new List<AudioRecordingStreamHydrated>();
        _radioFullQueues = new List<AudioRecordingStream>();
    }

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

        var clientBuffer = new float[_maxSamples];
        var playerBuffer = new float[_maxSamples];

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

    private float[] SingleRadioMixDown(List<DeJitteredTransmission> mainAudio,
        List<DeJitteredTransmission> secondaryAudio, int radio, out int count)
    {
        //should be no more than 80 ms of audio
        //should really be 40 but just in case
        //TODO reuse this but return a new array of the right length
        var mixBuffer = new float[Constants.OUTPUT_SEGMENT_FRAMES * 2];
        var secondaryMixBuffer = new float[0];

        var primarySamples = 0;
        var secondarySamples = 0;
        var outputSamples = 0;

        //run this sample through - mix down all the audio for now PER radio 
        //we can then decide what to do with it later
        //same pipeline (ish) as RadioMixingProvider

        if (mainAudio?.Count > 0)
            mixBuffer = pipeline.ProcessClientTransmissions(mixBuffer, mainAudio,
                out primarySamples);

        //handle guard
        if (secondaryAudio?.Count > 0)
        {
            secondaryMixBuffer = new float[Constants.OUTPUT_SEGMENT_FRAMES * 2];
            secondaryMixBuffer =
                pipeline.ProcessClientTransmissions(secondaryMixBuffer, secondaryAudio, out secondarySamples);
        }

        if (primarySamples > 0 || secondarySamples > 0)
            mixBuffer = AudioManipulationHelper.MixArraysClipped(mixBuffer, primarySamples, secondaryMixBuffer,
                secondarySamples, out outputSamples);

        count = outputSamples;

        return mixBuffer;
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
            _clientRawQueues.Add(new CircularFloatBuffer(_maxSamples));
            _playerRawQueues.Add(new CircularFloatBuffer(_maxSamples));

            _clientFullQueues.Add(new AudioRecordingStreamHydrated(_maxSamples, $"{i}.c"));
            _playerFullQueues.Add(new AudioRecordingStreamHydrated(_maxSamples, $"{i}.p"));

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
            _audioRecordingWriter = new AudioRecordingLameWriter(streams, _sampleRate, _maxSamples);
        }
        else
        {
            // write per-radio audio files. create a write with N streams, one for each of the
            // radios.

            _audioRecordingWriter = new AudioRecordingLameWriter(_radioFullQueues, _sampleRate, _maxSamples);
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

    public void AppendPlayerAudio(float[] transmission, int radioId)
    {
        //only record if we need too
        if (!_stop && GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio))
            _playerRawQueues[radioId]?.Write(transmission, 0, transmission.Length);
    }

    public void AppendClientAudio(List<DeJitteredTransmission> mainAudio, List<DeJitteredTransmission> secondaryAudio,
        int radioId)
    {
        //only record if we need too
        if (!_stop && GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio))
        {
            mainAudio = FilterTransmisions(mainAudio);
            secondaryAudio = FilterTransmisions(secondaryAudio);

            var buf = SingleRadioMixDown(mainAudio, secondaryAudio, radioId, out var count);
            if (count > 0) _clientRawQueues[radioId].Write(buf, 0, count);
        }
    }

    private List<DeJitteredTransmission> FilterTransmisions(List<DeJitteredTransmission> originalTransmissions)
    {
        if (originalTransmissions == null || originalTransmissions.Count == 0)
            return new List<DeJitteredTransmission>();

        var filteredTransmisions = new List<DeJitteredTransmission>();

        foreach (var transmission in originalTransmissions)
            if (_connectedClientsSingleton.TryGetValue(transmission.OriginalClientGuid, out var client))
            {
                if (client.AllowRecord
                    || transmission.OriginalClientGuid ==
                    _clientGuid) // Assume that client intends to record their outgoing transmissions
                {
                    filteredTransmisions.Add(transmission);
                }
                else if (GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.DisallowedAudioTone))
                {
                    var toneTransmission = new DeJitteredTransmission
                    {
                        PCMMonoAudio =
                            AudioManipulationHelper.SineWaveOut(transmission.PCMAudioLength, _sampleRate, 0.25),
                        ReceivedRadio = transmission.ReceivedRadio,
                        PCMAudioLength = transmission.PCMAudioLength,
                        Decryptable = transmission.Decryptable,
                        Frequency = transmission.Frequency,
                        Guid = transmission.Guid,
                        IsSecondary = transmission.IsSecondary,
                        Modulation = transmission.Modulation,
                        NoAudioEffects = transmission.NoAudioEffects,
                        OriginalClientGuid = transmission.OriginalClientGuid,
                        Volume = transmission.Volume
                    };
                    filteredTransmisions.Add(toneTransmission);
                }
            }

        return filteredTransmisions;
    }
}