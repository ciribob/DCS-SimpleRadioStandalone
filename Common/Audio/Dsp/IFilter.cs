using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    [JsonConverter(typeof(JsonDspIFilterConverter))]
    internal interface IFilter
    {
        float Transform(float input);
    }
}
