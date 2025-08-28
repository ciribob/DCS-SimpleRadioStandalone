using OggVorbisEncoder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex
{
    public class JitterBuffer : IDisposable
    {
        public enum Status
        {
            OK = 0,
            MISSING = 1,
            INCOMPLETE = 2,
            INTERNAL_ERROR = -1,
            BAD_ARGUMENT = -2
        };

        public JitterBuffer(int ticks)
        {
            jitter = Native.jitter_buffer_init(ticks);
        }

        public void Reset()
        {
            Native.jitter_buffer_reset(jitter);
        }

        public void Put(ReadOnlySpan<byte> data, uint timestamp, uint timeSpan)
        {
            unsafe
            {
                fixed (byte* dataFixed = data)
                {
                    var packet = new Native.Packet
                    {
                        data = dataFixed,
                        len = (uint)data.Length,
                        timestamp = timestamp,
                        span = timeSpan
                    };

                    Native.jitter_buffer_put(jitter, ref packet);
                }
            }
        }

        public Status Get(Span<byte> bytes, int desired_span)
        {
            var result = Status.INTERNAL_ERROR;
            unsafe
            {
                fixed (byte* dataFixed = bytes)
                {
                    var packet = new Native.Packet
                    {
                        data = dataFixed,
                        len = (uint)bytes.Length
                    };

                    result = (Status)Native.jitter_buffer_get(jitter, ref packet, desired_span, out _);
                }
            }

            return result;
        }

        public void Tick()
        {
            Native.jitter_buffer_tick(jitter);
        }



        public void Dispose()
        {
            Native.jitter_buffer_destroy(jitter);
        }

        private IntPtr jitter;
        // https://speex.org/docs/api/speex-api-reference/group__JitterBuffer.html
        private static class Native
        {
            

            // CTLs
            public const int SET_MARGIN = 0;
            public const int GET_MARGIN = 1;
            public const int GET_AVAILABLE_COUNT = 3;
            [StructLayout(LayoutKind.Sequential)]
            public unsafe struct Packet
            {
                public byte* data;
                public uint len;
                public uint timestamp;
                public uint span;
            };

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr jitter_buffer_init(int step_size);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern void jitter_buffer_reset(IntPtr jitter);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern void jitter_buffer_destroy(IntPtr jitter);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern void jitter_buffer_put(IntPtr jitter, ref Packet packet);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern int jitter_buffer_get(IntPtr jitter, ref Packet packet, int desired_span, out int start_offset);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern int jitter_buffer_get_pointer_timestamp(IntPtr jitter);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern void jitter_buffer_tick(IntPtr jitter);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static unsafe extern int jitter_buffer_ctl(IntPtr jitter, int request, void* ptr);
        }
    }

}
