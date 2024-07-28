using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState
{
    public class DCSLatLngPosition
    {
        public double lat;
        public double lng;
        public double alt;

        public DCSLatLngPosition()
        {
            // Null constructor. Is dangerous.
            this.lat = 0; this.lng = 0; this.alt = 0;
        }
        
        public DCSLatLngPosition(double lat, double lng, double alt)
        {
            this.lat = lat;
            this.lng = lng;
            this.alt = alt;
        }

        public bool isValid()
        {
            return lat != 0 && lng != 0;
        }

        public override string ToString()
        {
            return $"Pos:[{lat},{lng},{alt}]";
        }
    }
}
