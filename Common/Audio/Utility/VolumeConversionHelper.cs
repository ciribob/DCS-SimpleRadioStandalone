using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.Utility;

public static class VolumeConversionHelper
{
    // 20 / ln( 10 )
    private const double LOG_2_DB = 8.6858896380650365530225783783321;

    // ln( 10 ) / 20
    private const double DB_2_LOG = 0.11512925464970228420089957273422;

    /// <summary>
    ///     linear to dB conversion
    /// </summary>
    /// <param name="lin">linear value</param>
    /// <returns>decibel value</returns>
    public static double LinearToDecibels(double lin)
    {
        return Math.Log(lin) * LOG_2_DB;
    }

    /// <summary>
    ///     dB to linear conversion
    /// </summary>
    /// <param name="dB">decibel value</param>
    /// <returns>linear value</returns>
    public static double DecibelsToLinear(double dB)
    {
        return Math.Exp(dB * DB_2_LOG);
    }

    public static double ConvertFloatToDB(float linear)
    {
        if (linear == 0)
            //basically nothing but not 0 or we'll get -nan
            linear = 0.0000158f;

        return LinearToDecibels(linear);
    }


    public static string ConvertLinearDiffToDB(float delta)
    {
        var diff = (float)ConvertFloatToDB(delta) -
                   (float)ConvertFloatToDB((float)1.0);

        return Math.Round(diff) + " dB";
        //convert diff into db
    }

    public static float ConvertVolumeSliderToScale(float volume)
    {
        var db = -30 + (28 - -30) * volume;
        return (float)Math.Exp(db / 20 * Math.Log(10));
    }

    // https://electronics.stackexchange.com/questions/85435/why-do-16-bit-systems-have-a-minimum-dbfs-of-96
    //https://stackoverflow.com/questions/4152201/calculate-decibels
    public static double CalculateRMS(short[] pcmShort, int offset = 0, int limit = 0)
    {
        if (limit == 0) limit = pcmShort.Length;

        //convert diff into db
        double sum = 0;
        for (var i = offset; i < limit + offset; i++)
            if (pcmShort[i] != 0)
            {
                var sample = pcmShort[i] / 32768.0;
                sum += sample * sample;
            }

        if (sum == 0) return -96.6d;
        var rms = Math.Sqrt(sum / limit);

        if (rms == 0) return 0d;

        return 20 * Math.Log10(rms);
    }

    public static double CalculateRMS(float[] buffer, int offset, int sampleCount)
    {
        //convert diff into db

        double sum = 0;
        for (var i = offset; i < sampleCount + offset; i++)
        {
            double sample = buffer[i];
            sum += sample * sample;
        }

        if (sum == 0) return -96.6d;
        var rms = Math.Sqrt(sum / sampleCount);

        if (rms == 0) return 0d;

        return 20 * Math.Log10(rms);
    }

    public static float ConvertRadioVolumeSlider(float sliderValue, bool allowBoost = false)
    {
        var dB = ConvertSliderToVolume(sliderValue, allowBoost);
        return dBToLinearScale(dB);
    }

    private static double ConvertSliderToVolume(double sliderValue, bool allowBoost)
    {
        if (sliderValue <= 0)
            sliderValue = 0.000001;
        else if (sliderValue > 1 && !allowBoost) sliderValue = 1;
        return 20 * Math.Log10(sliderValue);
    }

    private static float dBToLinearScale(double dB)
    {
        return (float)Math.Pow(10, dB / 20.0d);
    }
    
    public static double GetTargetdB(double currentDb, double reductionFactor)
    {
        if (reductionFactor <= 0) return -144.0; 
        if (reductionFactor >= 1) return currentDb;

        double dbChange = 10 * Math.Log(reductionFactor, 2);
        return currentDb + dbChange;
    }
}