using System;
using System.Collections.Generic;
using Avalonia;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel(IEventAggregator eventAggregator, ServerSettingsModel serverSettingsModel, ServerStateModel serverState) : ObservableObject
{
	private IEventAggregator EventAggregator { get; } = eventAggregator;
	public ServerSettingsModel ServerSettings { get; } = serverSettingsModel;
	public ServerStateModel ServerState { get; } = serverState;
	
	
	[ObservableProperty] private RunningState _serverRunning = RunningState.Stopped;
	
	[RelayCommand]
	public void StartStopServer()
	{
		switch (ServerRunning)
		{
			case RunningState.Stopped:
				ServerRunning = RunningState.Starting;
				Console.WriteLine("Starting server...");
				
				StartServer();
				
				ServerRunning = RunningState.Running;
				Console.WriteLine("Server started");
				break;
			case RunningState.Running:
				ServerRunning = RunningState.Stopping;
				Console.WriteLine("Stopping server...");
				
				StopServer();
				
				ServerRunning = RunningState.Stopped;
				Console.WriteLine("Server stopped");
				break;

			default:
				break;
		}

		return;
	}
	
	private void StopServer()
	{
		EventAggregator.PublishOnUIThreadAsync(new StartServerMessage());
	}

	public void StartServer()
	{
		EventAggregator.PublishOnUIThreadAsync(new StopServerMessage());
	}
	
	public enum RunningState
	{
		Stopped,
		Starting,
		Running,
		Stopping,
	}
}