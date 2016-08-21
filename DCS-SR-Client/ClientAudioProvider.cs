using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class ClientAudioProvider: RadioAudioProvider
    {
        public long LastUpdate;
     

        public ClientAudioProvider():base(true)
        {
            
        }

        public void AddSamples(ClientAudio audio)
        {
            base.AddSamples(audio.PcmAudio,audio.Decryptable,audio.Encryption,audio.ReceivedRadio);
        }
    }
}