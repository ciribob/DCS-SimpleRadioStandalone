using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

public partial class ServerSettingsModel(IEventAggregator eventAggregator, ServerSettingsStore serverSettings) : ObservableObject
{
	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		
		// Any time a property changes here, send this signal.
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
		Console.WriteLine("onPropertyChanged");
	}
	
	[ObservableProperty] private bool _isCheckForBetaUpdatesEnabled = false;
	[ObservableProperty] private bool _isClientExportEnabled = false;
	[ObservableProperty] private bool _isCoalitionAudioSecurityEnabled = false;
	[ObservableProperty] private bool _isDistanceLimitEnabled = false;
	[ObservableProperty] private bool _isExternalModeEnabled = false;
	[ObservableProperty] private bool _isHTTPServerEnabled = false;
	[ObservableProperty] private bool _isIRLRadioRXEffectsEnabled = false;
	[ObservableProperty] private bool _isIRLRadioTXEffectsEnabled = false;
	[ObservableProperty] private bool _isLineOfSightEnabled = false;
	[ObservableProperty] private bool _isLotAtcExportEnabled = false;
	[ObservableProperty] private bool _isRadioEffectOverrideOnGlobalEnabled = false;
	[ObservableProperty] private bool _isRadioEncryptionAllowed = true;
	[ObservableProperty] private bool _isRadioExpansionAllowed = false;
	[ObservableProperty] private bool _isServerPresetsEnabled = false;
	[ObservableProperty] private bool _isShowTransmitterNameEnabled = false;
	[ObservableProperty] private bool _isShowTunedCountEnabled = true;
	[ObservableProperty] private bool _isSpectatorAudioDisabled = false;
	[ObservableProperty] private bool _isStrictRadioEncryptionEnabled = false;
	[ObservableProperty] private bool _isTransmissionLogEnabled = false;
	[ObservableProperty] private bool _isUPNPEnabled = true;
	[ObservableProperty] private int _httpServerPort = 8080;
	[ObservableProperty] private int _lotAtcExportPort = 10712;
	[ObservableProperty] private int _retransmissionNodeLimit = 0;
	[ObservableProperty] private int _serverPort = 5002;
	[ObservableProperty] private int _transmissionLogRetentionLimit = 2;
	[ObservableProperty] private string _clientExportFilePath = string.Empty;
	[ObservableProperty] private string _externalModePassBlue = string.Empty;
	[ObservableProperty] private string _externalModePassRed = string.Empty;
	[ObservableProperty] private string _lotAtcExportIP = "127.0.0.1";
	[ObservableProperty] private string _serverIP = "0.0.0.0";
	[ObservableProperty] private string _serverPresetsPath = string.Empty;
	[ObservableProperty] private ObservableCollection<double> _testFrequencies = new() { 125.2, 142.5 };
	[ObservableProperty] private ObservableCollection<double> _globalLobbyFrequencies = new() { 248.22 };
	
	partial void OnClientExportFilePathChanged(string value) => serverSettings.SetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, value.ToString());
	partial void OnExternalModePassBlueChanged(string value) => serverSettings.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, value.ToString());
	partial void OnExternalModePassRedChanged(string value) => serverSettings.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, value.ToString());
	partial void OnHttpServerPortChanged(int value) => serverSettings.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT, value.ToString());
	partial void OnIsCheckForBetaUpdatesEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value.ToString());
	partial void OnIsClientExportEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, value.ToString());
	partial void OnIsCoalitionAudioSecurityEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value.ToString());
	partial void OnIsDistanceLimitEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.DISTANCE_ENABLED, value.ToString());
	partial void OnIsExternalModeEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, value.ToString());
	partial void OnIsHTTPServerEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED, value.ToString());
	partial void OnIsIRLRadioRXEffectsEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, value.ToString());
	partial void OnIsIRLRadioTXEffectsEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.IRL_RADIO_TX, value.ToString());
	partial void OnIsLineOfSightEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.LOS_ENABLED, value.ToString());
	partial void OnIsLotAtcExportEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, value.ToString());
	partial void OnIsRadioEffectOverrideOnGlobalEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, value.ToString());
	partial void OnIsRadioEncryptionAllowedChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, value.ToString());
	partial void OnIsRadioExpansionAllowedChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.RADIO_EXPANSION, value.ToString());
	partial void OnIsServerPresetsEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.SERVER_PRESETS, value.ToString());
	partial void OnIsShowTransmitterNameEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, value.ToString());
	partial void OnIsShowTunedCountEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, value.ToString());
	partial void OnIsSpectatorAudioDisabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, value.ToString());
	partial void OnIsStrictRadioEncryptionEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, value.ToString());
	partial void OnIsTransmissionLogEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, value.ToString());
	partial void OnIsUPNPEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.UPNP_ENABLED, value.ToString());
	partial void OnLotAtcExportIPChanged(string value) => serverSettings.SetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_IP, value.ToString());
	partial void OnLotAtcExportPortChanged(int value) => serverSettings.SetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT, value.ToString());
	partial void OnRetransmissionNodeLimitChanged(int value) => serverSettings.SetServerSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, value.ToString());
	partial void OnServerIPChanged(string value) => serverSettings.SetServerSetting(ServerSettingsKeys.SERVER_IP, value.ToString());
	partial void OnServerPortChanged(int value) => serverSettings.SetServerSetting(ServerSettingsKeys.SERVER_PORT, value.ToString());
	partial void OnServerPresetsPathChanged(string value) => serverSettings.SetServerSetting(ServerSettingsKeys.SERVER_PRESETS, value.ToString());
	partial void OnTransmissionLogRetentionLimitChanged(int value) => serverSettings.SetServerSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, value.ToString());
	partial void OnTestFrequenciesChanged(ObservableCollection<double> value) => serverSettings.SetServerSetting(ServerSettingsKeys.TEST_FREQUENCIES, value.ToString());
	partial void OnGlobalLobbyFrequenciesChanged(ObservableCollection<double> value) => serverSettings.SetServerSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, value.ToString());

}