namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.ATCAWACS.Models;

// {"controller":{"altitude":34,"isFlying":false,"latitude":45.03800822794648,"longitude":39.18813534903283}}
public class ATCAWACSMessageWrapper
{
    public ATCAWACSPosition controller;

    public ATCAWACSLineOfSightResponse los;

    //class not struct so we get Nulls
    public class ATCAWACSPosition
    {
        public double altitude;
        public bool isFlying;
        public double latitude;
        public double longitude;
    }

    public class ATCAWACSLineOfSightResponse
    {
        public string clientId;
        public bool see; //visible
    }
}