using System;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers.Wave;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using MathNet.Filtering;
using MathNet.Filtering.IIR;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers
{
    namespace Wave
    {
        class OnlineFilterProvider : ISampleProvider
        {

            ISampleProvider Source;
            IOnlineFilter Filter;
            public WaveFormat WaveFormat => Source.WaveFormat;

            public OnlineFilterProvider(ISampleProvider source, IOnlineFilter filter)
            {
                Source = source;
                Filter = filter;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var samplesRead = Source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    buffer[offset + i] = (float)Filter.ProcessSample(buffer[offset + i]);
                }

                return samplesRead;
            }
        }

        class BiQuadProvider : ISampleProvider
        {
            BiQuadFilter Filter;
            ISampleProvider Source;

            public BiQuadProvider(ISampleProvider source, BiQuadFilter filter)
            {
                Filter = filter;
                Source = source;
            }

            public WaveFormat WaveFormat => Source.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                var samplesRead = Source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    buffer[offset + i] = Filter.Transform(buffer[offset + i]);
                }

                return samplesRead;
            }
        }
        class TransmissionProvider : ISampleProvider
        {
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 1);
            private float[] Buffer;
            private int Offset;
            public TransmissionProvider(float[] buffer, int offset)
            {
                Buffer = buffer;
                Offset = offset;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                count = Math.Min(Buffer.Length - Offset, count);
                Array.Copy(Buffer, Offset, buffer, offset, count);

                return count;
            }
        }
        class CachedEffectProvider : ISampleProvider
        {
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 1);

            public int Read(float[] buffer, int offset, int count)
            {
                var processed = 0;
                do
                {
                    var availableSamples = Effect.AudioEffectFloat.Length - Position;
                    var samplesToCopy = Math.Min(availableSamples, count - processed);
                    Array.Copy(Effect.AudioEffectFloat, Position, buffer, offset + processed, samplesToCopy);
                    Position += samplesToCopy;

                    if (Position == Effect.AudioEffectFloat.Length)
                    {
                        Position = PositionRollover(Position, Effect.AudioEffectFloat.Length);
                    }

                    processed += samplesToCopy;
                } while (processed < count);
                
                return processed;
            }

            public bool Enabled { get; set; } = true;
            public bool Active => Enabled && Effect.Loaded;


            private int Position { get; set; } = 0;


            private CachedAudioEffect Effect;

            public CachedEffectProvider(CachedAudioEffect Effect)
            {
                this.Effect = Effect;
            }

            protected virtual int PositionRollover(int position, int toneLength)
            {
                if (position == toneLength)
                {
                    position = 0;
                }

                return position;
            }
        }

        class VolumeCachedEffectProvider : ISampleProvider
        {
            private readonly VolumeSampleProvider volumeProvider;
            private readonly CachedEffectProvider effectProvider;

            public WaveFormat WaveFormat => volumeProvider.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                return volumeProvider.Read(buffer, offset, count);
            }

            public VolumeCachedEffectProvider(CachedEffectProvider effectProvider)
            {
                this.effectProvider = effectProvider;
                volumeProvider = new VolumeSampleProvider(effectProvider);
            }

            public float Volume
            {
                get { return volumeProvider.Volume; }
                set
                {
                    volumeProvider.Volume = value;
                }
            }

            public bool Enabled
            {
                get {  return effectProvider.Enabled; }
                set {  effectProvider.Enabled = value; }
            }

            public bool Active => effectProvider.Active;
        }

        class ClippingProvider : ISampleProvider
        {
            private float Min { get; set; }
            private float Max { get; set; }

            public WaveFormat WaveFormat => Source.WaveFormat;

            private ISampleProvider Source;

            public ClippingProvider(ISampleProvider source, float min, float max)
            {
                Source = source;
                Min = min;
                Max = max;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = Source.Read(buffer, offset, count);
                for (int i = 0; i < count; ++i)
                {
                    buffer[offset + i] = Math.Max(Math.Min(buffer[offset + i], Max), Min);
                }
                return samplesRead;
            }
        }
    }
   

    public class ClientEffectsPipeline
    {
        private readonly Random _random = new Random();

        private IOnlineFilter _bandpassFilter = OnlineIirFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.OUTPUT_SAMPLE_RATE, 560, 3900);
        private Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider> _fxProviders = new Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider>();

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;

        private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;

        private bool radioEffectsEnabled;
        private bool clippingEnabled;

        private long lastRefresh = 0; //last refresh of settings

        private readonly Settings.ProfileSettingsStore profileSettings;

        private bool radioEffects;
        private bool radioBackgroundNoiseEffect;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;
        
        public ClientEffectsPipeline()
        {
            profileSettings = Settings.GlobalSettingsStore.Instance.ProfileSettingsStore;
            serverSettings =  SyncedServerSettings.Instance;

            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.NATO_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.NATOTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HAVEQUICKTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.UHF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.UHFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.VHF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.VHFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.FM_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.FMNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.AM_COLLISION, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.AMCollision)));

            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.OUTPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.OUTPUT_SAMPLE_RATE, 4130, 2.0f);
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            //only get settings every 3 seconds - and cache them - issues with performance
            long now = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(now - lastRefresh).TotalSeconds > 3) //3 seconds since last refresh
            {
                lastRefresh = now;

                _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume);

                _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.HAVEQUICKTone);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HQToneVolume);

                radioEffectsEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
                clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);

                _fxProviders[CachedAudioEffect.AudioEffectTypes.UHF_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.UHFNoiseVolume);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.VHF_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.VHFNoiseVolume);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.HF_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseVolume);
                _fxProviders[CachedAudioEffect.AudioEffectTypes.FM_NOISE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.FMNoiseVolume);

                radioEffects = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect) ;

                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
            }
        }

        public float[] ProcessClientTransmissions(float[] tempBuffer, List<DeJitteredTransmission> transmissions, out int clientTransmissionLength)
        {
            RefreshSettings();
            DeJitteredTransmission lastTransmission = transmissions[0];

            clientTransmissionLength = 0;
            foreach (var transmission in transmissions)
            {
                for (int i = 0; i < transmission.PCMAudioLength; i++)
                {
                    tempBuffer[i] += transmission.PCMMonoAudio[i];
                }

                clientTransmissionLength = Math.Max(clientTransmissionLength, transmission.PCMAudioLength);
            }

            bool process = true;

            // take info account server setting AND volume of this radio AND if its AM or FM
            // FOR HAVEQUICK - only if its MORE THAN TWO
            if (lastTransmission.ReceivedRadio != 0
                && !lastTransmission.NoAudioEffects
                && (lastTransmission.Modulation == RadioInformation.Modulation.AM
                    || lastTransmission.Modulation == RadioInformation.Modulation.FM
                    || lastTransmission.Modulation == RadioInformation.Modulation.SINCGARS
                    || lastTransmission.Modulation == RadioInformation.Modulation.HAVEQUICK)
                && irlRadioRXInterference)
            {
                if (transmissions.Count > 1)
                {
                    //All AM is wrecked if more than one transmission
                    //For HQ - only if more than TWO transmissions and its totally fucked
                    var amCollisionProvider = _fxProviders[CachedAudioEffect.AudioEffectTypes.AM_COLLISION];
                    if (lastTransmission.Modulation == RadioInformation.Modulation.HAVEQUICK && transmissions.Count > 2 && amCollisionProvider.Active)
                    {
                        var collisionProvider = new VolumeSampleProvider(amCollisionProvider);
                        collisionProvider.Volume = lastTransmission.Volume;

                        //replace the buffer with our own
                        collisionProvider.Read(tempBuffer, 0, clientTransmissionLength);

                        process = false;
                    }
                    else if (lastTransmission.Modulation == RadioInformation.Modulation.AM && amCollisionProvider.Active)
                    {
                        //AM https://www.youtube.com/watch?v=yHRDjhkrDbo
                        //Heterodyne tone AND audio from multiple transmitters in a horrible mess
                        //TODO improve this

              
                        //process here first
                        tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);
                        process = false;

                        //apply heterodyne tone to the mixdown
                        // TODO: merge into the mixer living in ProcessClientAudioSamples().
                        var collisionMixer = new MixingSampleProvider(new List<ISampleProvider>{
                            new VolumeSampleProvider(amCollisionProvider)
                            {
                                Volume = lastTransmission.Volume,
                            },
                            new TransmissionProvider(tempBuffer, clientTransmissionLength)
                        });

                        collisionMixer.Read(tempBuffer, 0, clientTransmissionLength);
                    }
                    else if (lastTransmission.Modulation == RadioInformation.Modulation.FM || lastTransmission.Modulation == RadioInformation.Modulation.SINCGARS)
                    {
                        //FM picketing / picket fencing - pick one transmission at random
                        //TODO improve this to pick the stronger frequency?

                        int index = _random.Next(transmissions.Count);
                        var transmission = transmissions[index];

                        Array.Copy(transmission.PCMMonoAudio, tempBuffer, transmission.PCMMonoAudio.Length);
                        clientTransmissionLength = transmission.PCMMonoAudio.Length;
                    }
                }
            }

            //only process if AM effect doesnt apply
            if (process)
                tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, lastTransmission);


            return tempBuffer;
        }

        public float[] ProcessClientAudioSamples(float[] buffer, int count, int offset, DeJitteredTransmission transmission)
        {
            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == RadioInformation.Modulation.MIDS
                    || transmission.Modulation == RadioInformation.Modulation.SATCOM
                    || transmission.Modulation == RadioInformation.Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        AddRadioEffectIntercom(buffer, count, offset, transmission.Modulation);
                    }
                }
                else
                {
                    AddRadioEffect(buffer, count, offset, transmission.Modulation, transmission.Frequency);
                }
            }

            //final adjust
            AdjustVolume(buffer, count, offset, transmission.Volume);

            return buffer;
        }

        private void AdjustVolume(float[] buffer, int count, int offset, float volume)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                buffer[outputIndex] *= volume;

                outputIndex++;
            }
        }

        private void AddRadioEffectIntercom(float[] buffer, int count, int offset,RadioInformation.Modulation modulation)
        {
            int outputIndex = offset;
            while (outputIndex < offset + count)
            {
                var audio = _highPassFilter.Transform(buffer[outputIndex]);

                audio = _highPassFilter.Transform(audio);

                if (float.IsNaN(audio))
                    audio = _lowPassFilter.Transform(buffer[outputIndex]);
                else
                    audio = _lowPassFilter.Transform(audio);

                if (!float.IsNaN(audio))
                {
                    // clip
                    if (audio > 1.0f)
                        audio = 1.0f;
                    if (audio < -1.0f)
                        audio = -1.0f;

                    buffer[outputIndex] = audio;
                }

                outputIndex++;
            }
        }

        private VolumeCachedEffectProvider GetToneProvider(RadioInformation.Modulation modulation)
        {
            switch (modulation)
            {
                case RadioInformation.Modulation.FM:
                case RadioInformation.Modulation.SINCGARS:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE];

                case RadioInformation.Modulation.HAVEQUICK:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE];
            }

            return null;
        }

        private VolumeCachedEffectProvider GetNoiseProvider(RadioInformation.Modulation modulation, double freq)
        {
            switch (modulation)
            {
                case RadioInformation.Modulation.AM:
                case RadioInformation.Modulation.HAVEQUICK:
                    if (freq > 200e6) // UHF range
                    {
                        return _fxProviders[CachedAudioEffect.AudioEffectTypes.UHF_NOISE];
                    }

                    if (freq > 80e6)
                    {
                        return _fxProviders[CachedAudioEffect.AudioEffectTypes.VHF_NOISE];
                    }

                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.HF_NOISE];
                case RadioInformation.Modulation.FM:
                case RadioInformation.Modulation.SINCGARS:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.FM_NOISE];
            }

            return null;
        }

        private void AddRadioEffect(float[] buffer, int count, int offset, RadioInformation.Modulation modulation, double freq)
        {
            // NAudio version.
            // Chain of effects being applied.
            // TODO: We should be able to precompute a lot of this.
            ISampleProvider voiceProvider = new TransmissionProvider(buffer, offset);
            if (radioEffectsEnabled)
            {
                if (clippingEnabled)
                {
                    voiceProvider = new ClippingProvider(voiceProvider, RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
                }

                voiceProvider = new OnlineFilterProvider(voiceProvider, _bandpassFilter);
            }

            // Mix in the noise, tones, etc.
            // Note that they are applied LIFO.
            var fxMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioManager.OUTPUT_SAMPLE_RATE, 1));

            if (radioBackgroundNoiseEffect)
            {
                var noise = GetNoiseProvider(modulation, freq);
                if (noise != null && noise.Active)
                {
                    fxMixer.AddMixerInput(noise);
                }
            }

            // Modulation tone, if applicable.
            {
                var tone = GetToneProvider(modulation);
                if (tone != null && tone.Active)
                {
                    fxMixer.AddMixerInput(tone);
                }
            }

            // And now the voice.
            fxMixer.AddMixerInput(voiceProvider);

            voiceProvider = new ClippingProvider(fxMixer, -1, 1);

            // Apply the post processing to the voice!
            voiceProvider.Read(buffer, offset, count);
        }
    }
}
