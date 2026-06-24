using System;
using System.Diagnostics;
using System.Text.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;

public class DCSTimeUpdate
{
    public uint Year;
    public uint Month;
    public uint Day;
    public uint Start_time;
    public double Model_time;

    public static DateTime? Iso8691Builder(DCSTimeUpdate dcsTimeUpdate)
    {
        if (IsZero(dcsTimeUpdate))
            return null;

        return BuildDateTime(dcsTimeUpdate.Year, dcsTimeUpdate.Month, dcsTimeUpdate.Day, dcsTimeUpdate.Start_time, dcsTimeUpdate.Model_time);
    }

    public DateTime? GetStartMissionTime()
    {
        if (IsZero(this))
            return null;

        return BuildDateTime(Year, Month, Day, Start_time, 0);
    }

    private static DateTime? BuildDateTime(uint year, uint month, uint day, uint startTime, double modelTime)
    {
        try
        {
            int hours = (int)(startTime / 3600);
            int minutes = (int)((startTime % 3600) / 60);
            int seconds = (int)(startTime % 60);

            var stamp = new DateTime((int)year, (int)month, (int)day, hours, minutes, seconds)
                .AddSeconds((int)modelTime);
            return stamp;
        }
        catch (ArgumentOutOfRangeException)
        {
            Debug.WriteLine($"Invalid DateTime parameters: year={year}, month={month}, day={day}, startTime={startTime}, modelTime={modelTime}");
            return null;
        }
    }

    public static bool IsZero(DCSTimeUpdate dcsTimeUpdate) =>
        dcsTimeUpdate != null &&
        dcsTimeUpdate.Year == 0 &&
        dcsTimeUpdate.Month == 0 &&
        dcsTimeUpdate.Day == 0 &&
        dcsTimeUpdate.Start_time == 0 &&
        dcsTimeUpdate.Model_time == 0;
}