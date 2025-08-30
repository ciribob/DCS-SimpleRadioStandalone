using NAudio.Wave;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility.Speex
{
    /// <summary>
    ///     A preprocessor for microphone input which performs denoising and automatic gain control
    /// </summary>
    public sealed class Preprocessor
        : IDisposable
    {
        public Preprocessor(int frameSize, int sampleRate)
        {
            FrameSize = frameSize;
            Format = new WaveFormat(1, sampleRate);

            Reset();
        }

        /// <summary>
        ///     Process a frame of data captured from the microphone
        /// </summary>
        /// <param name="frame"></param>
        /// <returns>Returns true iff VAD is enabled and speech is detected</returns>
        public void Process(ArraySegment<short> frame)
        {
            if (frame.Count != FrameSize)
                throw new ArgumentException(
                    string.Format("Incorrect frame size, expected {0} but given {1}", FrameSize, frame.Count), "frame");

            using (var handle = frame.Pin())
            {
                DspNative.Methods.speex_preprocess_run(_preprocessor, handle.Ptr);
            }
        }

        public void Reset()
        {
            if (_preprocessor != IntPtr.Zero)
            {
                DspNative.Methods.speex_preprocess_state_destroy(_preprocessor);
                _preprocessor = IntPtr.Zero;
            }

            _preprocessor = DspNative.Methods.speex_preprocess_state_init(FrameSize, Format.SampleRate);
        }

        #region fields

        private IntPtr _preprocessor;

        public int FrameSize { get; }

        public WaveFormat Format { get; }

        #endregion

        #region denoise

        /// <summary>
        ///     Get or Set if denoise filter is enabled
        /// </summary>
        public bool Denoise
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_DENOISE) != 0;
            set
            {
                var input = value ? 1 : 0;
                CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_DENOISE, ref input);
            }
        }

        /// <summary>
        ///     Get or Set maximum attenuation of the noise in dB (negative number)
        /// </summary>
        public int DenoiseAttenuation
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_NOISE_SUPPRESS);
            set => CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_NOISE_SUPPRESS, ref value);
        }

        #endregion

        #region AGC

        public bool AutomaticGainControl
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC) != 0;
            set
            {
                var input = value ? 1 : 0;
                CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_AGC, ref input);
            }
        }

        public float AutomaticGainControlLevel
        {
            get => CTL_Float(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC_LEVEL);
            set => CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_AGC_LEVEL, ref value);
        }

        public int AutomaticGainControlTarget
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC_TARGET);
            set => CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_AGC_TARGET, ref value);
        }

        public int AutomaticGainControlMaxGain
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC_MAX_GAIN);
            set => CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_AGC_MAX_GAIN, ref value);
        }

        public int AutomaticGainControlIncrement
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC_INCREMENT);
            set => CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_AGC_INCREMENT, ref value);
        }

        public int AutomaticGainControlDecrement
        {
            get => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC_DECREMENT);
            set => CTL(DspNative.Ctl.SPEEX_PREPROCESS_SET_AGC_DECREMENT, ref value);
        }

        /// <summary>
        ///     Get the current amount of AGC applied (0-1 indicating none -> max)
        /// </summary>
        public float AutomaticGainControlCurrent => CTL_Int(DspNative.Ctl.SPEEX_PREPROCESS_GET_AGC_GAIN) / 100f;

        #endregion

        #region CTL

        private void CTL(DspNative.Ctl ctl, ref int value)
        {
            var code = DspNative.Methods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref value);
            if (code != 0)
                throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));
        }

        private void CTL(DspNative.Ctl ctl, ref float value)
        {
            var code = DspNative.Methods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref value);
            if (code != 0)
                throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));
        }

        private int CTL_Int(DspNative.Ctl ctl)
        {
            var result = 0;

            var code = DspNative.Methods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref result);
            if (code != 0)
                throw new InvalidOperationException(string.Format("Failed Speex CTL '{0}' Code='{1}'", ctl, code));

            return result;
        }

        private float CTL_Float(DspNative.Ctl ctl)
        {
            var result = 0f;

            var code = DspNative.Methods.speex_preprocess_ctl(_preprocessor, (int)ctl, ref result);
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
                DspNative.Methods.speex_preprocess_state_destroy(_preprocessor);
                _preprocessor = IntPtr.Zero;
            }

            _disposed = true;
        }

        #endregion
    }
}
