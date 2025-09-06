using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
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

	private CancellationTokenSource _serverCliCancelTokenSource = new();
	
	private Process _serverCLIProcess = new()
	{
		StartInfo = new() {
		CreateNoWindow = false, 
		UseShellExecute = true, 
		FileName = "C:\\Program Files\\DCS-SimpleRadio-Standalone\\ServerCommandLine-Windows\\SRS-Server-Commandline.exe" 
		}
	};
	
	public MainViewModel()
	{
		_serverSettings = new ServerSettingsModel();
		StartStopCommand = new RelayCommand(StartStopServer);
	}

	public ICommand StartStopCommand { get; }

	public void StartStopServer()
	{
		CancellationToken ct = _serverCliCancelTokenSource.Token;
		
		switch (ServerRunning)
		{
			case RunningState.Stopped:
				ServerRunning = RunningState.Starting;
				
				Console.WriteLine("Starting server...");
				Task.Run( ()=>
				{
					ct.ThrowIfCancellationRequested();
					_serverCLIProcess.Start();
					_serverCLIProcess.WaitForExitAsync(ct);
				}, ct);
				
				ServerRunning = RunningState.Running;
				Console.WriteLine("Server started");
				break;
			case RunningState.Running:
				ServerRunning = RunningState.Stopping;
				
				Console.WriteLine("Stopping server...");
				_serverCliCancelTokenSource.Cancel();
				
				ServerRunning = RunningState.Stopped;
				Console.WriteLine("Server stopped");
				break;

			default:
				break;
		}

		return;
	}
}