using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;

public class GatewayUnitUpdateMessage
{
    public bool FullUpdate { get; set; }
    public SRClientBase UnitUpdate { get; set; }
}