using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    internal class JsonDspIFilterConverter : JsonConverter<IFilter>
    {
        private static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>()
        {
            "lowpass", "highpass", "peak",
        }.ToFrozenSet();
        public override IFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            string type = null;
            float? frequency = null;

            float? q = null;
            float? gain = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (!frequency.HasValue)
                    {
                        throw new JsonException($"Filter must have a frequency set.");
                    }

                    if (type == null)
                    {
                        throw new JsonException($"Filter must have a type property defined.");
                    }


                    // BiQuads.
                    if (q.HasValue)
                    {
                        switch (type)
                        {
                            case "lowpass":
                                return new BiQuadFilter
                                {
                                    Filter = NAudio.Dsp.BiQuadFilter.LowPassFilter(Constants.OUTPUT_SAMPLE_RATE, frequency.Value, q.Value)
                                };
                            case "highpass":
                                return new BiQuadFilter
                                {
                                    Filter = NAudio.Dsp.BiQuadFilter.HighPassFilter(Constants.OUTPUT_SAMPLE_RATE, frequency.Value, q.Value)
                                };
                            case "peak":
                                if (!gain.HasValue)
                                {
                                    throw new JsonException("peak must have a gain property set.");
                                }

                                return new BiQuadFilter
                                {
                                    Filter = NAudio.Dsp.BiQuadFilter.PeakingEQ(Constants.OUTPUT_SAMPLE_RATE, frequency.Value, q.Value, gain.Value)
                                };
                        }
                    }
                    else
                    {
                        // First order.

                        switch (type)
                        {
                            case "lowpass":
                                return FirstOrderFilter.LowPass(Constants.OUTPUT_SAMPLE_RATE, frequency.Value);
                            case "highpass":
                                return FirstOrderFilter.HighPass(Constants.OUTPUT_SAMPLE_RATE, frequency.Value);
                        }
                    }
                    throw new JsonException();
                }

                // Get the key.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                string propertyName = reader.GetString().ToLowerInvariant();
                switch (propertyName)
                {
                    case "q":
                        reader.Read();
                        q = reader.GetSingle();
                        if (q <= 0)
                        {
                            throw new JsonException($"Q factor must be positive.");
                        }
                        break;
                    case "frequency":
                        reader.Read();
                        frequency = reader.GetSingle();
                        if (frequency <= 0)
                        {
                            throw new JsonException($"frequency must be positive.");
                        }
                        break;
                    case "$type":
                        reader.Read();
                        type = reader.GetString().ToLowerInvariant();
                        if (!ValidTypes.Contains(type))
                        {
                            throw new JsonException($"Unexpected type \"{type}\"");
                        }
                        break;
                    case "gain":
                        reader.Read();
                        gain = reader.GetSingle();
                        break;
                    default:
                        throw new JsonException($"Unexpected property \"{propertyName}\"");
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, IFilter value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
