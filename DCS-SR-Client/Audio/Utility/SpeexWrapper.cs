using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility
{
    //From https://github.com/mischa/HelloVR/blob/1796d2607f1f583d2669f005839e494511b2b83b/Assets/Plugins/Dissonance/Core/Audio/Capture/SpeexDspNative.cs
    internal static class SpeexDspNative
    {
        private static class SpeexDspNativeMethods
        {
            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr speex_preprocess_state_init(int frameSize, int sampleRate);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern int speex_preprocess_ctl(IntPtr st, int id, ref int val);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern int speex_preprocess_ctl(IntPtr st, int id, ref float val);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern int speex_preprocess_run(IntPtr st, IntPtr ptr);

            [DllImport("speexdsp", CallingConvention = CallingConvention.Cdecl)]
            public static extern void speex_preprocess_state_destroy(IntPtr st);
        }

        private enum SpeexDspCtl
        {
            // ReSharper disable InconsistentNaming
            // ReSharper disable UnusedMember.Local

            /** Set preprocessor denoiser state */
            SPEEX_PREPROCESS_SET_DENOISE = 0,
            /** Get preprocessor denoiser state */
            SPEEX_PREPROCESS_GET_DENOISE = 1,

            /** Set preprocessor Automatic Gain Control state */
            SPEEX_PREPROCESS_SET_AGC = 2,
            /** Get preprocessor Automatic Gain Control state */
            SPEEX_PREPROCESS_GET_AGC = 3,

            ///** Set preprocessor Voice Activity Detection state */
            //SPEEX_PREPROCESS_SET_VAD = 4,
            ///** Get preprocessor Voice Activity Detection state */
            //SPEEX_PREPROCESS_GET_VAD = 5,

            /** Set preprocessor Automatic Gain Control level (float) */
            SPEEX_PREPROCESS_SET_AGC_LEVEL = 6,
            /** Get preprocessor Automatic Gain Control level (float) */
            SPEEX_PREPROCESS_GET_AGC_LEVEL = 7,

            //Dereverb is disabled in the preprocessor!
            ///** Set preprocessor Dereverb state */
            //SPEEX_PREPROCESS_SET_DEREVERB = 8,
            ///** Get preprocessor Dereverb state */
            //SPEEX_PREPROCESS_GET_DEREVERB = 9,

            ///** Set probability required for the VAD to go from silence to voice */
            //SPEEX_PREPROCESS_SET_PROB_START = 14,
            ///** Get probability required for the VAD to go from silence to voice */
            //SPEEX_PREPROCESS_GET_PROB_START = 15,

            ///** Set probability required for the VAD to stay in the voice state (integer percent) */
            //SPEEX_PREPROCESS_SET_PROB_CONTINUE = 16,
            ///** Get probability required for the VAD to stay in the voice state (integer percent) */
            //SPEEX_PREPROCESS_GET_PROB_CONTINUE = 17,

            /** Set maximum attenuation of the noise in dB (negative number) */
            SPEEX_PREPROCESS_SET_NOISE_SUPPRESS = 18,
            /** Get maximum attenuation of the noise in dB (negative number) */
            SPEEX_PREPROCESS_GET_NOISE_SUPPRESS = 19,

            ///** Set maximum attenuation of the residual echo in dB (negative number) */
            //SPEEX_PREPROCESS_SET_ECHO_SUPPRESS = 20,
            ///** Get maximum attenuation of the residual echo in dB (negative number) */
            //SPEEX_PREPROCESS_GET_ECHO_SUPPRESS = 21,

            ///** Set maximum attenuation of the residual echo in dB when near end is active (negative number) */
            //SPEEX_PREPROCESS_SET_ECHO_SUPPRESS_ACTIVE = 22,
            ///** Get maximum attenuation of the residual echo in dB when near end is active (negative number) */
            //SPEEX_PREPROCESS_GET_ECHO_SUPPRESS_ACTIVE = 23,

            ///** Set the corresponding echo canceller state so that residual echo suppression can be performed (NULL for no residual echo suppression) */
            //SPEEX_PREPROCESS_SET_ECHO_STATE = 24,
            ///** Get the corresponding echo canceller state */
            //SPEEX_PREPROCESS_GET_ECHO_STATE = 25,

            /** Set maximal gain increase in dB/second (int32) */
            SPEEX_PREPROCESS_SET_AGC_INCREMENT = 26,

            /** Get maximal gain increase in dB/second (int32) */
            SPEEX_PREPROCESS_GET_AGC_INCREMENT = 27,

            /** Set maximal gain decrease in dB/second (int32) */
            SPEEX_PREPROCESS_SET_AGC_DECREMENT = 28,

            /** Get maximal gain decrease in dB/second (int32) */
            SPEEX_PREPROCESS_GET_AGC_DECREMENT = 29,

            /** Set maximal gain in dB (int32) */
            SPEEX_PREPROCESS_SET_AGC_MAX_GAIN = 30,

            /** Get maximal gain in dB (int32) */
            SPEEX_PREPROCESS_GET_AGC_MAX_GAIN = 31,

            /*  Can't set loudness */
            /** Get loudness */
            SPEEX_PREPROCESS_GET_AGC_LOUDNESS = 33,

            /*  Can't set gain */
            /** Get current gain (int32 percent) */
            SPEEX_PREPROCESS_GET_AGC_GAIN = 35,

            /*  Can't set spectrum size */
            /** Get spectrum size for power spectrum (int32) */
            SPEEX_PREPROCESS_GET_PSD_SIZE = 37,

            /*  Can't set power spectrum */
            /** Get power spectrum (int32[] of squared values) */
            SPEEX_PREPROCESS_GET_PSD = 39,

            /*  Can't set noise size */
            /** Get spectrum size for noise estimate (int32)  */
            SPEEX_PREPROCESS_GET_NOISE_PSD_SIZE = 41,

            /*  Can't set noise estimate */
            /** Get noise estimate (int32[] of squared values) */
            SPEEX_PREPROCESS_GET_NOISE_PSD = 43,

            /* Can't set speech probability */
            /** Get speech probability in last frame (int32).  */
            SPEEX_PREPROCESS_GET_PROB = 45,

            /** Set preprocessor Automatic Gain Control level (int32) */
            SPEEX_PREPROCESS_SET_AGC_TARGET = 46,
            /** Get preprocessor Automatic Gain Control level (int32) */
            SPEEX_PREPROCESS_GET_AGC_TARGET = 47

            // ReSharper restore UnusedMember.Local
            // ReSharper restore InconsistentNaming
        }

        /// <summary>
        /// A preprocessor for microphone input which performs denoising and automatic gain control
        /// </summary>
        public sealed class Preprocessor
            : IDisposable
        {
            #region fields
            private IntPtr _preprocessor;

            private readonly int _frameSize;
            public int FrameSize
            {
                get { return _frameSize; }
            }

            private readonly WaveFormat _format;
            public WaveFormat Format
            {
                get { return _format; }
            }
            #endregion

            #region denoise
            /// <summary>
            /// Get or Set if denoise filter is enabled
            /// </summary>
            public bool Denoise
            {
                get
                {
                    return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_DENOISE) != 0;
                }
                set
                {
                    var input = value ? 1 : 0;
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_DENOISE, ref input);
                }
            }

            /// <summary>
            /// Get or Set maximum attenuation of the noise in dB (negative number)
            /// </summary>
            public int DenoiseAttenuation
            {
                get
                {
                    return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_NOISE_SUPPRESS);
                }
                set
                {
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_NOISE_SUPPRESS, ref value);
                }
            }
            #endregion

            #region AGC
            public bool AutomaticGainControl
            {
                get { return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC) != 0; }
                set
                {
                    var input = value ? 1 : 0;
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_AGC, ref input);
                }
            }

            public float AutomaticGainControlLevel
            {
                get
                {
                    return CTL_Float(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC_LEVEL);
                }
                set
                {
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_AGC_LEVEL, ref value);
                }
            }

            public int AutomaticGainControlTarget
            {
                get
                {
                    return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC_TARGET);
                }
                set
                {
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_AGC_TARGET, ref value);
                }
            }

            public int AutomaticGainControlLevelMax
            {
                get
                {
                    return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC_MAX_GAIN);
                }
                set
                {
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_AGC_MAX_GAIN, ref value);
                }
            }

            public int AutomaticGainControlIncrement
            {
                get
                {
                    return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC_INCREMENT);
                }
                set
                {
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_AGC_INCREMENT, ref value);
                }
            }

            public int AutomaticGainControlDecrement
            {
                get
                {
                    return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC_DECREMENT);
                }
                set
                {
                    CTL(SpeexDspCtl.SPEEX_PREPROCESS_SET_AGC_DECREMENT, ref value);
                }
            }

            /// <summary>
            /// Get the current amount of AGC applied (0-1 indicating none -> max)
            /// </summary>
            public float AutomaticGainControlCurrent
            {
                get { return CTL_Int(SpeexDspCtl.SPEEX_PREPROCESS_GET_AGC_GAIN) / 100f; }
            }
            #endregion

            public Preprocessor(int frameSize, int sampleRate)
            {
                _frameSize = frameSize;
                _format = new WaveFormat(1, sampleRate);

                Reset();

                var settingsStore = SettingsStore.Instance;

                //From https://github.com/mumble-voip/mumble/blob/a189969521081565b8bda93d253670370778d471/src/mumble/Settings.cpp
                //and  https://github.com/mumble-voip/mumble/blob/3ffd9ad3ed18176774d8e1c64a96dffe0de69655/src/mumble/AudioInput.cpp#L605
                AutomaticGainControl = settingsStore.GetClientSetting(SettingsKeys.AGC).BoolValue;
                AutomaticGainControlTarget = settingsStore.GetClientSetting(SettingsKeys.AGCTarget).IntValue;
                AutomaticGainControlDecrement = settingsStore.GetClientSetting(SettingsKeys.AGCDecrement).IntValue;
                AutomaticGainControlLevelMax = settingsStore.GetClientSetting(SettingsKeys.AGCLevelMax).IntValue;

                Denoise = settingsStore.GetClientSetting(SettingsKeys.Denoise).BoolValue;
                DenoiseAttenuation = settingsStore.GetClientSetting(SettingsKeys.DenoiseAttenuation).IntValue;

            }

            /// <summary>
            /// Process a frame of data captured from the microphone
            /// </summary>
            /// <param name="frame"></param>
            /// <returns>Returns true iff VAD is enabled and speech is detected</returns>
            public void Process(ArraySegment<short> frame)
            {
                if (frame.Count != _frameSize)
                    throw new ArgumentException(string.Format("Incorrect frame size, expected {0} but given {1}", _frameSize, frame.Count), "frame");

                using (var handle = frame.Pin())
                    SpeexDspNativeMethods.speex_preprocess_run(_preprocessor, handle.Ptr);
            }

            public void Reset()
            {
                if (_preprocessor != IntPtr.Zero)
                {
                    SpeexDspNativeMethods.speex_preprocess_state_destroy(_preprocessor);
                    _preprocessor = IntPtr.Zero;
                }

                _preprocessor = SpeexDspNativeMethods.speex_preprocess_state_init(_frameSize, _format.SampleRate);
            }

            #region CTL
            private void CTL(SpeexDspCtl ctl, ref int value)
            {
                var code = SpeexDspNativeMethods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref value);
                if (code != 0)
                    throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));
            }

            private void CTL(SpeexDspCtl ctl, ref float value)
            {
                var code = SpeexDspNativeMethods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref value);
                if (code != 0)
                    throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));
            }

            private int CTL_Int(SpeexDspCtl ctl)
            {
                var result = 0;

                var code = SpeexDspNativeMethods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref result);
                if (code != 0)
                    throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));

                return result;
            }

            private float CTL_Float(SpeexDspCtl ctl)
            {
                var result = 0f;

                var code = SpeexDspNativeMethods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref result);
                if (code != 0)
                    throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));

                return result;
            }
            #endregion

            #region disposal
            ~Preprocessor()
            {
                Dispose();
            }

            private bool _disposed;
            public void Dispose()
            {
                if (_disposed)
                    return;

                GC.SuppressFinalize(this);

                if (_preprocessor != IntPtr.Zero)
                {
                    SpeexDspNativeMethods.speex_preprocess_state_destroy(_preprocessor);
                    _preprocessor = IntPtr.Zero;
                }

                _disposed = true;
            }
            #endregion
        }
    }

    public class Preprocessor
        : IDisposable
    {
        private readonly SpeexDspNative.Preprocessor _preprocessor;


        public Preprocessor(int frameSize, int sampleRate)
        {
            _preprocessor = new SpeexDspNative.Preprocessor(frameSize, sampleRate);


        }



        public void Process(ArraySegment<short> frame)
        {
            _preprocessor.Process(frame);
        }

        public void Reset()
        {
            _preprocessor.Reset();
        }

        public void Dispose()
        {
            _preprocessor.Dispose();
        }
    }

    //for the Pin for the arrays
    internal static class ArraySegmentExtensions
    {
        /// <summary>
        /// Copy from the given array segment into the given array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="segment"></param>
        /// <param name="destination"></param>
        /// <param name="destinationOffset"></param>
        /// <returns>The segment of the destination array which was written into</returns>
        internal static ArraySegment<T> CopyTo<T>(this ArraySegment<T> segment, T[] destination, int destinationOffset = 0)
            where T : struct
        {
            if (segment.Count > destination.Length - destinationOffset)
                throw new ArgumentException("Insufficient space in destination array", "destination");

            Buffer.BlockCopy(segment.Array, segment.Offset, destination, destinationOffset, segment.Count);

            return new ArraySegment<T>(destination, destinationOffset, segment.Count);
        }

        /// <summary>
        /// Copy as many samples as possible from the source array into the segment
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="segment"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        internal static int CopyFrom<T>(this ArraySegment<T> segment, T[] source)
        {
            var count = Math.Min(segment.Count, source.Length);
            Array.Copy(source, 0, segment.Array, segment.Offset, count);
            return count;
        }

        internal static void Clear<T>(this ArraySegment<T> segment)
        {
            Array.Clear(segment.Array, segment.Offset, segment.Count);
        }

        /// <summary>
        /// Pin the array and return a pointer to the start of the segment
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="segment"></param>
        /// <returns></returns>
        internal static DisposableHandle Pin<T>(this ArraySegment<T> segment) where T : struct
        {
            var handle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            var ptr = new IntPtr(handle.AddrOfPinnedObject().ToInt64() + segment.Offset * Marshal.SizeOf(typeof(T)));

            return new DisposableHandle(ptr, handle);
        }

        internal struct DisposableHandle
            : IDisposable
        {
            private readonly IntPtr _ptr;
            private readonly GCHandle _handle;

            public IntPtr Ptr
            {
                get
                {
                    if (!_handle.IsAllocated)
                        throw new ObjectDisposedException("GC Handle has already been freed");
                    return _ptr;
                }
            }

            internal DisposableHandle(IntPtr ptr, GCHandle handle)
            {
                _ptr = ptr;
                _handle = handle;
            }

            public void Dispose()
            {
                _handle.Free();
            }
        }
    }
}
