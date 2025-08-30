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
                //only check settings store every 5 seconds
                var settingsStore = GlobalSettingsStore.Instance;

                var agc = settingsStore.GetClientSettingBool(GlobalSettingsKeys.AGC);
                var agcTarget = settingsStore.GetClientSetting(GlobalSettingsKeys.AGCTarget).IntValue;
                var agcDecrement = settingsStore.GetClientSetting(GlobalSettingsKeys.AGCDecrement).IntValue;
                var agcMaxGain = settingsStore.GetClientSetting(GlobalSettingsKeys.AGCLevelMax).IntValue;

                var denoise = settingsStore.GetClientSettingBool(GlobalSettingsKeys.Denoise);
                var denoiseAttenuation = settingsStore.GetClientSetting(GlobalSettingsKeys.DenoiseAttenuation).IntValue;

                //From https://github.com/mumble-voip/mumble/blob/a189969521081565b8bda93d253670370778d471/src/mumble/Settings.cpp
                //and  https://github.com/mumble-voip/mumble/blob/3ffd9ad3ed18176774d8e1c64a96dffe0de69655/src/mumble/AudioInput.cpp#L605

                if (agc != speex.AutomaticGainControl) speex.AutomaticGainControl = agc;
                if (agcTarget != speex.AutomaticGainControlTarget) speex.AutomaticGainControlTarget = agcTarget;
                if (agcDecrement != speex.AutomaticGainControlDecrement) speex.AutomaticGainControlDecrement = agcDecrement;
                if (agcMaxGain != speex.AutomaticGainControlMaxGain) speex.AutomaticGainControlMaxGain = agcMaxGain;

                if (denoise != speex.Denoise) speex.Denoise = denoise;
                if (denoiseAttenuation != speex.DenoiseAttenuation) speex.DenoiseAttenuation = denoiseAttenuation;

                count = 0;
            }

            count++;
        }
    }
}
