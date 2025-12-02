using NAudio.Wave;
using System;
using System.Numerics;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

//From https://raw.githubusercontent.com/naudio/NAudio/master/NAudio/Wave/SampleProviders/VolumeSampleProvider.cs
public class VolumeSampleProviderWithPeak : ISampleProvider
{
    public delegate void SamplePeak(float peak);

    private readonly SamplePeak _samplePeak;
    private readonly ISampleProvider source;

    /// <summary>
    ///     Initializes a new instance of VolumeSampleProvider
    /// </summary>
    /// <param name="source">Source Sample Provider</param>
    public VolumeSampleProviderWithPeak(ISampleProvider source, SamplePeak samplePeak)
    {
        this.source = source;
        _samplePeak = samplePeak;
        Volume = 1.0f;
    }

    /// <summary>
    ///     Allows adjusting the volume, 1.0f = full volume
    /// </summary>
    public float Volume { get; set; }

    /// <summary>
    ///     WaveFormat
    /// </summary>
    public WaveFormat WaveFormat => source.WaveFormat;

    /// <summary>
    ///     Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="sampleCount">Number of samples desired</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int sampleCount)
    {
        var samplesRead = source.Read(buffer, offset, sampleCount);

        var vectorSize = Vector<float>.Count;
        var v_volume = new Vector<float>(Volume);
        var v_min = new Vector<float>(-1f);
        var v_max = new Vector<float>(1f);
        var v_peaks = new Vector<float>(0);
        var remainder = samplesRead % vectorSize;


        for (var i = 0; i < samplesRead - remainder; i += vectorSize)
        {
            var v_samples = Vector.LoadUnsafe(ref buffer[0], (nuint)(offset + i));
            v_samples *= v_volume;
            // Clamp.
            v_samples = Vector.Max(v_min, Vector.Min(v_samples, v_max));

            // Find peaks per lane.
            v_peaks = Vector.Max(v_peaks, Vector.Abs(v_samples));

            v_samples.StoreUnsafe(ref buffer[0], (nuint)(offset + i));
        }

        float peak = 0;

        // Initialize with component-wise peak value.
        for (var c = 0; c < vectorSize; ++c)
        {
            peak = Math.Max(peak, v_peaks[c]);
        }

        // Process remainder.
        for (var n = samplesRead - remainder; n < samplesRead; n++)
        {
            var sample = buffer[offset + n];
            sample *= Volume;

            //stop over boosting
            sample = Math.Max(-1f, Math.Min(sample, 1f));

            peak = Math.Max(peak, Math.Abs(sample));

            buffer[offset + n] = sample;
        }

        _samplePeak(peak);

        return samplesRead;
    }
}