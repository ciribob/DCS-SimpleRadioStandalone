using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

/// <summary>
///     Store to only 5 decimal places - thats plenty for what we need
/// </summary>
public class LatLngPosition
{
    private double _alt;
    private double _lat;
    private double _lng;

    public double alt
    {
        get => _alt;
        set
        {
            if (value != 0)
                _alt = Math.Round(value, 5);
            else
                _alt = 0;
        }
    }

    public double lat
    {
        get => _lat;
        set
        {
            if (value != 0)
                _lat = Math.Round(value, 5);
            else
                _lat = 0;
        }
    }

    public double lng
    {
        get => _lng;
        set
        {
            if (value != 0)
                _lng = Math.Round(value, 5);
            else
                _lng = 0;
        }
    }

    private bool Equals(LatLngPosition other)
    {
        return lat.Equals(other.lat) && lng.Equals(other.lng) && alt.Equals(other.alt);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((LatLngPosition)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(lat, lng, alt);
    }

    public bool IsValid()
    {
        return lat != 0 && lng != 0;
    }

    public override string ToString()
    {
        return $"Pos:[{lat},{lng},{alt}]";
    }

    public LatLngPosition DeepClone()
    {
        return new LatLngPosition
        {
            lat = lat,
            lng = lng,
            alt = alt
        };
    }
}