using System.Text.Json.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Dsp
{
    internal class BiQuadFilter : IFilter
    {
        public NAudio.Dsp.BiQuadFilter Filter { get; set; }
        public float Transform(float input)
        {
            return Filter.Transform(input);
        }
    }
}
