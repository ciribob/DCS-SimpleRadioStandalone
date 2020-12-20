using NAudio.CoreAudioApi;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    public class AudioDeviceListItem: IDisposable
    {
        public string Text { get; set; }
        public MMDevice Value { get; set; }

        public void Dispose()
        {
            Value?.Dispose();
        }

        public override string ToString()
        {
            return Text;
        }
    }
}