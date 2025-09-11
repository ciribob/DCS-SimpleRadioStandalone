using System;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel(ServerSettingsModel serverSettingsModel, ServerStateModel serverState) : ObservableRecipient
{
	public ServerSettingsModel ServerSettings { get; } = serverSettingsModel;
	public ServerStateModel Server { get; } = serverState;
	
	[ObservableProperty] private bool _isRestartRequired = false; //todo make the settings that require the server to restart causes that to happen or make it say so.
	
	[RelayCommand]
	private void StartStopServer()
	{
		switch (Server.State)
		{
			case ServerStateModel.RunningState.Stopped:
				Server.StartServerCommand.Execute(null);
				break;
			case ServerStateModel.RunningState.Running:
				Server.StopServerCommand.Execute(null);
				break;

			default:
				break;
		}

		return;
	}

	
	// Seperated out so we can invoke on window close too.
	[RelayCommand]
	private void StopServer()
	{
		Server.StopServerCommand.Execute(null);
	}
}