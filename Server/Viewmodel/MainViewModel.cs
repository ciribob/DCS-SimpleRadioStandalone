using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public partial class MainViewModel : ObservableObject
{
	[ObservableProperty] private ServerSettingsModel _serverSettings;
	
	public MainViewModel()
	{
		_serverSettings = new ServerSettingsModel();
	}
}