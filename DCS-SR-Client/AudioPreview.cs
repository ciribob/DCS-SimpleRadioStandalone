using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    internal class AudioPreview
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private BufferedWaveProvider _playBuffer;
        private WaveIn _waveIn;
        private WaveOut _waveOut;

        public SampleChannel Volume { get; private set; }


        public void StartPreview(int mic, int speakers)
        {
            try
            {
                _waveIn = new WaveIn(WaveCallbackInfo.FunctionCallback())
                {
                    BufferMilliseconds = 80,
                    DeviceNumber = mic,
                    WaveFormat = new WaveFormat(8000, 16, 1)
                };

                var pcm = new WaveInProvider(_waveIn);


                Volume = new SampleChannel(pcm);

          //      var downsample =  new WdlResamplingSampleProvider(Volume, 24000);

                var filter = new RadioFilter(Volume);

                _waveOut = new WaveOut
                {
                    DesiredLatency = 100,
                    DeviceNumber = speakers
                };
                //75ms latency in output buffer

                _waveOut.Init(filter);

                _waveIn.StartRecording();
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Quitting!");

                Environment.Exit(1);
            }
        }

        public void StopEncoding()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_playBuffer != null)
            {
                _playBuffer.ClearBuffer();
                _playBuffer = null;
            }
        }
    }
}