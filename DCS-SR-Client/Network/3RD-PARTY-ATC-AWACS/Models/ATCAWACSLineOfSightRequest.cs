namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.ATCAWACS.Models;

public struct ATCAWACSLineOfSightRequest
{
    //{ "long1": 39.00, "lat1": 42.00, "alt1": 10, "long2": 40.00, "lat2": 44.00, "alt2": 1600, "clientId":"SOME_ID" }
    public double long1;
    public double lat1;
    public double alt1;

    public double lat2;
    public double long2;
    public double alt2;

    public string clientId;
}