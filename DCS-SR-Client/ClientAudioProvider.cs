using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class ClientAudioProvider
    {
        public long LastUpdate;
        private readonly Settings _settings;


        public ClientAudioProvider()
        {
            BufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(AudioManager.SAMPLE_RATE, 16, 2));
           
            
            //    
            BufferedWaveProvider.DiscardOnBufferOverflow = true;
            BufferedWaveProvider.BufferDuration = new TimeSpan(0, 0, 2); //2 seconds buffer

            var pcm = new Pcm16BitToSampleProvider(BufferedWaveProvider);

            var filter = new RadioFilter(pcm);

            VolumeSampleProvider = new VolumeSampleProvider(filter);

            _settings = Settings.Instance;
        }

        public VolumeSampleProvider VolumeSampleProvider { get; }
        public BufferedWaveProvider BufferedWaveProvider { get; }
        private Random _random = new Random();


        public void AddSamples(ClientAudio audio)
        {
            //convert to Stereo Mix
            var settingType = SettingType.Radio1Channel;
            byte[] stereoMix;
            if (audio.ReceivedRadio == 0)
            {
                settingType = SettingType.IntercomChannel;
            }
            else if (audio.ReceivedRadio == 1)
            {
                settingType = SettingType.Radio1Channel;
            }
            else if (audio.ReceivedRadio == 2)
            {
                settingType = SettingType.Radio2Channel;
            }
            else if (audio.ReceivedRadio == 3)
            {
                settingType = SettingType.Radio3Channel;
            }
            else
            {
                //different radio
                stereoMix = CreateBothMix(audio);
                BufferedWaveProvider.AddSamples(stereoMix, 0, stereoMix.Length);

                return;
            }

            var setting = _settings.UserSettings[(int) settingType];

            if (setting == "Left")
            {
                stereoMix = CreateLeftMix(audio);
            }
            else if (setting == "Right")
            {
                stereoMix = CreateRightMix(audio);
            }
            else
            {
                stereoMix = CreateBothMix(audio);
            }

            BufferedWaveProvider.AddSamples(stereoMix, 0, stereoMix.Length);
        }

        private byte[] CreateLeftMix(ClientAudio audio)
        {
            var stereoMix = new byte[audio.PcmAudio.Length*2];
            for (var i = 0; i < audio.PcmAudio.Length/2; i++)
            {
                if (audio.Decryptable || audio.Encryption == 0)
                {
                    stereoMix[i * 4] = audio.PcmAudio[i * 2];
                    stereoMix[i * 4 + 1] = audio.PcmAudio[i * 2 + 1];
                }
                else
                {
                    stereoMix[i*4] = (byte) _random.Next(16);
                    stereoMix[i * 4 + 1] = (byte)_random.Next(16);
                }
                
                stereoMix[i*4 + 2] = 0;
                stereoMix[i*4 + 3] = 0;
            }
            return stereoMix;
        }

        private byte[] CreateRightMix(ClientAudio audio)
        {
            var stereoMix = new byte[audio.PcmAudio.Length*2];
            for (var i = 0; i < audio.PcmAudio.Length/2; i++)
            {
                stereoMix[i*4] = 0;
                stereoMix[i*4 + 1] = 0;

                if (audio.Decryptable || audio.Encryption == 0) 
                {
                    stereoMix[i*4 + 2] = audio.PcmAudio[i*2];
                    stereoMix[i*4 + 3] = audio.PcmAudio[i*2 + 1];
                }
                else
                {
                    stereoMix[i * 4 + 2] = (byte)_random.Next(16);
                    stereoMix[i * 4 + 3] = (byte)_random.Next(16);
                }
               
            }
            return stereoMix;
        }

        private byte[] CreateBothMix(ClientAudio audio)
        {
            var stereoMix = new byte[audio.PcmAudio.Length*2];
            for (var i = 0; i < audio.PcmAudio.Length/2; i++)
            {
                if (audio.Decryptable || audio.Encryption == 0)
                {
                    stereoMix[i*4] = audio.PcmAudio[i*2];
                    stereoMix[i*4 + 1] = audio.PcmAudio[i*2 + 1];

                    stereoMix[i*4 + 2] = audio.PcmAudio[i*2];
                    stereoMix[i*4 + 3] = audio.PcmAudio[i*2 + 1];
                }
                else
                {
                    stereoMix[i * 4] = (byte)_random.Next(16);
                    stereoMix[i * 4 + 1] = (byte)_random.Next(16);

                    stereoMix[i * 4 + 2] = (byte)_random.Next(16);
                    stereoMix[i * 4 + 3] = (byte)_random.Next(16);
                }
               
            }
            return stereoMix;
        }
    }
}