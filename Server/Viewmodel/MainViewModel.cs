using System;
using Avalonia;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel(IEventAggregator  eventAggregator) : ObservableObject
{
	private IEventAggregator EventAggregator { get; } = eventAggregator;

	public enum RunningState
	{
		Stopped,
		Starting,
		Running,
		Stopping,
	}
	
	[ObservableProperty] private RunningState _serverRunning = RunningState.Stopped;
	
	[ObservableProperty] private ServerSettingsModel _serverSettings;
	
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