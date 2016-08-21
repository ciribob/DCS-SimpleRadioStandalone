using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class RadioAudioProvider
    {
        private readonly Settings _settings;

        public VolumeSampleProvider VolumeSampleProvider { get; }
        public BufferedWaveProvider BufferedWaveProvider { get; }

        private Random _random = new Random();

        public RadioAudioProvider(bool radioFilter)
        {
            BufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(AudioManager.SAMPLE_RATE, 16, 2));

            BufferedWaveProvider.DiscardOnBufferOverflow = true;
            BufferedWaveProvider.BufferDuration = new TimeSpan(0, 0, 2); //2 seconds buffer

            var pcm = new Pcm16BitToSampleProvider(BufferedWaveProvider);

            if (radioFilter)
            {
                var filter = new RadioFilter(pcm);
                VolumeSampleProvider = new VolumeSampleProvider(filter);
            }
            else
            {
                VolumeSampleProvider = new VolumeSampleProvider(pcm);
            }
            _settings = Settings.Instance;
        }


        public void AddSamples(byte[] pcmAudio, bool decrytable, short encryptionKey, int receiveRadio)
        {
            //convert to Stereo Mix
            var settingType = SettingType.Radio1Channel;
            byte[] stereoMix;
            if (receiveRadio == 0)
            {
                settingType = SettingType.IntercomChannel;
            }
            else if (receiveRadio == 1)
            {
                settingType = SettingType.Radio1Channel;
            }
            else if (receiveRadio == 2)
            {
                settingType = SettingType.Radio2Channel;
            }
            else if (receiveRadio == 3)
            {
                settingType = SettingType.Radio3Channel;
            }
            else
            {
                //different radio
                stereoMix = CreateBothMix(pcmAudio,decrytable,  encryptionKey);
                BufferedWaveProvider.AddSamples(stereoMix, 0, stereoMix.Length);

                return;
            }

            var setting = _settings.UserSettings[(int) settingType];

            if (setting == "Left")
            {
                stereoMix = CreateLeftMix(pcmAudio, decrytable, encryptionKey);
            }
            else if (setting == "Right")
            {
                stereoMix = CreateRightMix(pcmAudio, decrytable, encryptionKey);
            }
            else
            {
                stereoMix = CreateBothMix(pcmAudio, decrytable, encryptionKey);
            }

            BufferedWaveProvider.AddSamples(stereoMix, 0, stereoMix.Length);
        }

        private byte[] CreateLeftMix(byte[] pcmAudio, bool decrytable, short encryptionKey)
        {
            var stereoMix = new byte[pcmAudio.Length*2];
            for (var i = 0; i < pcmAudio.Length/2; i++)
            {
                if (decrytable || encryptionKey == 0)
                {
                    stereoMix[i*4] = pcmAudio[i*2];
                    stereoMix[i*4 + 1] = pcmAudio[i*2 + 1];
                }
                else
                {
                    stereoMix[i*4] = (byte) _random.Next(16);
                    stereoMix[i*4 + 1] = (byte) _random.Next(16);
                }

                stereoMix[i*4 + 2] = 0;
                stereoMix[i*4 + 3] = 0;
            }
            return stereoMix;
        }

        private byte[] CreateRightMix(byte[] pcmAudio, bool decrytable, short encryptionKey)
        {
            var stereoMix = new byte[pcmAudio.Length*2];
            for (var i = 0; i < pcmAudio.Length/2; i++)
            {
                stereoMix[i*4] = 0;
                stereoMix[i*4 + 1] = 0;

                if (decrytable || encryptionKey == 0)
                {
                    stereoMix[i*4 + 2] = pcmAudio[i*2];
                    stereoMix[i*4 + 3] = pcmAudio[i*2 + 1];
                }
                else
                {
                    stereoMix[i*4 + 2] = (byte) _random.Next(16);
                    stereoMix[i*4 + 3] = (byte) _random.Next(16);
                }

            }
            return stereoMix;
        }

        private byte[] CreateBothMix(byte[] pcmAudio, bool decrytable, short encryptionKey)
        {
            var stereoMix = new byte[pcmAudio.Length*2];
            for (var i = 0; i < pcmAudio.Length/2; i++)
            {
                if (decrytable || encryptionKey == 0)
                {
                    stereoMix[i*4] = pcmAudio[i*2];
                    stereoMix[i*4 + 1] = pcmAudio[i*2 + 1];

                    stereoMix[i*4 + 2] = pcmAudio[i*2];
                    stereoMix[i*4 + 3] = pcmAudio[i*2 + 1];
                }
                else
                {
                    stereoMix[i*4] = (byte) _random.Next(16);
                    stereoMix[i*4 + 1] = (byte) _random.Next(16);

                    stereoMix[i*4 + 2] = (byte) _random.Next(16);
                    stereoMix[i*4 + 3] = (byte) _random.Next(16);
                }

            }
            return stereoMix;
        }
    }
}
