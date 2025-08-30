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
            foreach (var filter in Filters)
            {
                for (int i = 0; i < samplesRead; ++i)
                {
                    // ignore perfect silence.
                    if (buffer[offset + i] != 0)
                    {
                        buffer[offset + i] = filter.Transform(buffer[offset + i]);
                    }
                    
                }
            }

            return samplesRead;
        }
    }
}
