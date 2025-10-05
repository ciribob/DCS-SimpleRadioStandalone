using Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Concentus;
using Concentus.Oggfile;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording
{
    internal class AudioRecordingOpusWriter : AudioRecordingWriterBase
    {

        private readonly List<OpusOggWriteStream> _opusFileWriters = new();
        public AudioRecordingOpusWriter(IReadOnlyList<AudioRecordingStream> streams, int sampleRate, int maxSamples)
       : base(streams, sampleRate, maxSamples)
        {
        }

        public override void Start()
        {
            var now = DateTime.UtcNow;

            // Get User Setting for Recording Directory
            string recordingDir = GetRecordingDirectory();

            // streams are stored in User defined recordings directory, named "<date>-<time><tag>.opus"
            var sanitisedDate = string.Join("-", now.ToLocalTime().ToShortDateString().Split(Path.GetInvalidFileNameChars()));
            var sanitisedTime = string.Join("-", now.ToLocalTime().ToLongTimeString().Split(Path.GetInvalidFileNameChars()));
            var filePathBase = $"{recordingDir}\\{sanitisedDate}-{sanitisedTime}";

            var encodedBy = "SRS " + UpdaterChecker.VERSION;
            var trackTotal = Streams.Count.ToString();
            var dateStr = now.ToString("O", CultureInfo.InvariantCulture);
            for (var i = 0; i < Streams.Count; i++)
            {
                var encoder = OpusCodecFactory.CreateEncoder(48000, 1);

                var tag = Streams[i].Tag;

                if (tag == null || tag.Length == 0) tag = "";

                var tags = new OpusTags();

                tags.Fields[OpusTagName.Title] = tag;
                tags.Fields["encoded_by"] = encodedBy;
                tags.Fields["encoder"] = encodedBy;
                tags.Fields["tracknumber"] = (i + 1).ToString();
                tags.Fields["tracktotal"] = trackTotal;
                tags.Fields["date"] = dateStr;
                var path = filePathBase + tag + ".opus";

                var stream = new FileStream(path, FileMode.Create);
                _opusFileWriters.Add(new OpusOggWriteStream(encoder, stream, tags, SampleRate));
            }
        }

        public override void Stop()
        {
            foreach (var stream in _opusFileWriters)
            {
                stream.Finish();
            }
        }

        protected override void DoPrepareProcessAudio()
        {
            if (_opusFileWriters.Count == 0) Start();
        }

        protected override void DoProcessAudioStream(int streamIndex, ReadOnlySpan<float> samples)
        {
            var floatPool = ArrayPool<float>.Shared;
            var floatSamples = new float[samples.Length];
            samples.CopyTo(floatSamples);
            _opusFileWriters[streamIndex].WriteSamples(floatSamples, 0, samples.Length);
        }
    }
}
