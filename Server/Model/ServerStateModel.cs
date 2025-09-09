using System.Collections.ObjectModel;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

[ObservableObject]
public partial class ServerStateModel(IEventAggregator eventAggregator) : ServerState(eventAggregator, false)
{
	public ObservableCollection<SRClientBase> ConnectedClients => new ObservableCollection<SRClientBase>(_connectedClients.Values);
}