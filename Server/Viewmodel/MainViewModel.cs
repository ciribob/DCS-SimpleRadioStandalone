using System.Diagnostics;
using System.Windows.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel : ObservableObject
{
	enum RunningState
	{
		Stopped,
		Starting,
		Running,
		Stopping,
	}
	
	[ObservableProperty] private ServerSettingsModel _serverSettings;
	RunningState ServerRunning {get; set;}
	
	public MainViewModel()
	{
		_serverSettings = new ServerSettingsModel();
		StartStopCommand = new RelayCommand(StartStopServer);
	}

	public ICommand StartStopCommand { get; }
	public void StartStopServer()
	{
		switch (ServerRunning)
		{
			case RunningState.Stopped:
				ServerRunning = RunningState.Starting;

				ServerRunning = RunningState.Running;
				break;
			case RunningState.Running:
				ServerRunning = RunningState.Stopping;
				
				ServerRunning = RunningState.Stopped;
				break;
			
			default:
				break;
		}
	}
}