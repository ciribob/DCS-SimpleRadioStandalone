﻿using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NAudio.Wave;
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

        private float radioEffectsAmount = 1.0f; // Default to 1.0 (full effect)
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
            LoadRadioModels();
        }

        private class RadioModel
        {
            public DeferredSourceProvider RxSource { get; } = new DeferredSourceProvider();

            public ISampleProvider RxEffectProvider { get; set; }

            public RadioModel(Models.Dto.RadioModel dtoPreset)
            {
                RxEffectProvider = dtoPreset.RxEffect.ToSampleProvider(RxSource);
            }
        }

        private IReadOnlyDictionary<string, RadioModel> RadioModels;

        private readonly RadioModel Arc210 = new RadioModel(DefaultRadioModels.BuildArc210());
        private readonly RadioModel Intercom = new RadioModel(DefaultRadioModels.BuildIntercom());
        private void LoadRadioModels()
        {
            var modelsFolders = new List<string> { ModelsFolder, ModelsCustomFolder };
            var loadedModels = new Dictionary<string, RadioModel>();

            var deserializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // "propertyName" (starts lowercase)
                AllowTrailingCommas = true, // 
                ReadCommentHandling = JsonCommentHandling.Skip, // Allow comments but ignore them.
            };


            foreach (var modelsFolder in modelsFolders)
            {
                try
                {
                    var models = Directory.EnumerateFiles(modelsFolder, "*.json");
                    foreach (var modelFile in models)
                    {
                        var modelName = Path.GetFileNameWithoutExtension(modelFile).ToLowerInvariant();
                        using (var jsonFile = File.OpenRead(modelFile))
                        {
                            try
                            {
                                var loadedModel = JsonSerializer.Deserialize<Models.Dto.RadioModel>(jsonFile, deserializerOptions);
                                loadedModels[modelName] = new RadioModel(loadedModel);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Unable to parse radio preset file {modelFile}", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Unable to parse radio preset files {modelsFolder}", ex);
                }
            }
                
            RadioModels = loadedModels.ToFrozenDictionary();
        }

        private void RefreshSettings()
        {
            //only get settings every 3 seconds - and cache them - issues with performance
            long now = DateTime.Now.Ticks;

            if (TimeSpan.FromTicks(now - lastRefresh).TotalSeconds > 3)
            {
                var profileSettings = GlobalSettingsStore.Instance.ProfileSettingsStore;
                var serverSettings = SyncedServerSettings.Instance;
                lastRefresh = now;

                perRadioModelEffect = profileSettings.GetClientSettingBool(ProfileSettingsKeys.PerRadioModelEffects);
                irlRadioRXInterference = serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
                clippingEnabled = profileSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
                radioEffectsAmount = Math.Clamp(profileSettings.GetClientSettingFloat(ProfileSettingsKeys.RadioEffectsAmount), 0f, 2f);
            }
        }

        private ISampleProvider BuildRXPipeline(ISampleProvider voiceProvider, RadioModel radioModel)
        {
            radioModel.RxSource.Source = voiceProvider;
            voiceProvider = radioModel.RxEffectProvider;
            return voiceProvider;
        }

        public int ProcessSegments(float[] mixBuffer, int offset, int count, IReadOnlyList<TransmissionSegment> segments, string modelName = null)
        {
            RefreshSettings();
            var floatPool = ArrayPool<float>.Shared;
            var workingBuffer = floatPool.Rent(count);
            var workingSpan = workingBuffer.AsSpan(0, count);
            workingSpan.Clear();
            
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
                    Mix(workingSpan, segment.AudioSpan);
                }
            }

            if (capturedFMSegment != null)
            {
                // Use the last one (highest power).
                Mix(workingSpan, capturedFMSegment.AudioSpan);
            }

            ISampleProvider provider = new TransmissionProvider(workingBuffer, 0, count);

            if (perRadioModelEffect && modelName != null)
                provider = BuildRXPipeline(provider, RadioModels.GetValueOrDefault(modelName, Intercom));
            else
                provider = BuildRXPipeline(provider, Intercom);

            if (clippingEnabled)
                provider = new ClippingProvider(provider, -1f, 1f);

            int samplesRead = provider.Read(mixBuffer, offset, count);

            // Apply effect strength (wet/dry mix)
            if (Math.Abs(radioEffectsAmount - 1.0f) > 0.0001f)
            {
                for (int i = offset; i < offset + samplesRead; i++)
                    mixBuffer[i] *= radioEffectsAmount;
            }

            floatPool.Return(workingBuffer);
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