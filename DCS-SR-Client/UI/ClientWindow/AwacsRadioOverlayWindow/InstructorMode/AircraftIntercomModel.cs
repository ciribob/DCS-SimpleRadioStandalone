using System.Collections.Generic;
using System.Text;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.AwacsRadioOverlayWindow.InstructorMode;

public class AircraftIntercomModel
{
    public uint UnitId { get; set; }
    public List<string> PilotNames { get; set; } = new List<string>();
    public string AircraftType { get; set; }

    public override string ToString()
    {
        var names = new StringBuilder();
        foreach (var pilotName in PilotNames)
        {
            names.Append(pilotName);
            names.Append(' ');
        }

        return $"{AircraftType}: {names.ToString().Trim()}";
    }
}