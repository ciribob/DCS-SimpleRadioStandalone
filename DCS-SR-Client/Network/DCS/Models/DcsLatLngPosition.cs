﻿namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models;

public class DCSLatLngPosition
{
    public double alt;
    public double lat;
    public double lng;

    public bool isValid()
    {
        return lat != 0 && lng != 0;
    }

    public override string ToString()
    {
        return $"Pos:[{lat},{lng},{alt}]";
    }
}