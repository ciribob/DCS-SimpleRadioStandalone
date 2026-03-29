using System;
using System.Diagnostics;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel(ServerSettingsModel serverSettingsModel, ServerStateModel serverState) : ObservableRecipient
{
	private readonly Logger _logger = LogManager.GetCurrentClassLogger();
	
	[ObservableProperty] private UpdateCallbackResult _updateCallback = new(){ UpdateAvailable = true, Version = new Version("0.0.0"), Branch = "Null"};
	[ObservableProperty] private bool _updateError;
	
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

	[RelayCommand]
	private void AutomaticUpdateServer()
	{
		try
		{
			_logger.Warn($@"Attempting automatic update to Version: {UpdateCallback.Version}-{UpdateCallback.Branch}");
			UpdaterChecker.Instance.LaunchUpdater(UpdateCallback.Beta);
		}
		catch (Exception e)
		{
			UpdateError = true;
			_logger.Error($@"Error while updating! {Environment.NewLine} {e.Message}");
		}
	}
	
	[RelayCommand]
	private void ManualUpdateServer()
	{
		try
		{
			Process.Start(new ProcessStartInfo{FileName = "https://github.com/ciribob/DCS-SimpleRadioStandalone/releases", UseShellExecute = true, Verb = "open" } );
		}
		catch (Exception e)
		{
			UpdateError = true;
			_logger.Error($@"Error opening browser! {Environment.NewLine} {e.Message}");
		}
	}
}