using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class DCSPlayerSideInfo
    {
        public string name = "";
        public int side = 0;
        public int seat = 0; // 0 is front / normal - 1 is back seat
        public string type = ""; //Used to identify Combined Arms Slots.

        public DCSLatLngPosition LngLngPosition { get; set; } = new DCSLatLngPosition();

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj is DCSPlayerSideInfo info &&
                   name == info.name &&
                   side == info.side &&
                   seat == info.seat && 
                   type == info.type;
        }

        public void Reset()
        {
            name = "";
            side = 0;
            seat = 0;
            LngLngPosition = new DCSLatLngPosition();
            type = "";
        }



    }
}