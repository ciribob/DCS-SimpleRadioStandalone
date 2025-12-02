using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;

public class InstructorModeMessage
{
    public DCSRadio Radio { get; set; }
    public int RadioId { get; set; }
}