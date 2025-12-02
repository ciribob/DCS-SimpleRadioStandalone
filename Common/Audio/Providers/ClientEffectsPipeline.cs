using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;


namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{

    public class ClientEffectsPipeline
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private float radioEffectRatio = 1.0f; // Default to 1.0 (full effect)
        private bool perRadioModelEffect;
        private bool clippingEnabled;

        private long lastRefresh = 0; //last refresh of settings

        private bool irlRadioRXInterference = false;

        private string ModelsFolder
        {
            get
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "RadioModels");
            }
        }

        private string ModelsCustomFolder
        {
            get
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "RadioModelsCustom");
            }
        }

        public ClientEffectsPipeline()
        {
            RefreshSettings();
        }

        private IDictionary<string, RxRadioModel> RxRadioModels { get; } = new Dictionary<string, RxRadioModel>();
        private void RefreshSettings()
        {
            //only get settings every 3 seconds - and cache them - issues with performance
            long now = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(now - lastRefresh).TotalSeconds > 3) //3 seconds since last refresh
            {
                var profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
                var serverSettings = SyncedServerSettings.Instance;
                lastRefresh = now;

                perRadioModelEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.PerRadioModelEffects);
                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
                clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
                radioEffectRatio = Math.Clamp(profileSettings.GetClientSettingFloat(ProfileSettingsKeys.RadioEffectsRatio), 0f, 1f);
            }
        }

        private ISampleProvider BuildRXPipeline(ISampleProvider voiceProvider, RxRadioModel radioModel)
        {
            radioModel.RxSource.Source = voiceProvider;
            voiceProvider = radioModel.RxEffectProvider;
            return voiceProvider;
        }

        public int ProcessSegments(Span<float> audioOut, IReadOnlyList<TransmissionSegment> segments, string modelName = null)
        {
            RefreshSettings();
            using var drySourceBuffer = new PooledArray<float>(audioOut.Length);
            var drySpan = drySourceBuffer.Array.AsSpan(0, drySourceBuffer.Length);
            drySpan.Clear();

            TransmissionSegment capturedFMSegment = null;
            foreach (var segment in segments)
            {
                if (irlRadioRXInterference && !segment.NoAudioEffects && segment.Modulation == Modulation.FM)
                {
                    // FM Capture effect: sort out the segments and try to see if we latched
                    if (capturedFMSegment == null || capturedFMSegment.ReceivingPower < segment.ReceivingPower)
                    {
                        capturedFMSegment = segment;
                    }
                }
                else
                {
                    // Everything, just mix.
                    // Accumulate in destination buffer.
                    Mix(drySpan, segment.Audio.AsSpan());
                }
            }

            if (capturedFMSegment != null)
            {
                // Use the last one (highest power).
                Mix(drySpan, capturedFMSegment.Audio.AsSpan());
            }

            

            //Get desire RadioModel
            var desiredName = perRadioModelEffect && modelName != null ? modelName : string.Empty;
            if (!RxRadioModels.TryGetValue(desiredName, out var radioModel))
            {
                radioModel = RadioModelFactory.Instance.LoadRxOrDefaultIntercom(desiredName);
                RxRadioModels.Add(desiredName, radioModel);
            }

            // Create dry and wet providers
            // Wet/effected provider: must use a separate buffer to avoid double-reading
           
            var dryProvider = new TransmissionProvider(drySourceBuffer.Array, 0, drySourceBuffer.Length);

            using var wetSourceBuffer = new PooledArray<float>(audioOut.Length);
            var wetSpan = wetSourceBuffer.Array.AsSpan(0, wetSourceBuffer.Length);
            drySpan.CopyTo(wetSpan);
            var wetProvider = BuildRXPipeline(new TransmissionProvider(wetSourceBuffer.Array, 0, wetSourceBuffer.Length), radioModel);

            // Set up volume providers for wet/dry mix
            var dryVolume = new VolumeSampleProvider(dryProvider) { Volume = 1.0f - radioEffectRatio };
            var wetVolume = new VolumeSampleProvider(wetProvider) { Volume = radioEffectRatio };

            // Mix dry and wet
            var mixer = new MixingSampleProvider(new[] { dryVolume, wetVolume });

            // Wrap with ClippingProvider if needed
            ISampleProvider finalProvider = mixer;
            if (clippingEnabled && radioEffectRatio > 0f)
                finalProvider = new ClippingProvider(mixer, -1f, 1f);

            using var mixerBuffer = new PooledArray<float>(audioOut.Length);
            int samplesRead = 0;
            try
            {
                samplesRead = finalProvider.Read(mixerBuffer.Array, 0, mixerBuffer.Length);
                mixerBuffer.Array.AsSpan(0, samplesRead).CopyTo(audioOut);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to process segments: ");
                // Something borked - reset for the next packet(s).
                RxRadioModels.Clear();
            }
            
            return samplesRead;
        }

        internal void Mix(Span<float> target, ReadOnlySpan<float> source)
        {
            var vectorSize = Vector<float>.Count;
            var remainder = source.Length % vectorSize;


            for (var i = 0; i < source.Length - remainder; i += vectorSize)
            {
                var v_source = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(source), (nuint)i);
                var v_current = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(target), (nuint)i);

                (v_current + v_source).CopyTo(target.Slice(i, vectorSize));
            }

            for (var i = source.Length - remainder; i < source.Length; ++i)
            {
                target[i] += source[i];
            }
        }
    }
}