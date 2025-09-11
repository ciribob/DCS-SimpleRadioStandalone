using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public class DesignMainViewModel(ServerSettingsModel serverSettingsModel, ServerStateModel serverState)
	: MainViewModel(serverSettingsModel, serverState)
{
	
	/// <summary>
	/// Design time constructor.
	/// </summary>
	public DesignMainViewModel() : this(new ServerSettingsModel(new EventAggregator(), new ServerSettingsStore()), new ServerStateModel(new EventAggregator(), false))
	{
	}
}