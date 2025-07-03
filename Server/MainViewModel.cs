using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

public class MainViewModel
{
	public ServerSettingsModel ServerSettings { get; set; }
	
	public MainViewModel()
	{
		ServerSettings = new ServerSettingsModel();
	}
}