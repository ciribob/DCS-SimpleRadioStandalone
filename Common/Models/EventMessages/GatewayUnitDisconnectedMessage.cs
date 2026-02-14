using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Client;

public class GatewayUnitDisconnectedMessage
{
    public SRClientBase UnitUpdate { get; set; }
}