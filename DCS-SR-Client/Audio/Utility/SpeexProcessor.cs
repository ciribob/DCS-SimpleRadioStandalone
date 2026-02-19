using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility
{
    internal class SpeexProcessor : IDisposable
    {
        private Preprocessor speex;
        //every 40ms so 500
        private int count;

        public SpeexProcessor(int frameSize, int sampleRate)
        {
            speex = new Preprocessor(frameSize, sampleRate);
            RefreshSettings(true);
        }

        #region Disposable

        // https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose#base-class-with-managed-resources
        private bool _isDisposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (disposing)
                {
                    speex?.Dispose();
                    speex = null;
                }
            }
        }
#endregion Disposable

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
