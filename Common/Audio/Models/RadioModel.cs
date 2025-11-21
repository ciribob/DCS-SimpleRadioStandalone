using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models.Dto;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Models
{
    namespace Dto
    {
        [JsonConverter(typeof(JsonIFilterConverter))]

        internal abstract class IFilter
        {
            public required float Frequency { get; set; }
            public abstract Dsp.IFilter ToFilter();
        };

        internal class FirstOrderLowPassFilter : IFilter
        {
            public override Dsp.IFilter ToFilter()
            {
                return FirstOrderFilter.LowPass(Constants.OUTPUT_SAMPLE_RATE, Frequency);
            }
        }

        internal class FirstOrderHighPassFilter : IFilter
        {
            public override Dsp.IFilter ToFilter()
            {
                return FirstOrderFilter.HighPass(Constants.OUTPUT_SAMPLE_RATE, Frequency);
            }
        }

        internal abstract class IBiQuadFilter : IFilter
        {
            public required float Q { get; set; }
        }

        internal class BiQuadHighPassFilter : IBiQuadFilter
        {
            public override Dsp.IFilter ToFilter()
            {
                return new Dsp.BiQuadFilter
                {
                    Filter = NAudio.Dsp.BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, Frequency, Q)
                };
            }
        };

        

        internal class BiQuadLowPassFilter : IBiQuadFilter
        {
            public override Dsp.IFilter ToFilter()
            {
                return new Dsp.BiQuadFilter
                {
                    Filter = NAudio.Dsp.BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, Frequency, Q)
                };
            }
        };

        internal class BiQuadPeakingEQFilter : IBiQuadFilter
        {
            public required float Gain { get; set; }

            public override Dsp.IFilter ToFilter()
            {
                return new Dsp.BiQuadFilter
                {
                    Filter = NAudio.Dsp.BiQuadFilter.PeakingEQ(Constants.OUTPUT_SAMPLE_RATE, Frequency, Q, Gain)
                };
            }
        };

        [JsonDerivedType(typeof(ChainEffect), typeDiscriminator: "chain")]
        [JsonDerivedType(typeof(FiltersEffect), typeDiscriminator: "filters")]

        [JsonDerivedType(typeof(SaturationEffect), typeDiscriminator: "saturation")]
        [JsonDerivedType(typeof(CompressorEffect), typeDiscriminator: "compressor")]
        [JsonDerivedType(typeof(SidechainCompressorEffect), typeDiscriminator: "sidechainCompressor")]
        [JsonDerivedType(typeof(GainEffect), typeDiscriminator: "gain")]

        [JsonDerivedType(typeof(CVSDEffect), typeDiscriminator: "cvsd")]
        internal abstract class IEffect
        {
            public abstract ISampleProvider ToSampleProvider(ISampleProvider source);
        };

        internal class CVSDEffect : IEffect
        {
            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new CVSDProvider(source);
            }
        };

        internal class ChainEffect : IEffect
        {
            public required IEffect[] Effects { get; set; }
            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                var last = source;
                foreach (var effect in Effects)
                {
                    last = effect.ToSampleProvider(last);
                }
                return last;
            }
        };

        internal class FiltersEffect : IEffect
        {
            public required IFilter[] Filters { get; set; }
            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                var filters = new Dsp.IFilter[Filters.Length];
                for (var i = 0; i < Filters.Length; ++i)
                {
                    filters[i] = Filters[i].ToFilter();
                }
                return new FiltersProvider(source)
                {
                    Filters = filters
                };
            }
        };

        internal class SaturationEffect : IEffect
        {
            public required float Gain { get; set; }
            public required float Threshold { get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new SaturationProvider(source)
                {
                    GainDB = Gain,
                    ThresholdDB = Threshold
                };
            }
        };

        internal class CompressorEffect : IEffect
        {
            public required float Attack { get; set; }
            public required float MakeUp { get; set; }
            public required float Release { get; set; }
            public required float Threshold { get; set; }
            public required float Ratio { get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new SimpleCompressorEffect(source)
                {
                    Attack = Attack * 1000,
                    MakeUpGain = MakeUp,
                    Release = Release * 1000,
                    Threshold = Threshold,
                    Ratio = Ratio,
                    Enabled = true,
                };
            }
        };

        internal class SidechainCompressorEffect : IEffect
        {
            public required float Attack { get; set; }
            public required float MakeUp { get; set; }
            public required float Release { get; set; }
            public required float Threshold { get; set; }
            public required float Ratio { get; set; }
            public required IEffect SidechainEffect {  get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new SidechainCompressorProvider
                {
                    Compressor = new Dsp.SidechainCompressor(Attack * 1000, Release * 1000, source.WaveFormat.SampleRate)
                    {
                        MakeUpGain = MakeUp,
                        Threshold = Threshold,
                        Ratio = Ratio,
                    },
                    SignalProvider = source,
                    SidechainProvider = SidechainEffect.ToSampleProvider(new NoopSampleProvider()
                    {
                        WaveFormat = source.WaveFormat
                    })
                };
            }
        }

        internal class GainEffect : IEffect
        {
            public required float Gain {  get; set; }

            public override ISampleProvider ToSampleProvider(ISampleProvider source)
            {
                return new VolumeSampleProvider(source)
                {
                    Volume = (float)Decibels.DecibelsToLinear(Gain),
                };
            }
        };

        internal class RadioModel
        {
            public required int Version { get; set; }
            public required float NoiseGain { get; set; }
            public required IEffect TxEffect { get; set; }
            public required IEffect RxEffect { get; set; }
            public IEffect EncryptionEffect { get; set; }

        };
    }


    internal class TxRadioModel
    {
        public DeferredSourceProvider TxSource { get; } = new DeferredSourceProvider();

        public ISampleProvider TxEffectProvider { get; init; }

        public ISampleProvider EncryptionProvider { get; init; }

        public float NoiseGain { get; init; }

        public TxRadioModel(Models.Dto.RadioModel dtoPreset)
        {
            TxEffectProvider = dtoPreset.TxEffect.ToSampleProvider(TxSource);

            if (dtoPreset.EncryptionEffect != null)
            {
                EncryptionProvider = dtoPreset.EncryptionEffect.ToSampleProvider(TxEffectProvider);
            }

            NoiseGain = dtoPreset.NoiseGain;
        }
    }

    internal class RxRadioModel
    {
        public DeferredSourceProvider RxSource { get; } = new DeferredSourceProvider();

        public ISampleProvider RxEffectProvider { get; init; }

        public RxRadioModel(Models.Dto.RadioModel dtoPreset)
        {
            RxEffectProvider = dtoPreset.RxEffect.ToSampleProvider(RxSource);
        }
    }

    internal class RadioModelFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private IReadOnlyDictionary<string, RadioModel> RadioModelTemplates { get; set; }
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

        public static RadioModelFactory Instance = new();

        private RadioModelFactory()
        {
            LoadTemplates();
        }
        private void LoadTemplates()
        {
            var modelsFolders = new List<string> { ModelsFolder, ModelsCustomFolder };
            var loadedTemplates = new Dictionary<string, RadioModel>();

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
                                loadedTemplates[modelName] = JsonSerializer.Deserialize<RadioModel>(jsonFile, deserializerOptions);
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

            RadioModelTemplates = loadedTemplates.ToFrozenDictionary();
        }

        public TxRadioModel LoadTxRadio(string name)
        {
            if (RadioModelTemplates.TryGetValue(name, out var template))
            {
                return new(template);
            }

            return null;
        }

        public TxRadioModel LoadTxOrDefaultRadio(string name)
        {
            var model = LoadTxRadio(name);
            if (model == null)
            {
                model = new(DefaultRadioModels.BuildArc210());
            }

            return model;
        }

        public TxRadioModel LoadTxOrDefaultIntercom(string name)
        {
            var model = LoadTxRadio(name);
            if (model == null)
            {
                model = new(DefaultRadioModels.BuildIntercom());
            }

            return model;
        }

        public RxRadioModel LoadRxRadio(string name)
        {
            if (RadioModelTemplates.TryGetValue(name, out var template))
            {
                return new(template);
            }

            return null;
        }

        public RxRadioModel LoadRxOrDefaultIntercom(string name)
        {
            var model = LoadRxRadio(name);
            if (model == null)
            {
                model = new(DefaultRadioModels.BuildIntercom());
            }

            return model;
        }
    }

    internal class DefaultRadioModels
    {
        // ARC-210 as default radio FX.
        public static Dto.RadioModel BuildArc210() => new()
        {
            Version = 1,
            TxEffect = new ChainEffect()
            {
                Effects = new IEffect[]
                {
                    new FiltersEffect()
                    {
                        Filters = new Dto.IFilter[]
                        {
                            new BiQuadHighPassFilter
                            {
                                Frequency = 1700,
                                Q = 0.53f
                            },
                            new BiQuadPeakingEQFilter
                            {
                                Frequency = 2801,
                                Q = 0.5f,
                                Gain = 5f
                            },
                            new FirstOrderLowPassFilter
                            {
                                Frequency = 5538
                            }
                        }
                    },

                    new SaturationEffect()
                    {
                        Gain = 9,
                        Threshold = -23,
                    },

                    new SidechainCompressorEffect()
                    {
                        Attack = 0.01f,
                        MakeUp = 6,
                        Release = 0.2f,
                        Threshold = -33,
                        Ratio = 1.18f,
                        SidechainEffect = new FiltersEffect
                        {
                            Filters = new[]
                            {
                                new FirstOrderHighPassFilter
                                {
                                    Frequency = 709
                                }
                            }
                        }
                    },
                    new FiltersEffect()
                    {
                        Filters = new Dto.IFilter[]
                        {
                            new BiQuadHighPassFilter
                            {
                                Frequency = 456,
                                Q = 0.36f
                            },
                            new BiQuadLowPassFilter
                            {
                                Frequency = 5435,
                                Q = 0.39f
                            }
                        }
                    },
                    new GainEffect()
                    {
                        Gain = 12,
                    }

                }

            },

            RxEffect = new FiltersEffect()
            {
                Filters = new Dto.IFilter[]
                {
                    new FirstOrderHighPassFilter
                    {
                        Frequency = 270,
                    },
                    new FirstOrderLowPassFilter
                    {
                        Frequency = 4500
                    }
                },
            },

            EncryptionEffect = new CVSDEffect(),

            NoiseGain = -33,
        };

        public static Dto.RadioModel BuildIntercom() => new()
        {
            Version = 1,
            TxEffect = new ChainEffect()
            {
                Effects = new IEffect[]
                {
                    new FiltersEffect()
                    {
                        Filters = new Dto.IFilter[]
                        {
                            new BiQuadHighPassFilter
                            {
                                Frequency = 207,
                                Q = 0.5f
                            },
                            new BiQuadPeakingEQFilter
                            {
                                Frequency = 3112,
                                Q = 0.4f,
                                Gain = 16f
                            },
                            new BiQuadLowPassFilter
                            {
                                Frequency = 6036,
                                Q = 0.4f
                            },
                            new FirstOrderLowPassFilter
                            {
                                Frequency = 5538
                            }
                        },
                    },

                    new SaturationEffect
                    {
                        Gain = 2,
                        Threshold = -33,
                    },

                    new SidechainCompressorEffect
                    {
                        Attack = 0.01f,
                        MakeUp = -1,
                        Release = 0.2f,
                        Threshold = -17,
                        Ratio = 1.18f,
                        SidechainEffect = new FiltersEffect
                        {
                            Filters = new Dto.IFilter[]
                            {
                                new FirstOrderHighPassFilter
                                {
                                    Frequency = 709
                                }
                            }
                        }
                    },

                    new FiltersEffect
                    {
                        Filters = new Dto.IFilter[]
                        {
                            new BiQuadHighPassFilter
                            {
                                Frequency = 393,
                                Q =  0.43f
                            },
                            new BiQuadLowPassFilter
                            {
                                Frequency = 4875,
                                Q = 0.3f
                            }
                        },
                    },

                    new GainEffect
                    {
                        Gain = 8,
                    }
                }
            },

            RxEffect = new FiltersEffect()
            {
                Filters = new Dto.IFilter[]
                {
                    new FirstOrderHighPassFilter
                    {
                        Frequency = 270,
                    },
                    new FirstOrderLowPassFilter
                    {
                        Frequency = 4500
                    }
                },
            },

            NoiseGain = -60,
        };
    };
}
