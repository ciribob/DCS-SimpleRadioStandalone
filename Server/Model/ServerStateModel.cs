using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

[ObservableObject]
public partial class ServerStateModel : ServerState, IHandle<ServerStateMessage>
{
	[ObservableProperty] private RunningState _state = RunningState.Stopped;

	public ObservableCollection<SRClientBase> ConnectedClients => new(_connectedClients.Values);

	public ServerStateModel(IEventAggregator eventAggregator, bool autostart = false) : base(eventAggregator, autostart)
	{
		eventAggregator.SubscribeOnUIThread(this);
	}
	
	[RelayCommand]
	private new void StartServer()
	{
		Console.WriteLine("Server is Starting...");
		State = RunningState.Starting;
		base.StartServer();

		Console.WriteLine("Server is Running");
		State = RunningState.Running;
	}

	[RelayCommand]
	private new void StopServer()
	{
		Console.WriteLine("Server is Stopping...");
		State = RunningState.Stopping;
		base.StopServer();
		
		Console.WriteLine("Server is Stopped");
		State = RunningState.Stopped;
	}

	[RelayCommand]
	private new void KickClient(SRClientBase client)
	{
		base.KickClient(client);
	}

	[RelayCommand]
	private void BanClient(SRClientBase client)
	{
		WriteBanIP(client);
		KickClient(client);
	}
	
	
	public async Task HandleAsync(ServerStateMessage message, CancellationToken cancellationToken)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectedClients)) );
	}
	
	public enum RunningState
	{
		Stopped,
		Starting,
		Running,
		Stopping,
	}
}