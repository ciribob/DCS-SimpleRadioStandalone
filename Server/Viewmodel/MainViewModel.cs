using System;
using Avalonia;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel : ObservableObject
{
	private IEventAggregator EventAggregator { get; }

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
		EventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
	}

	public MainViewModel(IEventAggregator  eventAggregator)
	{
		EventAggregator = eventAggregator;
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