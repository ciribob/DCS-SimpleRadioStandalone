using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList;

public class SRClientListClient : SRClientBase
{
    public SRClientListClient(SRClientBase client)
    {
        this.AllowRecord = client.AllowRecord;
        this.Coalition = client.Coalition;
        this.Name = client.Name;
        this.ClientGuid = client.ClientGuid;
        this.LastTransmissionReceived = client.LastTransmissionReceived;
        this.LatLngPosition = client.LatLngPosition;
        this.Muted = client.Muted;
        this.RadioInfo = client.RadioInfo;
        this.TransmittingFrequency = client.TransmittingFrequency;
    }

    public SolidColorBrush ClientCoalitionColour
    {
        get
        {
            switch (Coalition)
            {
                case 0:
                    return new SolidColorBrush(Colors.White);
                case 1:
                    return new SolidColorBrush(Colors.Red);
                case 2:
                    return new SolidColorBrush(Colors.Blue);
                default:
                    return new SolidColorBrush(Colors.White);
            }
        }
    }
}