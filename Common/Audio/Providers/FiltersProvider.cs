using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class FiltersProvider : ISampleProvider
    {
        public Dsp.IFilter[] Filters { get; set; }
        ISampleProvider Source;

        public FiltersProvider(ISampleProvider source)
        {
            Source = source;
        }

        public WaveFormat WaveFormat => Source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = Source.Read(buffer, offset, count);
            for (int i = 0; i < count; ++i)
            {
                var source = buffer[offset + i];
                foreach (var filter in Filters)
                {
                    source = filter.Transform(source);
                }
                buffer[offset + i] = source;
            }

            return samplesRead;
        }
    }
}
