using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public partial class ServerSettingsModel : ObservableObject
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage());
		base.OnPropertyChanging(e);
	}
	
	[ObservableProperty] private string _serverIp = "0.0.0.0";
	[ObservableProperty] private int _serverPort = 5002;
	[ObservableProperty] private bool _isUpnpEnabled = true;
	[ObservableProperty] private bool _checkForBetaUpdates = false;
	
	[ObservableProperty] private bool _isLotAtcExportEnabled = false;
	[ObservableProperty] private string _lotAtcExportIp = "127.0.0.1";
	[ObservableProperty] private int _lotAtcExportPort = 10712;
	
	[ObservableProperty] private bool _isExpandedRadiosAllowed = false;
	[ObservableProperty] private bool _isClientExportAllowed = false;
	[ObservableProperty] private bool _isCoalitionAudioSeperated = false;
	[ObservableProperty] private bool _isDistanceCheckingEnabled = false;
	[ObservableProperty] private bool _isExternalModeAllowed = false;
	[ObservableProperty] private string _externalModeBluePassword = string.Empty;
	[ObservableProperty] private string _externalModeRedPassword = string.Empty;
	[ObservableProperty] private bool _isRadioRxInterferenceEnabled = false;
	
	/// <summary>
	/// Not Used.
	/// </summary>
	[ObservableProperty] private bool _isRadioStaticEffectsEnabled = false;
	[ObservableProperty] private bool _isRadioTxEffectsEnabled = false;
	[ObservableProperty] private bool _isTransmissionLogEnabled = false;
	[ObservableProperty] private int _transmissionLogRetentionLimit = 2;
	
	/// <summary>
	/// inverted from normal flow. True means that Spectators can not hear things.
	/// </summary>
	[ObservableProperty] private bool _isSpectatorsAudioDisabled = false;
	[ObservableProperty] private string _clientExportFileName = "clients-list.json";
	[ObservableProperty] private string _testFrequencies = "247.2,120.3";
	
	/// <summary>
	/// Should we show how many people are on a Radio Frequency?
	/// </summary>
	[ObservableProperty] private bool _isShowTunedListenerCount = true;
	[ObservableProperty] private string _globalLobbyFrequencies = "248.22";
	[ObservableProperty] private bool _isShowTransmitterNameEnabled = true;
	[ObservableProperty] private int _retransmissionNodeLimit = 0;
	[ObservableProperty] private bool _isStrictRadioEncryptionEnabled = false;
	
	/// <summary>
	/// TODO: Document what this setting does.
	/// </summary>
	[ObservableProperty] private bool _isRadioEffectOverrideEnabled = false;
}