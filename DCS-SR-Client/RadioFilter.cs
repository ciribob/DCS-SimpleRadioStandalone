using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using NAudio.Dsp;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.DSP
{
    public class RadioFilter : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.SAMPLE_RATE, 520, 0.97f);
        private readonly BiQuadFilter _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.SAMPLE_RATE, 4130, 2.0f);
        private readonly Settings _settings;

        public RadioFilter(ISampleProvider sampleProvider)
        {
            _source = sampleProvider;

            _settings = Settings.Instance;
        }

        public WaveFormat WaveFormat
        {
            get { return _source.WaveFormat; }
        }

        public int Read(float[] buffer, int offset, int sampleCount)
        {
            var samplesRead = _source.Read(buffer, offset, sampleCount);

            if (_settings.UserSettings[(int) SettingType.RadioEffects] == "ON")
            {
                for (var n = 0; n < sampleCount; n++)
                {
                    var audio = buffer[offset + n];
                    if (audio != 0)
                        // because we have silence in one channel (if a user picks radio left or right ear) we don't want to transform it or it'll play in both
                    {
                        audio = _highPassFilter.Transform(audio);
                        audio = _lowPassFilter.Transform(audio);
                        buffer[offset + n] = audio;
                    }
                }
            }

            return samplesRead;
        }
    }
}