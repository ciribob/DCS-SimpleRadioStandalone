using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Core;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.CoreAudioApi;
using NAudio.Utils;
using NAudio.Wave;
using NLog;
using WebRtcVadSharp;
using Application = Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Opus.Application;
using LogManager = NLog.LogManager;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;

public class AudioManager : IHandle<SRClientUpdateMessage>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;
    private readonly AudioOutputSingleton _audioOutputSingleton = AudioOutputSingleton.Instance;

    private readonly AudioRecordingManager _audioRecordingManager = AudioRecordingManager.Instance;

    private readonly ConcurrentDictionary<string, ClientAudioProvider> _clientsBufferedAudio = new();

    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    private readonly string _guid;

    private readonly Queue<short> _micInputQueue = new(Constants.MIC_SEGMENT_FRAMES * 3);
    private readonly byte[] _pcmBytes = new byte[Constants.MIC_SEGMENT_FRAMES * 2];


    //buffers intialised once for use repeatedly
    private readonly short[] _pcmShort = new short[Constants.MIC_SEGMENT_FRAMES];
    //Stopwatch _stopwatch = new Stopwatch();

    private readonly object lockObj = new();

    //MIC SEGMENT FRAMES IS SHORTS not bytes - which is two bytes
    //however we only want half of a frame IN BYTES not short - so its MIC_SEGMENT_FRAMES *2 (for bytes) then / 2 for bytes again
    //declare here to save on garbage collection
    private readonly byte[] tempBuffferFirst20ms = new byte[Constants.MIC_SEGMENT_FRAMES];
    private readonly byte[] tempBuffferSecond20ms = new byte[Constants.MIC_SEGMENT_FRAMES];

    private readonly bool windowsN;

    private OpusEncoder _encoder;

    private int _errorCount;

    private SRSMixingSampleProvider _finalMixdown;

    private SRSWasapiOut _micWaveOut;
    private BufferedWaveProvider _micWaveOutBuffer;

    private ClientAudioProvider _passThroughAudioProvider;

    private List<RadioMixingProvider> _radioMixingProvider;
    private EventDrivenResampler _resampler;

    private float _speakerBoost = 1.0f;
    private SpeexProcessor _speex;

    private byte[] _tempMicOutputBuffer;

    //private Stopwatch _stopwatch = new();

    private UDPClientAudioProcessor _udpClientAudioProcessor;
    private UDPVoiceHandler _udpVoiceHandler;
    private VolumeSampleProviderWithPeak _volumeSampleProvider;

    private WebRtcVad _voxDectection;

    private WasapiCapture _wasapiCapture;

    private SRSWasapiOut _waveOut;

    public AudioManager(bool windowsN)
    {
        this.windowsN = windowsN;
        _guid = ClientStateSingleton.Instance.ShortGUID;
    }

    public float MicMax { get; set; } = -100;
    public float SpeakerMax { get; set; } = -100;

    public float SpeakerBoost
    {
        get => _speakerBoost;
        set
        {
            _speakerBoost = value;
            if (_volumeSampleProvider != null) _volumeSampleProvider.Volume = value;
        }
    }

    public Task HandleAsync(SRClientUpdateMessage message, CancellationToken cancellationToken)
    {
        if (!message.Connected) RemoveClientBuffer(message.SrClient);

        return Task.CompletedTask;
    }

    public void InitWaveOut()
    {
        MMDevice speakers = null;
        if (_audioOutputSingleton.SelectedAudioOutput.Value == null)
            speakers = SRSWasapiOut.GetDefaultAudioEndpoint();
        else
            speakers = (MMDevice)_audioOutputSingleton.SelectedAudioOutput.Value;

        _waveOut = new SRSWasapiOut(speakers, AudioClientShareMode.Shared, true, 40, windowsN);

        //add final volume boost to all mixed audio
        _volumeSampleProvider = new VolumeSampleProviderWithPeak(_finalMixdown,
            peak => SpeakerMax = (float)VolumeConversionHelper.ConvertFloatToDB(peak));
        _volumeSampleProvider.Volume = SpeakerBoost;

        if (speakers.AudioClient.MixFormat.Channels == 1)
        {
            _waveOut.Init(_volumeSampleProvider.ToMono());
        }
        else
        {
            _waveOut.Init(_volumeSampleProvider.ToStereo());
        }

        _waveOut.Play();
    }

    private void InitMicPassthrough()
    {
        MMDevice micOutput = null;
        if (_audioOutputSingleton.SelectedMicAudioOutput.Value != null)
        {
            micOutput = (MMDevice)_audioOutputSingleton.SelectedMicAudioOutput.Value;

            _micWaveOut = new SRSWasapiOut(micOutput, AudioClientShareMode.Shared, true, 40, windowsN);

            _micWaveOutBuffer =
                new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 1));
            _micWaveOutBuffer.ReadFully = true;
            _micWaveOutBuffer.DiscardOnBufferOverflow = true;

            var sampleProvider = _micWaveOutBuffer.ToSampleProvider();

            if (micOutput.AudioClient.MixFormat.Channels == 1)
            {
                if (sampleProvider.WaveFormat.Channels == 2)
                    _micWaveOut.Init(sampleProvider.ToMono());
                else
                    //already mono
                    _micWaveOut.Init(sampleProvider);
            }
            else
            {
                if (sampleProvider.WaveFormat.Channels == 1)
                    _micWaveOut.Init(sampleProvider.ToStereo());
                else
                    //already stereo
                    _micWaveOut.Init(sampleProvider);
            }

            _micWaveOut.Play();
        }
    }

    public void InitEncodersSpeex()
    {
        //opus
        _encoder = OpusEncoder.Create(Constants.MIC_SAMPLE_RATE, 1, Application.Voip);
        _encoder.ForwardErrorCorrection = false;

        //speex
        _speex = new SpeexProcessor(Constants.MIC_SEGMENT_FRAMES, Constants.MIC_SAMPLE_RATE);
    }


    public void InitMicInput()
    {
        _passThroughAudioProvider = new ClientAudioProvider();
        
        var device = (MMDevice)_audioInputSingleton.SelectedAudioInput.Value;

        if (device == null) device = WasapiCapture.GetDefaultCaptureDevice();

        try
        {
            device.AudioEndpointVolume.Mute = false;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to forcibly unmute: " + ex.Message);
        }

        _wasapiCapture = new WasapiCapture(device, true);
        _wasapiCapture.ShareMode = AudioClientShareMode.Shared;
        _wasapiCapture.DataAvailable += WasapiCaptureOnDataAvailable;
        _wasapiCapture.RecordingStopped += WasapiCaptureOnRecordingStopped;

        _wasapiCapture.StartRecording();
    }

    public void StartEncoding(string guid, InputDeviceManager inputManager,
        IPEndPoint endPoint)
    {
        InitEncodersSpeex();

        try
        {
            _micInputQueue.Clear();

            InitMixers();

            //Audio manager should start / stop and cleanup based on connection successfull and disconnect
            //Should use listeners to synchronise all the state
            InitWaveOut();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error starting audio Output - Quitting! " + ex.Message);
            ShowOutputError("Problem Initialising Audio Output!");
            Environment.Exit(1);
        }

        try
        {
            InitMicPassthrough();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error starting mic audio Output - Quitting! " + ex.Message);

            ShowOutputError("Problem Initialising Mic Audio Output!");
            Environment.Exit(1);
        }

        if (_audioInputSingleton.MicrophoneAvailable)
            try
            {
                InitMicInput();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Input - Quitting! " + ex.Message);

                ShowInputError("Problem initialising Audio Input!");

                Environment.Exit(1);
            }

        InitVox();

        //Start UDP handler
        _udpVoiceHandler =
            new UDPVoiceHandler(guid, endPoint);
        

        _udpClientAudioProcessor = new UDPClientAudioProcessor(_udpVoiceHandler, this, guid);

        _udpVoiceHandler.Connect();
        _udpClientAudioProcessor.Start();

        EventBus.Instance.SubscribeOnBackgroundThread(this);

        AudioRecordingManager.Instance.Start(ClientStateSingleton.Instance.ShortGUID);
    }


    private void WasapiCaptureOnRecordingStopped(object sender, StoppedEventArgs e)
    {
        Logger.Error("Recording Stopped");
    }
    // private WaveFileWriter _beforeWaveFile;
    // private WaveFileWriter _afterFileWriter;


    private void WasapiCaptureOnDataAvailable(object sender, WaveInEventArgs e)
    {
        if (_resampler == null)
            //create and use in the same thread or COM issues
            _resampler = new EventDrivenResampler(windowsN, _wasapiCapture.WaveFormat,
                new WaveFormat(Constants.MIC_SAMPLE_RATE, 16, 1));
        // _afterFileWriter = new WaveFileWriter(@"C:\Temp\Test-Preview-after.wav", new WaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 16, 1));
        if (e.BytesRecorded > 0)
        {
            //Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Bytes: {e.BytesRecorded}");
            var resampledPCM16Bit = _resampler.Resample(e.Buffer, e.BytesRecorded);

            // Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Bytes: {resampledPCM16Bit.Length}");
            //fill sound buffer
            for (var i = 0; i < resampledPCM16Bit.Length; i++) _micInputQueue.Enqueue(resampledPCM16Bit[i]);

            var recordAudio = GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio);
            var floatPool = ArrayPool<float>.Shared;
            //read out the queue
            while (_micInputQueue.Count >= Constants.MIC_SEGMENT_FRAMES)
            {
                for (var i = 0; i < Constants.MIC_SEGMENT_FRAMES; i++) _pcmShort[i] = _micInputQueue.Dequeue();

                try
                {
                    //ready for the buffer shortly

                    //check for voice before any pre-processing
                    var voice = true;

                    if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.VOX))
                    {
                        Buffer.BlockCopy(_pcmShort, 0, _pcmBytes, 0, _pcmBytes.Length);
                        voice = DoesFrameContainSpeech(_pcmBytes, _pcmShort);
                    }

                    //process with Speex
                    _speex.Process(new ArraySegment<short>(_pcmShort));

                    //convert to dB
                    MicMax = (float)VolumeConversionHelper.CalculateRMS(_pcmShort);

                    //copy and overwrite with new PCM data post processing
                    Buffer.BlockCopy(_pcmShort, 0, _pcmBytes, 0, _pcmBytes.Length);

                    //encode as opus bytes
                    int len;
                    var buff = _encoder.Encode(_pcmBytes, _pcmBytes.Length, out len);

                    if (_udpVoiceHandler != null && buff != null && len > 0)
                    {
                        //create copy with small buffer
                        var encoded = new byte[len];

                        Buffer.BlockCopy(buff, 0, encoded, 0, len);

                        // Console.WriteLine("Sending: " + e.BytesRecorded);
                        var clientAudio = _udpClientAudioProcessor.Send(encoded, len, voice);

                        // _beforeWaveFile.Write(pcmBytes, 0, pcmBytes.Length);
                        
                        if (clientAudio != null)
                        {
                            //todo see if we can fix the resample / opus decode
                            //send audio so play over local too
                            //as its passthrough it comes out as PCM 16
                            var samplesAdded = _passThroughAudioProvider?.AddClientAudioSamples(clientAudio);
                            
                            var segment = _passThroughAudioProvider?.Read(clientAudio.ReceivedRadio, samplesAdded.GetValueOrDefault(0));

                            if (segment != null)
                            {
                                var audioSpan = segment.AudioSpan;
                                // passthrough, run without transforms.
                                if (_micWaveOutBuffer != null && _micWaveOut != null)
                                {
                                    //now its a processed Mono audio
                                    _tempMicOutputBuffer =
                                        BufferHelpers.Ensure(_tempMicOutputBuffer, audioSpan.Length * 4);
                                    MemoryMarshal.AsBytes(audioSpan).CopyTo(_tempMicOutputBuffer);

                                    //_beforeWaveFile?.WriteSamples(jitterBufferAudio.Audio,0,jitterBufferAudio.Audio.Length);
                                    //_beforeWaveFile?.Write(pcm32, 0, pcm32.Length);
                                    //_beforeWaveFile?.Flush();

                                    _micWaveOutBuffer.AddSamples(_tempMicOutputBuffer, 0, segment.AudioSpan.Length * 4);
                                }

                                if (recordAudio)
                                {
                                    var segmentAudio = floatPool.Rent(segment.AudioSpan.Length);
                                    segment.AudioSpan.CopyTo(segmentAudio);
                                    _audioRecordingManager.AppendPlayerAudio(segmentAudio, audioSpan.Length, clientAudio.ReceivedRadio);
                                    floatPool.Return(segmentAudio);
                                }
                                segment.Dispose();
                            }
                        }
                    }
                    else
                    {
                        Logger.Error(
                            $"Invalid Bytes for Encoding - {_pcmShort.Length} should be {Constants.MIC_SEGMENT_FRAMES} ");
                    }

                    _errorCount = 0;
                }
                catch (Exception ex)
                {
                    _errorCount++;
                    if (_errorCount < 10)
                        Logger.Error(ex, "Error encoding Opus! " + ex.Message);
                    else if (_errorCount == 10) Logger.Error(ex, "Final Log of Error encoding Opus! " + ex.Message);
                }
            }
        }
    }

    private void ShowInputError(string message)
    {
        var audioInputErrorDialog = TaskDialog.ShowDialog(new TaskDialogPage
        {
            Caption = "Audio Input Error",
            Heading = message,
            Text = $"If you are using Windows 10 or above, this could be caused by your privacy settings (make sure to allow apps to access your microphone)." +
                $"\nAlternatively, try a different Input device and please post your client log to the support Discord server.",
            Icon = TaskDialogIcon.Error,
            Buttons =
            {
                new TaskDialogButton
                {
                    Text = "OPEN PRIVACY SETTINGS",
                    Tag = 1
                },
                new TaskDialogButton
                {
                    Text =  "JOIN DISCORD SERVER",
                    Tag = 2
                },
                new TaskDialogButton
                {
                    Text = "CLOSE",
                    Tag = 3
                }
            }
        });

        if (audioInputErrorDialog.Tag is int choice)
        {
            switch (choice)
            {
                case 1:
                    Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone")
                    { UseShellExecute = true });
                    break;
                case 2:
                    Process.Start(new ProcessStartInfo("https://discord.gg/baw7g3t")
                    { UseShellExecute = true });
                    break;
            }
        }
    }

    private void ShowOutputError(string message)
    {
        var audioOutputErrorDialog = TaskDialog.ShowDialog(new TaskDialogPage
        {
            Caption = "Audio Output Error",
            Heading = message,
            Text = "Try a different output device and please post your client log to the support Discord server.",
            Icon = TaskDialogIcon.Error,
            Buttons =
            {
                new TaskDialogButton
                {
                    Text =  "JOIN DISCORD SERVER",
                    Tag = 2
                },
                new TaskDialogButton
                {
                    Text = "CLOSE",
                    Tag = 3
                }
            }
        });

        if (audioOutputErrorDialog.Tag is int choice)
        {
            switch (choice)
            {
                case 2:
                    Process.Start(new ProcessStartInfo("https://discord.gg/baw7g3t")
                    { UseShellExecute = true });
                    break;
            }
        }
    }

    private void InitMixers()
    {
        var stereoWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Constants.OUTPUT_SAMPLE_RATE, 2);
        _finalMixdown =
            new SRSMixingSampleProvider(stereoWaveFormat);
        _finalMixdown.ReadFully = true;

        _radioMixingProvider = new List<RadioMixingProvider>();
        for (var i = 0; i < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length; i++)
        {
            var mix = new RadioMixingProvider(stereoWaveFormat, i);
            _radioMixingProvider.Add(mix);
            _finalMixdown.AddMixerInput(mix);
        }
    }

    private void InitVox()
    {
        if (_voxDectection != null)
        {
            _voxDectection.Dispose();
            _voxDectection = null;
        }

        _voxDectection = new WebRtcVad
        {
            SampleRate = SampleRate.Is16kHz,
            FrameLength = FrameLength.Is20ms,
            OperatingMode = (OperatingMode)_globalSettings.GetClientSettingInt(GlobalSettingsKeys.VOXMode)
        };
    }

    public void StopEncoding()
    {
        lock (lockObj)
        {
            //Stop input handler
            _udpClientAudioProcessor?.Stop();
            _udpClientAudioProcessor = null;

            _wasapiCapture?.StopRecording();
            _wasapiCapture?.Dispose();
            _wasapiCapture = null;

            _voxDectection?.Dispose();
            _voxDectection = null;

            _resampler?.Dispose(true);
            _resampler = null;

            //Debug Wav
            // _afterFileWriter?.Close();
            // _afterFileWriter?.Dispose();
            // _beforeWaveFile?.Close();
            // _beforeWaveFile?.Dispose();

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _micWaveOut?.Stop();
            _micWaveOut?.Dispose();
            _micWaveOut = null;

            _volumeSampleProvider = null;

            if (_radioMixingProvider != null)
                foreach (var mixer in _radioMixingProvider)
                    mixer.RemoveAllMixerInputs();

            _radioMixingProvider = new List<RadioMixingProvider>();

            _finalMixdown?.RemoveAllMixerInputs();
            _finalMixdown = null;

            _clientsBufferedAudio.Clear();

            _encoder?.Dispose();
            _encoder = null;

            _udpVoiceHandler?.RequestStop();
            _udpVoiceHandler = null;

            _speex?.Dispose();
            _speex = null;

            SpeakerMax = -100;
            MicMax = -100;

            AudioRecordingManager.Instance.Stop();

            EventBus.Instance.Unsubscribe(this);
        }
    }

    public void AddClientAudio(ClientAudio audio)
    {
        //sort out effects!

        //16bit PCM Audio
        //TODO: Clean  - remove if we havent received audio in a while?
        // If we have recieved audio, create a new buffered audio and read it
        ClientAudioProvider client = null;
        if (_clientsBufferedAudio.ContainsKey(audio.OriginalClientGuid))
        {
            client = _clientsBufferedAudio[audio.OriginalClientGuid];
        }
        else
        {
            client = new ClientAudioProvider();
            _clientsBufferedAudio[audio.OriginalClientGuid] = client;

            foreach (var mixer in _radioMixingProvider) mixer.AddMixerInput(client);
        }

        client.AddClientAudioSamples(audio);
    }

    private void RemoveClientBuffer(SRClientBase srClient)
    {
        //TODO test this
        ClientAudioProvider clientAudio = null;
        _clientsBufferedAudio.TryRemove(srClient.ClientGuid, out clientAudio);

        if (clientAudio == null) return;

        try
        {
            foreach (var mixer in _radioMixingProvider) mixer.RemoveMixerInput(clientAudio);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing client input");
        }
    }

    private bool DoesFrameContainSpeech(byte[] audioFrame, short[] pcmShort)
    {
        Buffer.BlockCopy(audioFrame, 0, tempBuffferFirst20ms, 0, Constants.MIC_SEGMENT_FRAMES);
        Buffer.BlockCopy(audioFrame, Constants.MIC_SEGMENT_FRAMES, tempBuffferSecond20ms, 0,
            Constants.MIC_SEGMENT_FRAMES);

        var mode = (OperatingMode)_globalSettings.GetClientSettingInt(GlobalSettingsKeys.VOXMode);

        if (_voxDectection.OperatingMode != mode) InitVox();

        //frame size is 40 - this only supports 20
        var voice = _voxDectection.HasSpeech(tempBuffferFirst20ms) || _voxDectection.HasSpeech(tempBuffferSecond20ms);

        if (voice)
        {
            //calculate the RMS and see if we're over it
            //voice run first as it ignores background hums very well
            var rms = VolumeConversionHelper.CalculateRMS(pcmShort);
            var min = _globalSettings.GetClientSettingDouble(GlobalSettingsKeys.VOXMinimumDB);

            return rms > min;
        }

        //no voice so dont bother with RMS
        return false;
    }

    public void PlaySoundEffectStartTransmit(int sendingOn, bool enc, float volume, Modulation modulation)
    {
        _radioMixingProvider[sendingOn]?.PlaySoundEffectStartTransmit(enc, volume, modulation);
    }

    public void PlaySoundEffectEndTransmit(int sendingOn, float radioVolume, Modulation radioModulation)
    {
        _radioMixingProvider[sendingOn]?.PlaySoundEffectEndTransmit(radioVolume, radioModulation);
    }
}