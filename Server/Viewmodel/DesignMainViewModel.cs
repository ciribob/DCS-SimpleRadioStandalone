using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public class DesignMainViewModel(IEventAggregator eventAggregator, ServerSettingsModel serverSettingsModel, ServerStateModel serverState)
	: MainViewModel(eventAggregator, serverSettingsModel, serverState)
{
	
	/// <summary>
	/// Design time constructor.
	/// </summary>
	public DesignMainViewModel(EventAggregator agg) : this(agg, new ServerSettingsModel(agg, new ServerSettingsStore()), new ServerStateModel(agg, false))
	{
	}
}