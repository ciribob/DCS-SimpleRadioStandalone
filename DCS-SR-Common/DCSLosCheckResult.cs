using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public struct DCSLosCheckResult
    {
        public string id;
        public bool los;

        public override string ToString()
        {
            return $"[id {id} LOS {los}]";
        }
    }
}
