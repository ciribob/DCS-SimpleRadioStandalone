using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using FragLabs.Audio.Codecs;
using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using WPFCustomMessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio
{
    internal class AudioPreview
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private BufferedWaveProvider _playBuffer;
            
        private WasapiCapture _wasapiCapture;
        private WasapiOut _waveOut;
        private EventDrivenResampler _resampler;
        //private readonly CircularBuffer _circularBuffer = new CircularBuffer();

        private VolumeSampleProviderWithPeak _volumeSampleProvider;
        private BufferedWaveProvider _buffBufferedWaveProvider;

        private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;
        private readonly AudioOutputSingleton _audioOutputSingleton = AudioOutputSingleton.Instance;

        public float MicBoost { get; set; } = 1.0f;

        private float _speakerBoost = 1.0f;
        private OpusEncoder _encoder;
        private OpusDecoder _decoder;

        private Preprocessor _speex;

        private readonly Queue<short> _micInputQueue = new Queue<short>(AudioManager.MIC_SEGMENT_FRAMES * 3);
        
        private WaveFileWriter _waveFile;

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

        public float MicMax { get; set; } = -100;
        public float SpeakerMax { get; set; } = -100;

        private bool windowsN;

        public void StartPreview( bool windowsN)
        {
            this.windowsN = windowsN;
            try
            {

                MMDevice speakers = null;
                if (_audioOutputSingleton.SelectedAudioOutput.Value != null)
                {
                    speakers = (MMDevice)_audioOutputSingleton.SelectedAudioOutput.Value;
                }

                _waveOut = speakers != null? new WasapiOut(speakers, AudioClientShareMode.Shared, true, 80) : new WasapiOut(AudioClientShareMode.Shared, true, 80);

                _buffBufferedWaveProvider =
                    new BufferedWaveProvider(new WaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 16, 1));
                _buffBufferedWaveProvider.ReadFully = true;
                _buffBufferedWaveProvider.DiscardOnBufferOverflow = true;

                RadioFilter filter = new RadioFilter(_buffBufferedWaveProvider.ToSampleProvider());

                //add final volume boost to all mixed audio
                _volumeSampleProvider = new VolumeSampleProviderWithPeak(filter,
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
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Output - Quitting! " + ex.Message);

                ShowOutputError("Problem Initialising Audio Output!");

                Environment.Exit(1);
            }

            try
            {
                _speex = new Preprocessor(AudioManager.MIC_SEGMENT_FRAMES, AudioManager.MIC_SAMPLE_RATE);
                //opus
                _encoder = OpusEncoder.Create(AudioManager.MIC_SAMPLE_RATE, 1,
                    FragLabs.Audio.Codecs.Opus.Application.Voip);
                _encoder.ForwardErrorCorrection = false;
                _decoder = OpusDecoder.Create(AudioManager.OUTPUT_SAMPLE_RATE, 1);
                _decoder.ForwardErrorCorrection = false;
                _decoder.MaxDataBytes = AudioManager.OUTPUT_SAMPLE_RATE * 4;

                var device = (MMDevice)_audioInputSingleton.SelectedAudioInput.Value;

                if (device == null)
                {
                    device = WasapiCapture.GetDefaultCaptureDevice();
                }

                device.AudioEndpointVolume.Mute = false;

                _wasapiCapture = new WasapiCapture(device, true);
                _wasapiCapture.ShareMode = AudioClientShareMode.Shared;
                _wasapiCapture.DataAvailable += WasapiCaptureOnDataAvailable;
                _wasapiCapture.RecordingStopped += WasapiCaptureOnRecordingStopped;

                //debug wave file
          //      _waveFile = new WaveFileWriter(@"C:\Temp\Test-Preview.wav", new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 16, 1));

                _wasapiCapture.StartRecording();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Input - Quitting! " + ex.Message);
                ShowInputError();

                Environment.Exit(1);
            }
        }

        private void ShowInputError()
        {
            if (Environment.OSVersion.Version.Major == 10)
            {
                var messageBoxResult = CustomMessageBox.ShowYesNoCancel(
                    "Problem initialising Audio Input!\n\nIf you are using Windows 10, this could be caused by your privacy settings (make sure to allow apps to access your microphone).\nAlternatively, try a different Input device and please post your client log to the support Discord server.",
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
                    "Problem initialising Audio Input!\n\nTry a different Input device and please post your client log to the support Discord server.",
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

        private void WasapiCaptureOnRecordingStopped(object sender, StoppedEventArgs e)
        {
            Logger.Error("Recording Stopped");
        }

        //Stopwatch _stopwatch = new Stopwatch();

        private void WasapiCaptureOnDataAvailable(object sender, WaveInEventArgs e)
        {

            if (_resampler == null)
            {
                _resampler = new EventDrivenResampler(windowsN, _wasapiCapture.WaveFormat, new WaveFormat(AudioManager.MIC_SAMPLE_RATE, 16, 1));
            }

            if (e.BytesRecorded > 0)
            {
               //Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Bytes: {e.BytesRecorded}");
                short[] resampledPCM16Bit = _resampler.Resample(e.Buffer, e.BytesRecorded);

               // Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Bytes: {resampledPCM16Bit.Length}");
              

                //fill sound buffer

                short[] pcmShort = null;

                for (var i = 0; i < resampledPCM16Bit.Length; i++)
                {
                    _micInputQueue.Enqueue(resampledPCM16Bit[i]);
                }
                
                //read out the queue
                while ((pcmShort != null) || (_micInputQueue.Count >= AudioManager.MIC_SEGMENT_FRAMES))
                {
                    //null sound buffer so read from the queue
                    if (pcmShort == null)
                    {
                        pcmShort = new short[AudioManager.MIC_SEGMENT_FRAMES];

                        for (var i = 0; i < AudioManager.MIC_SEGMENT_FRAMES; i++)
                        {
                            pcmShort[i] = _micInputQueue.Dequeue();
                        }
                    }

                    try
                    {
                        //process with Speex
                        _speex.Process(new ArraySegment<short>(pcmShort));

                        double rms = VolumeConversionHelper.CalculateRMS(pcmShort);

                        //convert to dB
                        MicMax = (float)rms;

                        var pcmBytes = new byte[pcmShort.Length * 2];
                        Buffer.BlockCopy(pcmShort, 0, pcmBytes, 0, pcmBytes.Length);

                        //                 _buffBufferedWaveProvider.AddSamples(pcmBytes, 0, pcmBytes.Length);
                        //encode as opus bytes
                        int len;
                        //need to get framing right for opus -
                        var buff = _encoder.Encode(pcmBytes, pcmBytes.Length, out len);

                        if ((buff != null) && (len > 0))
                        {
                            //create copy with small buffer
                            var encoded = new byte[len];

                            Buffer.BlockCopy(buff, 0, encoded, 0, len);

                            var decodedLength = 0;
                            //now decode
                            var decodedBytes = _decoder.Decode(encoded, len, out decodedLength);

                            _buffBufferedWaveProvider.AddSamples(decodedBytes, 0, decodedLength);

                //            Logger.Info($"Time: {_stopwatch.ElapsedMilliseconds} - Added samples");


                        }
                        else
                        {
                            Logger.Error(
                                $"Invalid Bytes for Encoding - {e.BytesRecorded} should be {AudioManager.MIC_SEGMENT_FRAMES} ");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error encoding Opus! " + ex.Message);
                    }

                    pcmShort = null;
                }
            }

          //   _stopwatch.Restart();
        }

        object lockob = new object();

        public void StopEncoding()
        {
            lock(lockob)
            {
                _wasapiCapture?.StopRecording();
                _wasapiCapture?.Dispose();
                _wasapiCapture = null;

                _resampler?.Dispose(true);
                _resampler = null;

                _waveOut?.Dispose();
                _waveOut = null;

                _playBuffer?.ClearBuffer();
                _playBuffer = null;

                _encoder?.Dispose();
                _encoder = null;

                _decoder?.Dispose();
                _decoder = null;

                _playBuffer?.ClearBuffer();
                _playBuffer = null;

                _speex?.Dispose();
                _speex = null;

                _waveFile?.Flush();
                _waveFile?.Dispose();
                _waveFile = null;

                SpeakerMax = -100;
                MicMax = -100;
            }
           
        }
    }
}