using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers
{
    internal class NoopSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat {  get; set; }

        public int Read(float[] buffer, int offset, int count)
        {
            return count;
        }
    }
}
