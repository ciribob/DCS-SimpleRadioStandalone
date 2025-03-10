using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers
{
    public class CachedLoopingNatoToneAudioProvider:IWaveProvider
    {
        private int _position = 0;

        private readonly short[] _audioEffectShort;
        private IWaveProvider source;

        public CachedLoopingNatoToneAudioProvider(IWaveProvider source, WaveFormat waveFormat)
        {

            this.WaveFormat = waveFormat;
            var effectDouble = CachedAudioEffectProvider.Instance.NATOTone.AudioEffectFloat;

            _audioEffectShort = new short[effectDouble.Length];
            for (int i = 0; i < effectDouble.Length; i++)
            {
                _audioEffectShort[i] = (short) (effectDouble[i] * 32768f);
            }


            this.source = source;
        }

        public WaveFormat WaveFormat { get; }
        public int Read(byte[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);

            if (!GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.NATOTone) || _audioEffectShort == null)
            {
                return read;
            }

            var effectBytes = GetEffect(read / 2);

            //mix together
            for (int i = 0; i < read / 2; i++)
            {
                short audio = ConversionHelpers.ToShort(buffer[(offset + i) * 2], buffer[((i + offset) * 2) + 1]);

                audio = (short)(audio + effectBytes[i]);

                //buffer[i + offset] = effectBytes[i]+buffer[i + offset];

                byte byte1;
                byte byte2;
                ConversionHelpers.FromShort(audio, out byte1, out byte2);

                buffer[(offset + i) * 2] = byte1;
                buffer[((i + offset) * 2) + 1] = byte2;
            }

            return read;
        }

        private short[] GetEffect(int count)
        {
            short[] loopedEffect = new short[count];

            var i = 0;
            while (i < count)
            {
                loopedEffect[i] = _audioEffectShort[_position];
                _position++;

                if (_position == _audioEffectShort.Length)
                {
                    _position = 0;
                }

                i++;
            }

            return loopedEffect;
        }
    }
}
