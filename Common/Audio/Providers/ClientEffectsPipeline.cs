using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NAudio.Dsp;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;



namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{

    public class ClientEffectsPipeline
    {
        private readonly Random _random = new Random();

        private Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider> _fxProviders = new Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider>();

        private readonly CachedAudioEffectProvider effectProvider = CachedAudioEffectProvider.Instance;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private bool radioEffectsEnabled;
        private bool clippingEnabled;

        private long lastRefresh = 0; //last refresh of settings

        private readonly ProfileSettingsStore profileSettings;

        private bool radioEffects;
        private bool radioBackgroundNoiseEffect;
        private bool radioEncryptionEffect;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;

        private string PresetsFolder
        {
            get
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "Presets");
            }
        }

        public ClientEffectsPipeline()
        {
            profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
            serverSettings = SyncedServerSettings.Instance;

            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.NATO_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.NATOTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HAVEQUICKTone)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.UHF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.UHFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.VHF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.VHFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.HF_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HFNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.FM_NOISE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.FMNoise)));
            _fxProviders.Add(CachedAudioEffect.AudioEffectTypes.AM_COLLISION, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.AMCollision)));

            RefreshSettings();
            LoadRadioModels();
        }

        private IReadOnlyDictionary<string, RadioPreset> Presets;

        private void LoadRadioModels()
        {
            var loadedPresets = new Dictionary<string, RadioPreset>();
            try
            {
                var presets = Directory.EnumerateFiles(PresetsFolder, "*.json");
                foreach (var presetFile in presets)
                {
                    var presetName = Path.GetFileNameWithoutExtension(presetFile).ToLowerInvariant().Replace("-custom", null);
                    using (Stream jsonFile = File.OpenRead(presetFile))
                    {
                        RadioPreset preset = null;
                        try
                        {
                            preset = JsonSerializer.Deserialize<RadioPreset>(jsonFile, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Unable to parse radio preset file {presetFile}", ex);
                        }

                        if (preset != null)
                        {
                            if (!loadedPresets.TryAdd(presetName, preset))
                            {
                                // If we load a customization afterwards, it takes precedence.
                                // If we happened to have already loaded it, ignore the 'default'.
                                if (presetName.EndsWith("-custom.json"))
                                {
                                    loadedPresets.Remove(presetName);
                                    loadedPresets.Add(presetName, preset);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Unable to parse radio preset files {PresetsFolder}", ex);
            }


            // If these were loaded from files, the try adds will fail here.
            loadedPresets.TryAdd("intercom", DefaultRadioPresets.Intercom);
            loadedPresets.TryAdd("arc210", DefaultRadioPresets.Arc210);

            Presets = loadedPresets.ToFrozenDictionary();
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

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect);
                radioEncryptionEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects);

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
                && (lastTransmission.Modulation == Modulation.AM
                    || lastTransmission.Modulation == Modulation.FM
                    || lastTransmission.Modulation == Modulation.SINCGARS
                    || lastTransmission.Modulation == Modulation.HAVEQUICK)
                && irlRadioRXInterference)
            {
                if (transmissions.Count > 1)
                {
                    //All AM is wrecked if more than one transmission
                    //For HQ - only if more than TWO transmissions and its totally fucked
                    var amCollisionProvider = _fxProviders[CachedAudioEffect.AudioEffectTypes.AM_COLLISION];
                    if (lastTransmission.Modulation == Modulation.HAVEQUICK && transmissions.Count > 2 && amCollisionProvider.Active)
                    {
                        var collisionProvider = new VolumeSampleProvider(amCollisionProvider);
                        collisionProvider.Volume = lastTransmission.Volume;

                        //replace the buffer with our own
                        collisionProvider.Read(tempBuffer, 0, clientTransmissionLength);

                        process = false;
                    }
                    else if (lastTransmission.Modulation == Modulation.AM && amCollisionProvider.Active)
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
                    else if (lastTransmission.Modulation == Modulation.FM || lastTransmission.Modulation == Modulation.SINCGARS)
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
            ISampleProvider transmissionProvider = new TransmissionProvider(buffer, offset);
            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == Modulation.MIDS
                    || transmission.Modulation == Modulation.SATCOM
                    || transmission.Modulation == Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        transmissionProvider = AddRadioEffectIntercom(transmissionProvider, transmission.Modulation);
                    }
                }
                else
                {
                    string model = null;
                    SRClientBase sender = null;
                    if (ConnectedClientsSingleton.Instance.Clients.TryGetValue(transmission.Guid, out sender))
                    {
                        if (sender != null)
                        {
                            // Try to find which radio the transmission is coming from.
                            // "best match".
                            foreach (var radio in sender.RadioInfo.radios)
                            {
                                if (radio.modulation == transmission.Modulation && RadioBase.FreqCloseEnough(transmission.Frequency, radio.freq))
                                {
                                    model = radio.Model;
                                    break;
                                }
                            }
                        }
                    }
                    

                    RadioPreset preset = Presets.GetValueOrDefault(model, Presets["arc210"]);

                    transmissionProvider = AddRadioEffect(transmissionProvider, preset, transmission.Modulation, transmission.Frequency, transmission.Encryption);
                }
            }

            transmissionProvider = new VolumeSampleProvider(transmissionProvider)
            {
                Volume = transmission.Volume
            };

            transmissionProvider.Read(buffer, offset, count);
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

        private ISampleProvider AddRadioEffectIntercom(ISampleProvider voiceProvider, Modulation modulation)
        {
            return BuildMicPipeline(voiceProvider, Presets["intercom"], modulation == Modulation.MIDS);
        }

        private VolumeCachedEffectProvider GetToneProvider(Modulation modulation)
        {
            switch (modulation)
            {
                case Modulation.FM:
                case Modulation.SINCGARS:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE];

                case Modulation.HAVEQUICK:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE];
            }

            return null;
        }

        private ISampleProvider GetNoiseProvider(Modulation modulation, double freq)
        {
            switch (modulation)
            {
                case Modulation.AM:
                case Modulation.HAVEQUICK:
                    if (freq > 200e6) // UHF range
                    {
                        return _fxProviders[CachedAudioEffect.AudioEffectTypes.UHF_NOISE];
                    }

                    if (freq > 80e6)
                    {
                        return _fxProviders[CachedAudioEffect.AudioEffectTypes.VHF_NOISE];
                    }

                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.HF_NOISE];
                case Modulation.FM:
                case Modulation.SINCGARS:
                    return _fxProviders[CachedAudioEffect.AudioEffectTypes.FM_NOISE];
            }

            return null;
        }
        private ISampleProvider BuildMicPipeline(ISampleProvider voiceProvider, RadioPreset radioModel, bool encryptionEffects)
        {
            voiceProvider = new MixingSampleProvider(new ISampleProvider[]{
                voiceProvider,
                new SignalGenerator(voiceProvider.WaveFormat.SampleRate, 1)
                {
                    Type = SignalGeneratorType.White,
                    Gain = (float)Decibels.DecibelsToLinear(radioModel.NoiseGain -30),
                },
            });
            voiceProvider = new FiltersProvider(voiceProvider)
            {
                Filters = radioModel.PrepassFilters
            };

            voiceProvider = new SaturationProvider(voiceProvider)
            {
                GainDB = radioModel.Saturation.Gain,
                ThresholdDB = radioModel.Saturation.Threshold
            };

            voiceProvider = new SimpleCompressorEffect(voiceProvider)
            {
                Attack = radioModel.Compressor.Attack,
                MakeUpGain = radioModel.Compressor.MakeUp,
                Release = radioModel.Compressor.Release,
                Enabled = true,
                Threshold = radioModel.Compressor.Threshold,
                Ratio = 1f / radioModel.Compressor.Slope,
            };

            // post
            voiceProvider = new FiltersProvider(voiceProvider)
            {
                Filters = radioModel.PostCompressorFilters
            };

            // Bump gain.
            voiceProvider = new VolumeSampleProvider(voiceProvider)
            {
                Volume = (float)Decibels.DecibelsToLinear(radioModel.PostGain)
            };

            if (encryptionEffects)
            {
                voiceProvider = new CVSDProvider(voiceProvider);
            }

            // Add receiver bandpass.
            voiceProvider = new FiltersProvider(voiceProvider)
            {
                Filters = radioModel.ReceiverFilters
            };

            return voiceProvider;
        }
        private ISampleProvider AddRadioEffect(ISampleProvider voiceProvider, RadioPreset radioModel, Modulation modulation, double freq, short encryption)
        {
            // NAudio version.
            // Chain of effects being applied.
            // TODO: We should be able to precompute a lot of this.
            if (radioEffectsEnabled)
            {
                if (clippingEnabled)
                {
                    voiceProvider = new ClippingProvider(voiceProvider, RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
                }

                voiceProvider = BuildMicPipeline(voiceProvider, radioModel, radioEncryptionEffect && encryption > 0);
            }



            // Mix in the noise, tones, etc.
            // Note that they are applied LIFO.
            var fxMixer = new MixingSampleProvider(voiceProvider.WaveFormat);

            if (radioBackgroundNoiseEffect)
            {
                var noise = GetNoiseProvider(modulation, freq);
                if (noise != null)
                {
                    fxMixer.AddMixerInput(new VolumeSampleProvider(noise)
                    {
                        Volume = (float)Decibels.DecibelsToLinear(radioModel.NoiseGain)
                    });
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

            return voiceProvider;
        }
    }
}