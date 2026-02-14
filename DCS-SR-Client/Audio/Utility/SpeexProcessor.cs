using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility
{
    internal class SpeexProcessor : IDisposable
    {
        private readonly Preprocessor speex;
        //every 40ms so 500
        private int count;

        public SpeexProcessor(int frameSize, int sampleRate)
        {
            speex = new Preprocessor(frameSize, sampleRate);
            RefreshSettings(true);
        }

        ~SpeexProcessor()
        {
            speex?.Dispose();
        }

        public void Dispose()
        {
            speex?.Dispose();
        }

        public void Process(ArraySegment<short> frame)
        {
            RefreshSettings(false);
            speex.Process(frame);
        }

        private void RefreshSettings(bool force)
        {
            //only check every 5 seconds - 5000/40ms is 125 frames
            if (count > 125 || force)
            {
                speex.AutomaticGainControl = false;
                
                //only check settings store every 5 seconds
                var settingsStore = GlobalSettingsStore.Instance;

                var denoise = settingsStore.GetClientSettingBool(GlobalSettingsKeys.Denoise);
                var denoiseAttenuation = settingsStore.GetClientSetting(GlobalSettingsKeys.DenoiseAttenuation).IntValue;
                
                if (denoise != speex.Denoise) speex.Denoise = denoise;
                if (denoiseAttenuation != speex.DenoiseAttenuation) speex.DenoiseAttenuation = denoiseAttenuation;

                count = 0;
            }

            count++;
        }
    }
}
