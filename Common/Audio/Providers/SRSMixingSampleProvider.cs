using System;
using System.Buffers;
using System.Collections.Generic;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Providers;

/// <summary>
///     A sample provider mixer, allowing inputs to be added and removed
/// </summary>
public class SRSMixingSampleProvider : ISampleProvider
{
    private const int MaxInputs = 1024; // protect ourselves against doing something silly
    private readonly List<ISampleProvider> sources;

    /// <summary>
    ///     Creates a new MixingSampleProvider, with no inputs, but a specified WaveFormat
    /// </summary>
    /// <param name="waveFormat">The WaveFormat of this mixer. All inputs must be in this format</param>
    public SRSMixingSampleProvider(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            throw new ArgumentException("Mixer wave format must be IEEE float");
        sources = new List<ISampleProvider>();
        WaveFormat = waveFormat;
    }

    /// <summary>
    ///     Creates a new MixingSampleProvider, based on the given inputs
    /// </summary>
    /// <param name="sources">
    ///     Mixer inputs - must all have the same waveformat, and must
    ///     all be of the same WaveFormat. There must be at least one input
    /// </param>
    public SRSMixingSampleProvider(IEnumerable<ISampleProvider> sources)
    {
        this.sources = new List<ISampleProvider>();
        foreach (var source in sources) AddMixerInput(source);
        if (this.sources.Count == 0) throw new ArgumentException("Must provide at least one input in this constructor");
    }

    /// <summary>
    ///     Returns the mixer inputs (read-only - use AddMixerInput to add an input
    /// </summary>
    public IEnumerable<ISampleProvider> MixerInputs => sources;

    /// <summary>
    ///     When set to true, the Read method always returns the number
    ///     of samples requested, even if there are no inputs, or if the
    ///     current inputs reach their end. Setting this to true effectively
    ///     makes this a never-ending sample provider, so take care if you plan
    ///     to write it out to a file.
    /// </summary>
    public bool ReadFully { get; set; }

    /// <summary>
    ///     The output WaveFormat of this sample provider
    /// </summary>
    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    ///     Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="count">Number of samples required</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        var outputSamples = 0;
        var floatPool = ArrayPool<float>.Shared;
        var sourceBuffer = floatPool.Rent(count);
        lock (sources)
        {
            var index = sources.Count - 1;
            while (index >= 0)
            {
                var source = sources[index];
                Array.Clear(sourceBuffer, 0, count);
                var samplesRead = source.Read(sourceBuffer, 0, count);
                var outIndex = offset;
                for (var n = 0; n < samplesRead; n++)
                    if (n >= outputSamples)
                        buffer[outIndex++] = sourceBuffer[n];
                    else
                        buffer[outIndex++] += sourceBuffer[n];

                outputSamples = Math.Max(samplesRead, outputSamples);
                if (samplesRead < count) MixerInputEnded?.Invoke(this, new SampleProviderEventArgs(source));
                //  sources.RemoveAt(index);
                index--;
            }
        }
        floatPool.Return(sourceBuffer);

        // Stability: Ensure we have reasonable values.
        // #TODO: Vectorize?
        for (var i = 0; i < outputSamples; ++i)
        {
            if (float.IsFinite(buffer[offset + i]))
            {
                buffer[i] = Math.Clamp(buffer[offset + i], -1f, 1f);
            }
            else
            {
                buffer[offset + i] = 0;
            }
        }

        // optionally ensure we return a full buffer
        if (ReadFully && outputSamples < count)
        {
            // NB: Cannot use Array.Clear, as this is/may come from a WaveBuffer which does type punting.
            for (var i = outputSamples; i < count; ++i)
            {
                buffer[offset + i] = 0;
            }
            outputSamples = count;
        }

        return outputSamples;
    }

    // /// <summary>
    // /// Adds a WaveProvider as a Mixer input.
    // /// Must be PCM or IEEE float already
    // /// </summary>
    // /// <param name="mixerInput">IWaveProvider mixer input</param>
    // public void AddMixerInput(IWaveProvider mixerInput)
    // {
    //     AddMixerInput(SampleProviderConverters.ConvertWaveProviderIntoSampleProvider(mixerInput));
    // }

    /// <summary>
    ///     Adds a new mixer input
    /// </summary>
    /// <param name="mixerInput">Mixer input</param>
    public void AddMixerInput(ISampleProvider mixerInput)
    {
        // we'll just call the lock around add since we are protecting against an AddMixerInput at
        // the same time as a Read, rather than two AddMixerInput calls at the same time
        lock (sources)
        {
            if (sources.Count >= MaxInputs) throw new InvalidOperationException("Too many mixer inputs");
            sources.Add(mixerInput);
        }

        if (WaveFormat == null)
        {
            WaveFormat = mixerInput.WaveFormat;
        }
        else
        {
            if (WaveFormat.SampleRate != mixerInput.WaveFormat.SampleRate ||
                WaveFormat.Channels != mixerInput.WaveFormat.Channels)
                throw new ArgumentException("All mixer inputs must have the same WaveFormat");
        }
    }

    /// <summary>
    ///     Raised when a mixer input has been removed because it has ended
    /// </summary>
    public event EventHandler<SampleProviderEventArgs> MixerInputEnded;

    /// <summary>
    ///     Removes a mixer input
    /// </summary>
    /// <param name="mixerInput">Mixer input to remove</param>
    public void RemoveMixerInput(ISampleProvider mixerInput)
    {
        lock (sources)
        {
            sources.Remove(mixerInput);
        }
    }

    /// <summary>
    ///     Removes all mixer inputs
    /// </summary>
    public void RemoveAllMixerInputs()
    {
        lock (sources)
        {
            sources.Clear();
        }
    }
}