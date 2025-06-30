using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

internal class AudioRecordingStreamMixer : AudioRecordingStream
{
    private readonly List<AudioRecordingStream> streams;

    public AudioRecordingStreamMixer(List<AudioRecordingStream> theStreams, string tag)
    {
        streams = theStreams;
        Tag = tag;
    }

    // return the minimum number of samples available across all streams associated with the
    // mixer. callers are responsible for serialization when Count() and Read() on any of the
    // component streams may be called concurrently.
    public override int Count()
    {
        var count = int.MaxValue;
        for (var i = 0; i < streams.Count(); i++) count = Math.Min(count, streams[i].Count());
        return count;
    }

    // read up to maxSampleCount samples from the streams that are being mixed and return them
    // through the samples array. returns the number of samples returned.
    //
    // to mix, we will find the minimum number of available samples across the streams we are
    // mixing and then compute the mix of that number of samples. this allows us to mix streams
    // that may not have their head at the same point in time. 
    public override int Read(float[] samples, int maxSampleCount)
    {
        var count = Math.Min(Count(), maxSampleCount);
        if (count > 0 && streams.Count() > 0)
        {
            var samplesSpan = samples.AsSpan(0, count);
            streams[0].Read(samples, count);

            var buffer = new float[maxSampleCount];
            var bufferSpan = buffer.AsSpan(0, count);
            for (var i = 1; i < streams.Count(); i++)
            {
                streams[i].Read(buffer, count);

                // to optimize copies, MixArraysNoClipping always mixes into the larger of the
                // two buffers (second if buffers are equal in size). samples (where we want
                // the output) is the second buffer to ensure results end up there.
                //
                // TODO: this is a bit fragile and dependent on specific implementation...
                //
                AudioManipulationHelper.MixArraysNoClipping(bufferSpan, samplesSpan);
            }

            AudioManipulationHelper.ClipArray(samplesSpan);
        }

        return count;
    }
}