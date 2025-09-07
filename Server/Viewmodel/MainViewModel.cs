using System;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel : ObservableObject
{
	public enum RunningState
	{
		Stopped,
		Starting,
		Running,
		Stopping,
	}
	
	[ObservableProperty] private RunningState _serverRunning = RunningState.Stopped;
	
	[ObservableProperty] private ServerSettingsModel _serverSettings;
	partial void OnServerSettingsChanged(ServerSettingsModel? oldValue, ServerSettingsModel newValue)
	{
		
	}

	public MainViewModel()
	{
		_serverSettings = new ServerSettingsModel();
	}
	
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
		
	}

	public void StartServer()
	{
		
	}
}