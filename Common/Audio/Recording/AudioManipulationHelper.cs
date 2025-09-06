using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Recording;

public static class AudioManipulationHelper
{
    public static short[] MixSamplesClipped(short[] pcmAudioOne, short[] pcmAudioTwo, int samplesLength)
    {
        var mixedDown = new short[samplesLength];

        for (var i = 0; i < samplesLength; i++)
        {
            var result = pcmAudioOne[i] + pcmAudioTwo[i];
            if (result > short.MaxValue)
                result = short.MaxValue;
            else if (result < short.MinValue) result = short.MinValue;
            mixedDown[i] = (short)result;
        }

        return mixedDown;
    }

    public static float[] MixSamplesWithHeadroom(List<float[]> samplesToMixdown, int samplesLength)
    {
        var mixedDown = new float[samplesLength];

        foreach (var sample in samplesToMixdown)
            for (var i = 0; i < samplesLength; i++)
                // Unlikely to have duplicate signals across n radios, can use sqrt to find a sensible headroom level
                // FIXME: Users likely want a consistent mixdown regardless of radios in airframe, just hardcode a constant term?
                mixedDown[i] += (float)(sample[i] / Math.Sqrt(samplesToMixdown.Count));

        return mixedDown;
    }


    public static (short[], short[]) SplitSampleByTime(long samplesRemaining, short[] samples)
    {
        var toWrite = new short[samplesRemaining];
        var remainder = new short[samples.Length - samplesRemaining];

        Array.Copy(samples, 0, toWrite, 0, samplesRemaining);
        Array.Copy(samples, samplesRemaining, remainder, 0, remainder.Length);

        return (toWrite, remainder);
    }

    public static float[] SineWaveOut(int sampleLength, int sampleRate, double volume)
    {
        var sineBuffer = new float[sampleLength];
        var amplitude = volume;

        for (var i = 0; i < sineBuffer.Length; i++)
            sineBuffer[i] = (float)(amplitude * Math.Sin(2 * Math.PI * i * 175 / sampleRate));
        return sineBuffer;
    }

    public static int CalculateSamplesStart(long start, long end, int sampleRate)
    {
        var elapsedSinceLastWrite = ((double)end - start) / 10000000;
        var necessarySamples = Convert.ToInt32(elapsedSinceLastWrite * sampleRate);
        // prevent any potential issues due to a negative time being returned
        return necessarySamples >= 0 ? necessarySamples : 0;
        //return necessarySamples
    }

    public static void MixArraysClipped(Span<float> destination, ReadOnlySpan<float> samples)
    {
        var count = Math.Min(destination.Length, samples.Length);

        var vectorSize = Vector<float>.Count;
        var remainder = count % vectorSize;
        var v_minusOne = -Vector<float>.One;
        for (var i = 0; i < count - remainder; i += vectorSize)
        {
            var v_destination = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(destination), (nuint)i);
            var v_samples = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(samples), (nuint)i);

            v_destination = Vector.Max(v_minusOne, Vector.Min(v_destination + v_samples, Vector<float>.One));

            Vector.StoreUnsafe(v_destination, ref MemoryMarshal.GetReference(destination), (nuint)i);
        }

        for (var i = count - remainder; i < count; ++i)
        {
            destination[i] = Math.Clamp(destination[i] + samples[i], -1f, 1f);
        }
    }
  

    public static int MixArraysNoClipping(Span<float> array1, Span<float> array2)
    {
        if (array1.Length > array2.Length)
        {
            for (var i = 0; i < array2.Length; i++) array1[i] += array2[i];

            return array1.Length;
        }

        for (var i = 0; i < array2.Length; i++) array2[i] += array1[i];

        return array2.Length;
    }

    public static void ClipArray(Span<float> array)
    {
        var vectorSize = Vector<float>.Count;
        var remainder = array.Length % vectorSize;
        var v_minusOne = -Vector<float>.One;
        for (var i = 0; i < array.Length - remainder; i += vectorSize)
        {
            var v_samples = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(array), (nuint)i);

            v_samples = Vector.Max(v_minusOne, Vector.Min(v_samples, Vector<float>.One));

            Vector.StoreUnsafe(v_samples, ref MemoryMarshal.GetReference(array), (nuint)i);
        }

        for (var i = array.Length - remainder; i < array.Length; ++i)
        {
            array[i] = Math.Clamp(array[i], -1f, 1f);
        }
    }
}