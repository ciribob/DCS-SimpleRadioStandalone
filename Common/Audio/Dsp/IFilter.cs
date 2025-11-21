using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    internal interface IFilter
    {
        float Transform(float input);
    }
}
