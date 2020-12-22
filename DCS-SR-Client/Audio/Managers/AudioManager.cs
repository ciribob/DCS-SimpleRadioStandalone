using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Easy.MessageHub;
using FragLabs.Audio.Codecs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Compression;
using NAudio.Wave.SampleProviders;
using NLog;
using WPFCustomMessageBox;
using static Ciribob.DCS.SimpleRadio.Standalone.Common.RadioInformation;
using Application = FragLabs.Audio.Codecs.Opus.Application;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers
{
    public class AudioManager
    {
        public static readonly int MIC_SAMPLE_RATE = 16000;

        public static readonly int MIC_INPUT_AUDIO_LENGTH_MS = 40;

        public static readonly int MIC_SEGMENT_FRAMES = (MIC_SAMPLE_RATE / 1000) * MIC_INPUT_AUDIO_LENGTH_MS;

        public static readonly int OUTPUT_SAMPLE_RATE = 48000;

        public static readonly int OUTPUT_AUDIO_LENGTH_MS = 40;

        public static readonly int OUTPUT_SEGMENT_FRAMES = (OUTPUT_SAMPLE_RATE / 1000) * OUTPUT_AUDIO_LENGTH_MS;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly CachedAudioEffectProvider _cachedAudioEffectsProvider;

        private readonly ConcurrentDictionary<string, ClientAudioProvider> _clientsBufferedAudio =
            new ConcurrentDictionary<string, ClientAudioProvider>();

        private MixingSampleProvider _clientAudioMixer;

        //buffer for effects
        //plays in parallel with radio output buffer
        private RadioAudioProvider[] _effectsOutputBuffer;

        private OpusEncoder _encoder;

        private readonly Queue<short> _micInputQueue = new Queue<short>(MIC_SEGMENT_FRAMES * 3);

        private float _speakerBoost = 1.0f;
        private UdpVoiceHandler _udpVoiceHandler;
        private VolumeSampleProviderWithPeak _volumeSampleProvider;

        private WasapiCapture _wasapiCapture;
        private WasapiOut _waveOut;
        private EventDrivenResampler _resampler;

        public float MicMax { get; set; } = -100;
        public float SpeakerMax { get; set; } = -100;

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;
        private readonly AudioOutputSingleton _audioOutputSingleton = AudioOutputSingleton.Instance;

        private WasapiOut _micWaveOut;
        private BufferedWaveProvider _micWaveOutBuffer;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private Preprocessor _speex;
        private readonly bool windowsN;

        private ClientPassThroughAudioProvider _passThroughAudioProvider;

        public AudioManager(bool windowsN)
        {
            this.windowsN = windowsN;

            _cachedAudioEffectsProvider = CachedAudioEffectProvider.Instance;
        }

        public float MicBoost { get; set; } = 1.0f;

        public float SpeakerBoost
        {
            get { return _speakerBoost; }
            set
            {
                _speakerBoost = value;
                if (_volumeSampleProvider != null)
                {
                    _volumeSampleProvider.Volume = value;
                }
            }
        }

        public void StartEncoding(string guid, InputDeviceManager inputManager,
            IPAddress ipAddress, int port)
        {

            MMDevice speakers = null;
            if (_audioOutputSingleton.SelectedAudioOutput.Value == null)
            {
                speakers = WasapiOut.GetDefaultAudioEndpoint();
            }
            else 
            {
                speakers = _audioOutputSingleton.SelectedAudioOutput.Value;
            }

            MMDevice micOutput = null;
            if (_audioOutputSingleton.SelectedMicAudioOutput.Value != null)
            {
                micOutput = _audioOutputSingleton.SelectedMicAudioOutput.Value;
            }

            try
            {
                _micInputQueue.Clear();

                InitMixers();

                InitAudioBuffers();

                //Audio manager should start / stop and cleanup based on connection successfull and disconnect
                //Should use listeners to synchronise all the state

                _waveOut = new WasapiOut(speakers, AudioClientShareMode.Shared, true, 40,windowsN);

                //add final volume boost to all mixed audio
                _volumeSampleProvider = new VolumeSampleProviderWithPeak(_clientAudioMixer,
                    (peak => SpeakerMax = (float) VolumeConversionHelper.ConvertFloatToDB(peak)));
                _volumeSampleProvider.Volume = SpeakerBoost;

                if (speakers.AudioClient.MixFormat.Channels == 1)
                {
                    if (_volumeSampleProvider.WaveFormat.Channels == 2)
                    {
                        _waveOut.Init(_volumeSampleProvider.ToMono());
                    }
                    else
                    {
                        //already mono
                        _waveOut.Init(_volumeSampleProvider);
                    }
                }
                else
                {
                    if (_volumeSampleProvider.WaveFormat.Channels == 1)
                    {
                        _waveOut.Init(_volumeSampleProvider.ToStereo());
                    }
                    else
                    {
                        //already stereo
                        _waveOut.Init(_volumeSampleProvider);
                    }
                }
                _waveOut.Play();

                //opus
                _encoder = OpusEncoder.Create(MIC_SAMPLE_RATE, 1, Application.Voip);
                _encoder.ForwardErrorCorrection = false;

                //speex
                _speex = new Preprocessor(AudioManager.MIC_SEGMENT_FRAMES, AudioManager.MIC_SAMPLE_RATE);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Output - Quitting! " + ex.Message);


                ShowOutputError("Problem Initialising Audio Output!");


                Environment.Exit(1);
            }

            if (micOutput != null) // && micOutput !=speakers
            {
                //TODO handle case when they're the same?

                try
                {
                    _passThroughAudioProvider = new ClientPassThroughAudioProvider();
                    _micWaveOut = new WasapiOut(micOutput, AudioClientShareMode.Shared, true, 40,windowsN);

                    _micWaveOutBuffer = new BufferedWaveProvider(new WaveFormat(AudioManager.MIC_SAMPLE_RATE, 16, 1));
                    _micWaveOutBuffer.ReadFully = true;
                    _micWaveOutBuffer.DiscardOnBufferOverflow = true;

                    var sampleProvider = _micWaveOutBuffer.ToSampleProvider();

                    if (micOutput.AudioClient.MixFormat.Channels == 1)
                    {
                        if (sampleProvider.WaveFormat.Channels == 2)
                        {
                            _micWaveOut.Init(sampleProvider.ToMono());
                        }
                        else
                        {
                            //already mono
                            _micWaveOut.Init(sampleProvider);
                        }
                    }
                    else
                    {
                        if (sampleProvider.WaveFormat.Channels == 1)
                        {
                            _micWaveOut.Init(sampleProvider.ToStereo());
                        }
                        else
                        {
                            //already stereo
                            _micWaveOut.Init(sampleProvider);
                        }
                    }

                    _micWaveOut.Play();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error starting mic audio Output - Quitting! " + ex.Message);

                    ShowOutputError("Problem Initialising Mic Audio Output!");


                    Environment.Exit(1);
                }
            }

            if (_audioInputSingleton.MicrophoneAvailable)
            {
                try
                {
                    var device = (MMDevice) _audioInputSingleton.SelectedAudioInput.Value;

                    if (device == null)
                    {
                        device = WasapiCapture.GetDefaultCaptureDevice();
                    }

                    device.AudioEndpointVolume.Mute = false;

                    _wasapiCapture = new WasapiCapture(device, true);
                    _wasapiCapture.ShareMode = AudioClientShareMode.Shared;
                    _wasapiCapture.DataAvailable += WasapiCaptureOnDataAvailable;
                    _wasapiCapture.RecordingStopped += WasapiCaptureOnRecordingStopped;

                    _udpVoiceHandler =
                        new UdpVoiceHandler(guid, ipAddress, port, this, inputManager);
                    var voiceSenderThread = new Thread(_udpVoiceHandler.Listen);

                    voiceSenderThread.Start();

                    _wasapiCapture.StartRecording();

                    MessageHub.Instance.Subscribe<SRClient>(RemoveClientBuffer);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error starting audio Input - Quitting! " + ex.Message);

                    ShowInputError("Problem initialising Audio Input!");

                    Environment.Exit(1);
                }
            }
            else
            {
                //no mic....
                _udpVoiceHandler =
                    new UdpVoiceHandler(guid, ipAddress, port, this, inputManager);
                MessageHub.Instance.Subscribe<SRClient>(RemoveClientBuffer);
                var voiceSenderThread = new Thread(_udpVoiceHandler.Listen);
                voiceSenderThread.Start();
            }
        }

        private void WasapiCaptureOnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Logger.Error("Recording Stopped");
        }
        
        Stopwatch _stopwatch = new Stopwatch();
       
        private void WasapiCaptureOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_resampler == null)
            {
                //create and use in the same thread or COM issues
                _resampler = new EventDrivenResampler(windowsN, _wasapiCapture.WaveFormat, new WaveFormat(AudioManager.MIC_SAMPLE_RATE, 16, 1));
            }

            if (e.BytesRecorded > 0)
            {
                //Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Bytes: {e.BytesRecorded}");
                short[] resampledPCM16Bit = _resampler.Resample(e.Buffer, e.BytesRecorded);

                // Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Bytes: {resampledPCM16Bit.Length}");
                //fill sound buffer
                for (var i = 0; i < resampledPCM16Bit.Length; i++)
                {
                    _micInputQueue.Enqueue(resampledPCM16Bit[i]);
                }

                //read out the queue
                while (_micInputQueue.Count >= AudioManager.MIC_SEGMENT_FRAMES)
                {
                    short[] pcmShort  = new short[AudioManager.MIC_SEGMENT_FRAMES];

                    for (var i = 0; i < AudioManager.MIC_SEGMENT_FRAMES; i++)
                    {
                        pcmShort[i] = _micInputQueue.Dequeue();
                    }

                    try
                    {
                        //volume boost pre
                        for (var i = 0; i < pcmShort.Length; i++)
                        {
                            // n.b. no clipping test going on here
                            pcmShort[i] = (short)(pcmShort[i] * MicBoost);
                        }

                        //process with Speex
                        _speex.Process(new ArraySegment<short>(pcmShort));

                        float max = 0;
                        for (var i = 0; i < pcmShort.Length; i++)
                        {
                            //determine peak
                            if (pcmShort[i] > max)
                            {
                                max = pcmShort[i];
                            }
                        }
                        //convert to dB
                        MicMax = (float)VolumeConversionHelper.ConvertFloatToDB(max / 32768F);

                        var pcmBytes = new byte[pcmShort.Length * 2];
                        Buffer.BlockCopy(pcmShort, 0, pcmBytes, 0, pcmBytes.Length);

                        //encode as opus bytes
                        int len;
                        var buff = _encoder.Encode(pcmBytes, pcmBytes.Length, out len);

                        if ((_udpVoiceHandler != null) && (buff != null) && (len > 0))
                        {
                            //create copy with small buffer
                            var encoded = new byte[len];

                            Buffer.BlockCopy(buff, 0, encoded, 0, len);

                            // Console.WriteLine("Sending: " + e.BytesRecorded);
                            var clientAudio = _udpVoiceHandler.Send(encoded, len);
                            if (clientAudio != null && _micWaveOutBuffer != null)
                            {
                                //send audio so play over local too

                                var passThroughPCMBytes  = _passThroughAudioProvider.AddClientAudioSamples(clientAudio, pcmShort);
                                //process bytes and add effects
                                _micWaveOutBuffer?.AddSamples(passThroughPCMBytes, 0, passThroughPCMBytes.Length);
                            }
                        }
                        else
                        {
                            Logger.Error($"Invalid Bytes for Encoding - {pcmShort.Length} should be {MIC_SEGMENT_FRAMES} ");
                        }

                        _errorCount = 0;
                    }
                    catch (Exception ex)
                    {
                        _errorCount++;
                        if (_errorCount < 10)
                        {
                            Logger.Error(ex, "Error encoding Opus! " + ex.Message);
                        }
                        else if (_errorCount == 10)
                        {
                            Logger.Error(ex, "Final Log of Error encoding Opus! " + ex.Message);
                        }

                    }
                }
            }
        }

        private void ShowInputError(string message)
        {
            if (Environment.OSVersion.Version.Major == 10)
            {
                var messageBoxResult = CustomMessageBox.ShowYesNoCancel(
                    $"{message}\n\n" +
                    $"If you are using Windows 10, this could be caused by your privacy settings (make sure to allow apps to access your microphone)." +
                    $"\nAlternatively, try a different Input device and please post your client log to the support Discord server.",
                    "Audio Input Error",
                    "OPEN PRIVACY SETTINGS",
                    "JOIN DISCORD SERVER",
                    "CLOSE",
                    MessageBoxImage.Error);

                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    Process.Start("ms-settings:privacy-microphone");
                }
                else if (messageBoxResult == MessageBoxResult.No)
                {
                    Process.Start("https://discord.gg/baw7g3t");
                }
            }
            else
            {
                var messageBoxResult = CustomMessageBox.ShowYesNo(
                    $"{message}\n\n" +
                    "Try a different Input device and please post your client log to the support Discord server.",
                    "Audio Input Error",
                    "JOIN DISCORD SERVER",
                    "CLOSE",
                    MessageBoxImage.Error);

                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    Process.Start("https://discord.gg/baw7g3t");
                }
            }
        }

        private void ShowOutputError(string message)
        {
            var messageBoxResult = CustomMessageBox.ShowYesNo(
                $"{message}\n\n" +
                "Try a different output device and please post your client log to the support Discord server.",
                "Audio Output Error",
                "JOIN DISCORD SERVER",
                "CLOSE",
                MessageBoxImage.Error);

            if (messageBoxResult == MessageBoxResult.Yes)
            {
                Process.Start("https://discord.gg/baw7g3t");
            }
        }

        private void InitMixers()
        {
            _clientAudioMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(OUTPUT_SAMPLE_RATE, 2));
            _clientAudioMixer.ReadFully = true;
        }

        private void InitAudioBuffers()
        {
            _effectsOutputBuffer = new RadioAudioProvider[_clientStateSingleton.DcsPlayerRadioInfo.radios.Length];

            for (var i = 0; i < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length; i++)
            {
                _effectsOutputBuffer[i] = new RadioAudioProvider(OUTPUT_SAMPLE_RATE);
                _clientAudioMixer.AddMixerInput(_effectsOutputBuffer[i].VolumeSampleProvider);
            }
        }


        public void PlaySoundEffectStartReceive(int transmitOnRadio, bool encrypted, float volume, Modulation modulation)
        {
            if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start))
            {
                return;
            }

            bool midsTone = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

            if (modulation == Modulation.MIDS && midsTone)
            {
                //no tone for MIDS
                return;
            }

            var _effectsBuffer = _effectsOutputBuffer[transmitOnRadio];

            if (encrypted && (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects)))
            {
                _effectsBuffer.VolumeSampleProvider.Volume = volume;

                var effect = _cachedAudioEffectsProvider.KY58EncryptionEndTone;
                if (effect.Loaded)
                {
                    _effectsBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
            else
            {
                _effectsBuffer.VolumeSampleProvider.Volume = volume;

                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionStartEffect;
                if (effect.Loaded)
                {
                    _effectsBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
        }

        public void PlaySoundEffectStartTransmit(int transmitOnRadio, bool encrypted, float volume, Modulation modulation)
        {
            if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start))
            {
                return;
            }

            var _effectBuffer = _effectsOutputBuffer[transmitOnRadio];

            bool midsTone = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

            if (encrypted && (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects)))
            {
                _effectBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.KY58EncryptionTransmitTone;
                if (effect.Loaded)
                {
                    _effectBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
            else if (modulation == Modulation.MIDS && midsTone)
            {
                _effectBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.MIDSTransmitTone;
                if (effect.Loaded)
                {
                    _effectBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
            else
            {
                _effectBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionStartEffect;
                if (effect.Loaded)
                {
                    _effectBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
        }


        public void PlaySoundEffectEndReceive(int transmitOnRadio, float volume, Modulation modulation)
        {
            
            if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End))
            {
                return;
            }

            bool midsTone = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

            if (modulation == Modulation.MIDS && midsTone)
            {
                //end receive tone for MIDS
                var effectsBuffer = _effectsOutputBuffer[transmitOnRadio];

                effectsBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.MIDSEndTone;
                if (effect.Loaded)
                {
                    effectsBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
            else
            {
                var effectsBuffer = _effectsOutputBuffer[transmitOnRadio];

                effectsBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionEndEffect;
                if (effect.Loaded)
                {
                    effectsBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }

        }

        public void PlaySoundEffectEndTransmit(int transmitOnRadio, float volume, Modulation modulation)
        {
            if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_End))
            {
                return;
            }

            bool midsTone = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);
            var _effectBuffer = _effectsOutputBuffer[transmitOnRadio];

            if (modulation == Modulation.MIDS && midsTone)
            {
                _effectBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.MIDSEndTone;
                if (effect.Loaded)
                {
                    _effectBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }
            else{

                _effectBuffer.VolumeSampleProvider.Volume = volume;
                var effect = _cachedAudioEffectsProvider.SelectedRadioTransmissionEndEffect;
                if (effect.Loaded)
                {
                    _effectBuffer.AddAudioSamples(
                        effect.AudioEffectBytes,
                        transmitOnRadio);
                }
            }

         
        }

        private int _errorCount = 0;
        //Stopwatch _stopwatch = new Stopwatch();

        object lockObj = new object();
        public void StopEncoding()
        {
            lock(lockObj)
            {
                _wasapiCapture?.StopRecording();
                _wasapiCapture?.Dispose();
                _wasapiCapture = null;

                _resampler?.Dispose(true);
                _resampler = null;

                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;

                _micWaveOut?.Stop();
                _micWaveOut?.Dispose();
                _micWaveOut = null;

                _volumeSampleProvider = null;
                _clientAudioMixer?.RemoveAllMixerInputs();
                _clientAudioMixer = null;

                _clientsBufferedAudio.Clear();

                _encoder?.Dispose();
                _encoder = null;

                if (_udpVoiceHandler != null)
                {
                    _udpVoiceHandler.RequestStop();
                    _udpVoiceHandler = null;
                }

                _speex?.Dispose();
                _speex = null;

                SpeakerMax = -100;
                MicMax = -100;

                _effectsOutputBuffer = null;

                MessageHub.Instance.ClearSubscriptions();
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

                _clientAudioMixer.AddMixerInput(client.SampleProvider);
            }

            client.AddClientAudioSamples(audio);
        }

        private void RemoveClientBuffer(SRClient srClient)
        {
            ClientAudioProvider clientAudio = null;
            _clientsBufferedAudio.TryRemove(srClient.ClientGuid, out clientAudio);

            if (clientAudio == null)
            {
                return;
            }

            try
            {
                _clientAudioMixer.RemoveMixerInput(clientAudio.SampleProvider);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error removing client input");
            }
        }
    }
}