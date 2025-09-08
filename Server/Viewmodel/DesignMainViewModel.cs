using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.viewmodel;

public class DesignMainViewModel(IEventAggregator eventAggregator, ServerSettingsModel serverSettingsModel)
	: MainViewModel(eventAggregator, serverSettingsModel)
{
	
	/// <summary>
	/// Design time constructor.
	/// </summary>
	public DesignMainViewModel() : this(new EventAggregator(), new ServerSettingsModel(new EventAggregator(), new ServerSettingsStore()))
	{
	}
}