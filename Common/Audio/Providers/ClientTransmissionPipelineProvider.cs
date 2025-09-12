using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models.Dto;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.NetCoreServer;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.Codecs;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    // #TODO: ISampleProvider?
    internal class ClientTransmissionPipelineProvider
    {
        public ClientTransmissionPipelineProvider()
        {
            LoadTones();
            RefreshSettings();
        }

        public void Process(DeJitteredTransmission transmission, Span<float> audioOut)
        {
            RefreshSettings();
            if (transmission.NoAudioEffects)
                return;

            // Copy to regular array for compatibility with ISampleProvider.
            var floatPool = ArrayPool<float>.Shared;
            var scratchBuffer = floatPool.Rent(audioOut.Length);
            var sourceBuffer = floatPool.Rent(audioOut.Length);
            audioOut.CopyTo(sourceBuffer);


            ISampleProvider transmissionProvider = new TransmissionProvider(sourceBuffer, 0, audioOut.Length);
            transmissionProvider = new VolumeSampleProvider(transmissionProvider)
            {
                Volume = transmission.Volume
            };

            if (RadioEffectsEnabled)
            {
                if (IsIntercomLike(transmission.Modulation))
                {
                    if (!TxRadioModels.TryGetValue("intercom", out var intercomModel))
                    {
                        intercomModel = RadioModelFactory.Instance.LoadTxOrDefaultIntercom("intercom");
                        TxRadioModels.Add("intercom", intercomModel);
                    }
                    transmissionProvider = BuildRadioPipeline(transmissionProvider, intercomModel, transmission);
                }
                else
                {
                    var preset = GetRadioModel(transmission);
                    transmissionProvider = BuildRadioEffectsChain(transmissionProvider, preset, transmission);
                }
            }

            transmissionProvider.Read(scratchBuffer, 0, audioOut.Length);
            scratchBuffer.AsSpan(0, audioOut.Length).CopyTo(audioOut);

            floatPool.Return(sourceBuffer);
            floatPool.Return(scratchBuffer);
        }

        private static bool IsIntercomLike(Modulation modulation)
        {
            return modulation == Modulation.MIDS
                    || modulation == Modulation.SATCOM
                    || modulation == Modulation.INTERCOM;
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ISampleProvider BuildRadioEffectsChain(ISampleProvider voiceProvider, TxRadioModel radioModel, DeJitteredTransmission transmission)
        {
            if (ClippingEnabled)
            {
                voiceProvider = new ClippingProvider(voiceProvider, RadioFilter.CLIPPING_MIN, RadioFilter.CLIPPING_MAX);
            }

            if (BackgroundNoiseEffect)
            {
                voiceProvider = BuildBackgroundNoiseEffect(voiceProvider, radioModel, transmission);
            }

            if (RadioEffectsEnabled)
            {
                voiceProvider = BuildRadioPipeline(voiceProvider, radioModel, transmission);
            }

            voiceProvider = new ClippingProvider(voiceProvider, -1, 1);

            return voiceProvider;
        }

        private ISampleProvider BuildBackgroundNoiseEffect(ISampleProvider voiceProvider, TxRadioModel radioModel, DeJitteredTransmission transmission)
        {
            // Frequency at which we switch between HF noise (very grainy/rain sounding) vs white noise.
            var hfNoiseFrequencyCutoff = 25e6;
            var isHFNoise = transmission.Frequency <= hfNoiseFrequencyCutoff;
            var backgroundEffectsProvider = new MixingSampleProvider(voiceProvider.WaveFormat);
            // Noise, initial power depends on frequency band.
            // HF very susceptible (higher base), V/UHF not as much.
            // We can do a rough estimation applying a log-based rule.
            // Rough figures for attenuation:
            // 1-30 (HF): 0-17 dB
            // 30-100: 17-23 dB
            // 100-200 (VHF): 23-26 dB
            // 200-400 (UHF): 26-29 dB
            var noiseGainDB = -Math.Log(transmission.Frequency / 1e6) * 10 / 2;

            // Apply user defined noise attenuation/gain
            noiseGainDB += isHFNoise ? HFNoiseGainOffsetDB : NoiseGainOffsetDB;
            // Apply radio model noise attenuation/gain.
            noiseGainDB += radioModel.NoiseGain;

            var noiseGeneratorGainDB = !isHFNoise ? noiseGainDB : 0f;

            // #TODO: noise type should be part of the radio preset really.
            // Tube/HF noise (red/pink) vs transistor (white/AGWN)
            ISampleProvider noiseProvider = null;
            if (transmission.Frequency > hfNoiseFrequencyCutoff)
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

            if (transmission.Frequency <= hfNoiseFrequencyCutoff)
            {
                TxRadioModel hfNoise = null;
                if(!TxRadioModels.TryGetValue("hfnoise", out hfNoise))
                {
                    hfNoise = RadioModelFactory.Instance.LoadTxRadio("hfnoise");
                    TxRadioModels.Add("hfnoise", hfNoise);
                }

                if (hfNoise != null)
                {
                    var noiseTransmission = 
                    noiseProvider = BuildRadioPipeline(noiseProvider, hfNoise, new DeJitteredTransmission
                    {
                        Frequency = transmission.Frequency,
                        Modulation = transmission.Modulation,
                        Encryption = transmission.Encryption,
                    });
                }

                noiseProvider = new VolumeSampleProvider(noiseProvider)
                {
                    Volume = (float)Decibels.DecibelsToLinear(noiseGainDB)
                };
            }

            backgroundEffectsProvider.AddMixerInput(noiseProvider);

            var tone = GetToneProvider(transmission.Modulation);
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
            return backgroundEffectsProvider;
        }

        private ISampleProvider BuildRadioPipeline(ISampleProvider voiceProvider, TxRadioModel radioModel, DeJitteredTransmission details)
        {
            radioModel.TxSource.Source = voiceProvider;
            var encryptionEffects = RadioEncryptionEffect && (details.Modulation == Modulation.MIDS || details.Encryption > 0);
            if (encryptionEffects && radioModel.EncryptionProvider != null)
            {
                voiceProvider = radioModel.EncryptionProvider;
            }
            else
            {
                voiceProvider = radioModel.TxEffectProvider;
            }

            return voiceProvider;
        }

        private VolumeCachedEffectProvider GetToneProvider(Modulation modulation)
        {
            switch (modulation)
            {
                case Modulation.FM:
                case Modulation.SINCGARS:
                    return ToneProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE];

                case Modulation.HAVEQUICK:
                    return ToneProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE];
            }

            return null;
        }

        private TxRadioModel GetRadioModel(DeJitteredTransmission transmission)
        {

            SRClientBase sender = null;
            var guid = transmission.Guid;
            if (guid == null)
            {
                guid = transmission.OriginalClientGuid;
            }

            var candidateModel = string.Empty;
            if (PerRadioModelEffect && guid != null && ConnectedClientsSingleton.Instance.Clients.TryGetValue(guid, out sender))
            {
                if (sender != null && sender.RadioInfo != null && sender.RadioInfo.radios != null)
                {
                    // Try to find which radio the transmission is coming from.
                    // "best match".
                    // #FIXME this doesn't discriminate if multiple radios are set to the same frequency
                    var candidate = Array.Find(sender.RadioInfo.radios, radio => radio.modulation == transmission.Modulation && RadioBase.FreqCloseEnough(transmission.Frequency, radio.freq));

                    if (candidate != null)
                    {
                        candidateModel = candidate.Model;
                    }
                }
            }

            if (!TxRadioModels.TryGetValue(candidateModel, out var radioModel))
            {
                radioModel = RadioModelFactory.Instance.LoadTxOrDefaultRadio(candidateModel);
                TxRadioModels.Add(candidateModel, radioModel);
            }

            return radioModel;
        }

        private void RefreshSettings()
        {
            long now = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(now - LastRefresh).TotalSeconds <= 3)
                return;

            //3 seconds since last refresh
            LastRefresh = now;

            var profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
            PerRadioModelEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.PerRadioModelEffects);
            RadioEffectsEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
            RadioEncryptionEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects);
            clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
            BackgroundNoiseEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioBackgroundNoiseEffect);

            NoiseGainOffsetDB = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NoiseGainDB);
            HFNoiseGainOffsetDB = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HFNoiseGainDB);

            ToneProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.NATOTone);
            ToneProviders[CachedAudioEffect.AudioEffectTypes.NATO_TONE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.NATOToneVolume);

            ToneProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE].Enabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.HAVEQUICKTone);
            ToneProviders[CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE].Volume = profileSettings.GetClientSettingFloat(ProfileSettingsKeys.HQToneVolume);

        }

        private long LastRefresh { get; set; }

        private bool PerRadioModelEffect { get; set; }
        private bool RadioEffectsEnabled { get; set; }
        private bool RadioEncryptionEffect { get; set; }
        private bool BackgroundNoiseEffect { get; set; }

        private bool clippingEnabled = false;
        private bool ClippingEnabled => RadioEffectsEnabled && clippingEnabled;
        private float NoiseGainOffsetDB { get; set; } = 0f;
        private float HFNoiseGainOffsetDB { get; set; } = 0f;

        private IDictionary<string, TxRadioModel> TxRadioModels { get; } = new Dictionary<string, TxRadioModel>();
        private IReadOnlyDictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider> ToneProviders { get; set; }

        

        private void LoadTones()
        {
            var loadedTones = new Dictionary<CachedAudioEffect.AudioEffectTypes, VolumeCachedEffectProvider>();
            
            var effectProvider = CachedAudioEffectProvider.Instance;
            loadedTones.Add(CachedAudioEffect.AudioEffectTypes.NATO_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.NATOTone)));
            loadedTones.Add(CachedAudioEffect.AudioEffectTypes.HAVEQUICK_TONE, new VolumeCachedEffectProvider(new CachedEffectProvider(effectProvider.HAVEQUICKTone)));

            ToneProviders = loadedTones.ToFrozenDictionary();
        }
    }
}
