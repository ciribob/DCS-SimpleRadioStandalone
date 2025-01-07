using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Recording;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Easy.MessageHub;
using FragLabs.Audio.Codecs;
using NAudio.CoreAudioApi;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using WebRtcVadSharp;
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

        //TEMP
        private List<RadioMixingProvider> _radioMixingProvider;

        private MixingSampleProvider _finalMixdown;


        private OpusEncoder _encoder;

        private readonly Queue<short> _micInputQueue = new Queue<short>(MIC_SEGMENT_FRAMES * 3);

        //buffers intialised once for use repeatedly
        short[] _pcmShort = new short[AudioManager.MIC_SEGMENT_FRAMES];
        byte[] _pcmBytes = new byte[AudioManager.MIC_SEGMENT_FRAMES * 2];

        private byte[] _tempMicOutputBuffer = null;

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
        private readonly AudioRecordingManager _audioRecordingManager = AudioRecordingManager.Instance;

        private WebRtcVad _voxDectection;

        private WasapiOut _micWaveOut;
        private BufferedWaveProvider _micWaveOutBuffer;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private Preprocessor _speex;
        private readonly bool windowsN;

        private ClientAudioProvider _passThroughAudioProvider;

        private ClientEffectsPipeline _clientEffectsPipeline;

        private string _guid;

        public AudioManager(bool windowsN)
        {
            this.windowsN = windowsN;

            _cachedAudioEffectsProvider = CachedAudioEffectProvider.Instance;
            _clientEffectsPipeline = new ClientEffectsPipeline();

            //_beforeWaveFile = new WaveFileWriter(@"C:\Temp\Test-Preview-Before.wav", new WaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 32, 1));
        }

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
            guid = ClientStateSingleton.Instance.ShortGUID;

            MMDevice speakers = null;
            if (_audioOutputSingleton.SelectedAudioOutput.Value != null)
            {
                speakers = (MMDevice)_audioOutputSingleton.SelectedAudioOutput.Value;
            }

            MMDevice micOutput = null;
            if (_audioOutputSingleton.SelectedMicAudioOutput.Value != null)
            {
                micOutput = (MMDevice)_audioOutputSingleton.SelectedMicAudioOutput.Value;
            }

            try
            {
                _micInputQueue.Clear();

                InitMixers();

                InitVox();

                AudioRecordingManager.Instance.Start();

                //Audio manager should start / stop and cleanup based on connection successfull and disconnect
                //Should use listeners to synchronise all the state

                _waveOut = speakers != null ? new WasapiOut(speakers, AudioClientShareMode.Shared, true, 40) : new WasapiOut(AudioClientShareMode.Shared, 40);

                //add final volume boost to all mixed audio
                _volumeSampleProvider = new VolumeSampleProviderWithPeak(_finalMixdown,
                    (peak => SpeakerMax = peak));
                _volumeSampleProvider.Volume = SpeakerBoost;

                

                if (_waveOut.OutputWaveFormat.Channels == 1)
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

            _passThroughAudioProvider = new ClientAudioProvider(true);

            if (micOutput != null) // && micOutput !=speakers
            {
                try
                {
                    _micWaveOut = new WasapiOut(micOutput, AudioClientShareMode.Shared, true, 40);

                    _micWaveOutBuffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(OUTPUT_SAMPLE_RATE, 1));
                    _micWaveOutBuffer.ReadFully = true;
                    _micWaveOutBuffer.DiscardOnBufferOverflow = true;

                    var sampleProvider = _micWaveOutBuffer.ToSampleProvider();

                    if (_micWaveOut.OutputWaveFormat.Channels == 1)
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
        // private WaveFileWriter _beforeWaveFile;
        // private WaveFileWriter _afterFileWriter;


        private void WasapiCaptureOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_resampler == null)
            {
                //create and use in the same thread or COM issues
                _resampler = new EventDrivenResampler(windowsN, _wasapiCapture.WaveFormat, new WaveFormat(AudioManager.MIC_SAMPLE_RATE, 16, 1));
                 
                // _afterFileWriter = new WaveFileWriter(@"C:\Temp\Test-Preview-after.wav", new WaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 16, 1));
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

                    for (var i = 0; i < AudioManager.MIC_SEGMENT_FRAMES; i++)
                    {
                        _pcmShort[i] = _micInputQueue.Dequeue();
                    }

                    try
                    {
                        //ready for the buffer shortly
                        
                        //check for voice before any pre-processing
                        bool voice = true;

                        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.VOX))
                        {
                            Buffer.BlockCopy(_pcmShort, 0, _pcmBytes, 0, _pcmBytes.Length);
                            voice = DoesFrameContainSpeech(_pcmBytes, _pcmShort);
                        }
                        
                        //process with Speex
                        _speex.Process(new ArraySegment<short>(_pcmShort));

                        float max = 0;
                        for (var i = 0; i < _pcmShort.Length; i++)
                        {
                            //determine peak
                            if (_pcmShort[i] > max)
                            {
                                max = _pcmShort[i];
                            }
                        }

                        //convert to dB
                        MicMax = (float)VolumeConversionHelper.CalculateRMS(_pcmShort);

                        //copy and overwrite with new PCM data post processing
                        Buffer.BlockCopy(_pcmShort, 0, _pcmBytes, 0, _pcmBytes.Length);

                        //encode as opus bytes
                        int len;
                        var buff = _encoder.Encode(_pcmBytes, _pcmBytes.Length, out len);

                        if ((_udpVoiceHandler != null) && (buff != null) && (len > 0))
                        {
                            //create copy with small buffer
                            var encoded = new byte[len];

                            Buffer.BlockCopy(buff, 0, encoded, 0, len);

                            // Console.WriteLine("Sending: " + e.BytesRecorded);
                            var clientAudio = _udpVoiceHandler.Send(encoded, len, voice);                         

                            // _beforeWaveFile.Write(pcmBytes, 0, pcmBytes.Length);

                            if (clientAudio != null && (_micWaveOutBuffer != null 
                                                        || GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RecordAudio)))
                            {

                                //todo see if we can fix the resample / opus decode
                                //send audio so play over local too
                                var jitterBufferAudio = _passThroughAudioProvider?.AddClientAudioSamples(clientAudio);
                                
                                // //process bytes and add effects
                                if (jitterBufferAudio!=null)
                                {
                                    DeJitteredTransmission deJittered =  new DeJitteredTransmission()
                                    {
                                        PCMAudioLength = jitterBufferAudio.Audio.Length,
                                        Modulation = jitterBufferAudio.Modulation,
                                        Volume = jitterBufferAudio.Volume,
                                        Decryptable = true,
                                        Frequency = jitterBufferAudio.Frequency,
                                        IsSecondary = jitterBufferAudio.IsSecondary,
                                        NoAudioEffects = jitterBufferAudio.NoAudioEffects,
                                        ReceivedRadio = jitterBufferAudio.ReceivedRadio,
                                        PCMMonoAudio = jitterBufferAudio.Audio,
                                        Guid = _guid,
                                        OriginalClientGuid = _guid
                                    };

                                    //process audio
                                    float[] tempFloat = _clientEffectsPipeline.ProcessClientAudioSamples(jitterBufferAudio.Audio,
                                        jitterBufferAudio.Audio.Length, 0, deJittered);

                                    if (_micWaveOut != null)
                                    {
                                       
                                        //now its a processed Mono audio
                                        _tempMicOutputBuffer = BufferHelpers.Ensure(_tempMicOutputBuffer, tempFloat.Length * 4);
                                        Buffer.BlockCopy(tempFloat, 0, _tempMicOutputBuffer, 0, tempFloat.Length * 4);

                                        //_beforeWaveFile?.WriteSamples(jitterBufferAudio.Audio,0,jitterBufferAudio.Audio.Length);
                                        //_beforeWaveFile?.Write(pcm32, 0, pcm32.Length);
                                        //_beforeWaveFile?.Flush();

                                        _micWaveOutBuffer.AddSamples(_tempMicOutputBuffer, 0, tempFloat.Length * 4);
                                    }

                                    if (GlobalSettingsStore.Instance.GetClientSettingBool(
                                        GlobalSettingsKeys.RecordAudio))
                                    {
                                        ///TODO cache this to avoid the contant lookup
                                        _audioRecordingManager.AppendPlayerAudio(tempFloat, jitterBufferAudio.ReceivedRadio);
                                    }
                                   
                                }
                            }
                         
                        }
                        else
                        {
                            Logger.Error($"Invalid Bytes for Encoding - {_pcmShort.Length} should be {MIC_SEGMENT_FRAMES} ");
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
            _finalMixdown = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(OUTPUT_SAMPLE_RATE, 2));
            _finalMixdown.ReadFully = true;

            _radioMixingProvider = new List<RadioMixingProvider>();
            for (int i = 0; i < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length; i++)
            {
                var mix = new RadioMixingProvider(WaveFormat.CreateIeeeFloatWaveFormat(OUTPUT_SAMPLE_RATE, 2), i);
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

                if(_radioMixingProvider!=null)
                    foreach (var mixer in _radioMixingProvider)
                    {
                        mixer.RemoveAllMixerInputs();
                    }

                _radioMixingProvider = new List<RadioMixingProvider>();

                _finalMixdown?.RemoveAllMixerInputs();
                _finalMixdown = null;

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

                AudioRecordingManager.Instance.Stop();

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

                foreach (var mixer in _radioMixingProvider)
                {
                    mixer.AddMixerInput(client);
                }
               
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
                foreach (var mixer in _radioMixingProvider)
                {
                    mixer.RemoveMixerInput(clientAudio);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error removing client input");
            }
        }

        //MIC SEGMENT FRAMES IS SHORTS not bytes - which is two bytes
        //however we only want half of a frame IN BYTES not short - so its MIC_SEGMENT_FRAMES *2 (for bytes) then / 2 for bytes again
        //declare here to save on garbage collection
        byte[] tempBuffferFirst20ms = new byte[MIC_SEGMENT_FRAMES];
        byte[] tempBuffferSecond20ms = new byte[MIC_SEGMENT_FRAMES];
        bool DoesFrameContainSpeech(byte[] audioFrame, short[] pcmShort)
        {
            Buffer.BlockCopy(audioFrame,0,tempBuffferFirst20ms,0, MIC_SEGMENT_FRAMES);
            Buffer.BlockCopy(audioFrame, MIC_SEGMENT_FRAMES, tempBuffferSecond20ms, 0, MIC_SEGMENT_FRAMES);

            OperatingMode mode = (OperatingMode)_globalSettings.GetClientSettingInt(GlobalSettingsKeys.VOXMode);

            if (_voxDectection.OperatingMode != mode)
            {
                InitVox();
            }

            //frame size is 40 - this only supports 20
            bool voice = _voxDectection.HasSpeech(tempBuffferFirst20ms) || _voxDectection.HasSpeech(tempBuffferSecond20ms);

            if (voice)
            {
                //calculate the RMS and see if we're over it
                //voice run first as it ignores background hums very well
                double rms = VolumeConversionHelper.CalculateRMS(pcmShort);
                double min = _globalSettings.GetClientSettingDouble(GlobalSettingsKeys.VOXMinimumDB);

                return rms > min;
            }
            //no voice so dont bother with RMS
            return false;
        }

        public void PlaySoundEffectStartTransmit(int sendingOn, bool enc, float volume, Modulation modulation)
        {
            _radioMixingProvider[sendingOn]?.PlaySoundEffectStartTransmit(enc,volume,modulation);
        }

        public void PlaySoundEffectEndTransmit(int sendingOn, float radioVolume, Modulation radioModulation)
        {
            _radioMixingProvider[sendingOn]?.PlaySoundEffectEndTransmit(radioVolume,radioModulation);
        }
    }
}