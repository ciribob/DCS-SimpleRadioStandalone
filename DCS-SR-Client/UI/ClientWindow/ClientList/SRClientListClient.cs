using System.Windows.Input;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;

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

        ToggleMute = new DelegateCommand(ToggleClientMute);
    }

    public ICommand ToggleMute { get; }
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

    public string IsMuted => Muted ? "Muted" : "";

    public void ToggleClientMute()
    {
        if (ClientGuid != ClientStateSingleton.Instance.ShortGUID 
            && ConnectedClientsSingleton.Instance.TryGetValue(this.ClientGuid, out var client))
        {
            client.Muted = !client.Muted;
            this.Muted = !this.Muted;
            NotifyPropertyChanged(nameof(Muted));
            NotifyPropertyChanged(nameof(IsMuted));
        }
    }
}