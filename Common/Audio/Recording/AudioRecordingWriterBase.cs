using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NAudio.Wave;
using NLog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording
{
    internal abstract class AudioRecordingWriterBase
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly IReadOnlyList<AudioRecordingStream> _streams;
        private readonly WaveFormat _waveFormat;
        private readonly int _maxSamples;
        private readonly int _sampleRate;

        protected IReadOnlyList<AudioRecordingStream> Streams => _streams;
        protected WaveFormat WaveFormat => _waveFormat;
        protected int MaxSamples => _maxSamples;
        protected int SampleRate => _sampleRate;

        protected AudioRecordingWriterBase(IReadOnlyList<AudioRecordingStream> streams, int sampleRate, int maxSamples)
        {
            _streams = streams;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _sampleRate = sampleRate;
            _maxSamples = maxSamples;
        }

        public void ProcessAudio()
        {
            DoPrepareProcessAudio();
            try
            {
                // read from each of the streams and write the results to the file associated with
                // the stream. note that a "stream" here can be a mix of multiple audio streams.
                var floatPool = ArrayPool<float>.Shared;
                var streamBuffer = floatPool.Rent(MaxSamples);
                for (var i = 0; i < Streams.Count; i++)
                {
                    var samplesRead = Streams[i].Read(streamBuffer, MaxSamples);

                    DoProcessAudioStream(i, streamBuffer.AsSpan(0, samplesRead));
                }

                floatPool.Return(streamBuffer);
            }
            catch (Exception ex)
            {
                _logger.Error($"Unable to write audio samples to output file: {ex.Message}");
            }
        }
        public abstract void Start();
        public abstract void Stop();

        protected abstract void DoPrepareProcessAudio();
        protected abstract void DoProcessAudioStream(int streamIndex, ReadOnlySpan<float> samples);
        public string GetRecordingDirectory()
        {
            string dir = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.RecordingPath).StringValue;
            if (string.IsNullOrEmpty(dir))
                return Directory.GetCurrentDirectory();

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                _logger.Info($"Recording directory '{dir}' missing, creating directory");
                try
                {
                    Directory.CreateDirectory(dir);
                    _logger.Info("Directory Created!");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to create recording directory '{dir}': {ex.Message}");
                    return Directory.GetCurrentDirectory(); //return CurrentDirectory if we can't create the user specified one
                }
            }

            return dir;
        }
    }
}