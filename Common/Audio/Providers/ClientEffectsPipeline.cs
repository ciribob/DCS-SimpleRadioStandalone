using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
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

        private float NoiseGainOffsetDB { get; set; } = 0f;

        private bool irlRadioRXInterference = false;

        private readonly SyncedServerSettings serverSettings;

        private struct TransmissionInfo
        {
            public double Frequency;
            public Modulation Modulation;

            public short Encryption { get; internal set; }
        }

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

            RefreshSettings();
            LoadRadioModels();
        }

        private class RadioPreset
        {
            public DeferredSourceProvider TxSource { get; } = new DeferredSourceProvider();
            public DeferredSourceProvider RxSource { get; } = new DeferredSourceProvider();

            public ISampleProvider RxEffectProvider { get; set; }
            public ISampleProvider TxEffectProvider { get; set; }

            public ISampleProvider EncryptionProvider { get; set; }

            public float NoiseGain { get; set; }

            public RadioPreset(Models.Dto.RadioPreset dtoPreset)
            {
                RxEffectProvider = dtoPreset.RxEffect.ToSampleProvider(RxSource);
                TxEffectProvider = dtoPreset.TxEffect.ToSampleProvider(TxSource);

                if (dtoPreset.EncryptionEffect != null)
                {
                    EncryptionProvider = dtoPreset.EncryptionEffect.ToSampleProvider(TxEffectProvider);
                }

                NoiseGain = dtoPreset.NoiseGain;
            }
        }

        private IReadOnlyDictionary<string, RadioPreset> Presets;

        private static readonly RadioPreset Arc210 = new RadioPreset(DefaultRadioPresets.Arc210);
        private static readonly RadioPreset Intercom = new RadioPreset(DefaultRadioPresets.Intercom);
        private void LoadRadioModels()
        {
            var loadedPresets = new Dictionary<string, RadioPreset>();
            try
            {
                var presets = Directory.EnumerateFiles(PresetsFolder, "*.json");
                foreach (var presetFile in presets)
                {
                    var presetName = Path.GetFileNameWithoutExtension(presetFile).ToLowerInvariant().Replace("-custom", null);
                    using (var jsonFile = File.OpenRead(presetFile))
                    {
                        RadioPreset preset = null;
                        try
                        {
                            var loadedPreset = JsonSerializer.Deserialize<Models.Dto.RadioPreset>(jsonFile, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                AllowTrailingCommas = true,
                                ReadCommentHandling = JsonCommentHandling.Skip
                            });

                            preset = new RadioPreset(loadedPreset);
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
                                    loadedPresets[presetName] = preset;
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

                radioEffects = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);

                radioBackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect);
                radioEncryptionEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects);

                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);

                NoiseGainOffsetDB = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NoiseGainDB);
            }
        }

        public float[] ProcessClientTransmissions(float[] tempBuffer, List<DeJitteredTransmission> transmissions, out int clientTransmissionLength)
        {
            RefreshSettings();
            DeJitteredTransmission lastTransmission = transmissions[0];

            clientTransmissionLength = 0;

            // We get one pipeline per radio, so all transmissions should be of the same type (GUARD channels are separate pipelines too).

            // #FIXME: Should some of these settings (modulation, NoAudioEffects) be on the radio instance instead?
            if (lastTransmission.Modulation == Modulation.FM && !lastTransmission.NoAudioEffects && irlRadioRXInterference)
            {
                // FM capture effect: https://www.youtube.com/watch?v=yHRDjhkrDbo
                //FM picketing / picket fencing - pick one transmission at random
                //TODO improve this to pick the stronger frequency?

                int index = _random.Next(transmissions.Count);
                var transmission = transmissions[index];

                clientTransmissionLength = transmission.PCMMonoAudio.Length;
                Array.Copy(transmission.PCMMonoAudio, tempBuffer, clientTransmissionLength);
                tempBuffer = ProcessClientAudioSamples(tempBuffer, clientTransmissionLength, 0, transmission);
            }
            else
            {
                // Everything else should mix (either datalink/satcom type radios, or AM).

                // #TODO: Could trade memory for time and process in parallel, then merge in the destination buffer.
                float[] transmissionBuffer = new float[lastTransmission.PCMAudioLength];
                foreach (var transmission in transmissions)
                {
                    if (transmissionBuffer.Length < transmission.PCMAudioLength)
                    {
                        transmissionBuffer = new float[transmission.PCMAudioLength];
                    }
                    Array.Copy(transmission.PCMMonoAudio, transmissionBuffer, transmission.PCMAudioLength);

                    if (!transmission.NoAudioEffects)
                    {
                        transmissionBuffer = ProcessClientAudioSamples(transmissionBuffer, transmission.PCMAudioLength, 0, transmission);
                    }

                    for (int i = 0; i < transmission.PCMAudioLength; i++)
                    {
                        tempBuffer[i] += transmissionBuffer[i];
                    }

                    clientTransmissionLength = Math.Max(clientTransmissionLength, transmission.PCMAudioLength);
                }
            }

            return tempBuffer;
        }

        public float[] ProcessClientAudioSamples(float[] buffer, int count, int offset, DeJitteredTransmission transmission)
        {
            var transmissionDetails = new TransmissionInfo
            {
                Frequency = transmission.Frequency,
                Modulation = transmission.Modulation,
                Encryption = transmission.Encryption,
            };

            ISampleProvider transmissionProvider = new TransmissionProvider(buffer, offset);
            transmissionProvider = new VolumeSampleProvider(transmissionProvider)
            {
                Volume = transmission.Volume
            };

            if (!transmission.NoAudioEffects)
            {
                if (transmission.Modulation == Modulation.MIDS
                    || transmission.Modulation == Modulation.SATCOM
                    || transmission.Modulation == Modulation.INTERCOM)
                {
                    if (radioEffects)
                    {
                        transmissionProvider = AddRadioEffectIntercom(transmissionProvider, transmissionDetails);
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
                            // #FIXME this doesn't discriminate if multiple radios are set to the same frequency
                            var candidate = Array.Find(sender.RadioInfo.radios, radio => radio.modulation == transmission.Modulation && RadioBase.FreqCloseEnough(transmission.Frequency, radio.freq));
                            
                            if (candidate != null)
                            {
                                model = candidate.Model;
                            }
                        }
                    }
                    
                    var preset = Presets.GetValueOrDefault(model, Arc210);
                    transmissionProvider = AddRadioEffect(transmissionProvider, preset, transmissionDetails);
                }
            }

            transmissionProvider.Read(buffer, offset, count);
            return buffer;
        }

        private ISampleProvider AddRadioEffectIntercom(ISampleProvider voiceProvider, TransmissionInfo details)
        {
            return BuildMicPipeline(voiceProvider, Presets.GetValueOrDefault("intercom", Intercom), details);
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

        private ISampleProvider BuildMicPipeline(ISampleProvider voiceProvider, RadioPreset radioModel, TransmissionInfo details)
        {
            radioModel.TxSource.Source = voiceProvider;
            var encryptionEffects = radioEncryptionEffect && (details.Modulation == Modulation.MIDS || details.Encryption > 0);
            if (encryptionEffects && radioModel.EncryptionProvider != null)
            {
                voiceProvider = radioModel.EncryptionProvider;
            }
            else
            {
                voiceProvider = radioModel.TxEffectProvider;
            }

            radioModel.RxSource.Source = voiceProvider;
            voiceProvider = radioModel.RxEffectProvider;
            return voiceProvider;
        }
        private ISampleProvider AddRadioEffect(ISampleProvider voiceProvider, RadioPreset radioModel, TransmissionInfo details)
        {

            if (radioEffectsEnabled && clippingEnabled)
            {
                voiceProvider = new ClippingProvider(voiceProvider, RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
            }
            
            if (radioBackgroundNoiseEffect)
            {
                // Frequency at which we switch between HF noise (very grainy/rain sounding) vs white noise.
                var hfNoiseFrequencyCutoff = 25e6;
                var backgroundEffectsProvider = new MixingSampleProvider(voiceProvider.WaveFormat);
                // Noise, initial power depends on frequency band.
                // HF very susceptible (higher base), V/UHF not as much.
                // We can do a rough estimation applying a log-based rule.
                // Rough figures for attenuation:
                // 1-30 (HF): 0-17 dB
                // 30-100: 17-23 dB
                // 100-200 (VHF): 23-26 dB
                // 200-400 (UHF): 26-29 dB
                var noiseGainDB = -Math.Log(details.Frequency/1e6) * 10 / 2;

                // Apply user defined noise attenuation/gain
                noiseGainDB += NoiseGainOffsetDB;
                // Apply radio model noise attenuation/gain.
                noiseGainDB += radioModel.NoiseGain;

                var noiseGeneratorGainDB = details.Frequency > hfNoiseFrequencyCutoff ? noiseGainDB : 0f;

                // #TODO: noise type should be part of the radio preset really.
                // Tube/HF noise (red/pink) vs transistor (white/AGWN)
                ISampleProvider noiseProvider = null;
                if (details.Frequency > hfNoiseFrequencyCutoff)
                {
                    noiseProvider = new VolumeSampleProvider(new GaussianWhiteNoise())
                    {
                        Volume = (float)Decibels.DecibelsToLinear(noiseGeneratorGainDB),
                    };
                }
                else
                {
                    noiseProvider = new SignalGenerator(voiceProvider.WaveFormat.SampleRate, voiceProvider.WaveFormat.Channels)
                    {
                        Type = SignalGeneratorType.Pink,
                        Gain = (float)Decibels.DecibelsToLinear(noiseGeneratorGainDB),
                    };
                }

                noiseProvider = new FiltersProvider(noiseProvider)
                {
                    Filters = new Dsp.IFilter[] { Dsp.FirstOrderFilter.LowPass(voiceProvider.WaveFormat.SampleRate, 800) },
                };

                if (details.Frequency <= hfNoiseFrequencyCutoff)
                {
                    RadioPreset hfNoise = null;
                    if (Presets.TryGetValue("hfnoise", out hfNoise))
                    {
                        noiseProvider = BuildMicPipeline(noiseProvider, Presets["hfnoise"],
                        new TransmissionInfo
                        {
                            Frequency = details.Frequency,
                            Modulation = details.Modulation,
                            Encryption = 0
                        });
                    }

                    noiseProvider = new VolumeSampleProvider(noiseProvider)
                    {
                        Volume = (float)Decibels.DecibelsToLinear(noiseGainDB)
                    };
                }

                backgroundEffectsProvider.AddMixerInput(noiseProvider);

                var tone = GetToneProvider(details.Modulation);
                if (tone != null && tone.Active)
                {
                    backgroundEffectsProvider.AddMixerInput(tone);
                }

                // #TODO: Mix in ambient

#if false // Mains hum @ 400Hz (aviation standard)
                fxMixer.AddMixerInput(new SignalGenerator(voiceProvider.WaveFormat.SampleRate, 1)
                {
                    Type = SignalGeneratorType.SawTooth,
                    Frequency = 400,
                    Gain = (float)Decibels.DecibelsToLinear(-60),
                });
#endif
                backgroundEffectsProvider.AddMixerInput(voiceProvider);
                voiceProvider = backgroundEffectsProvider;
            }

            // NAudio version.
            // Chain of effects being applied.
            // TODO: We should be able to precompute a lot of this.
            if (radioEffectsEnabled)
            {
                voiceProvider = BuildMicPipeline(voiceProvider, radioModel, details);
            }

            voiceProvider = new ClippingProvider(voiceProvider, -1, 1);

            return voiceProvider;
        }
    }
}