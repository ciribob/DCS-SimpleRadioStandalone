using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using CommunityToolkit.Mvvm.ComponentModel;

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
	}
}