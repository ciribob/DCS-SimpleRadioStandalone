using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

public partial class ServerSettingsModel(IEventAggregator eventAggregator, ServerSettingsStore serverSettings) : ObservableValidator
{
	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		
		// Any time a property changes here, send this signal.
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
	}
	
	[ObservableProperty] private bool _isCheckForBetaUpdatesEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue;
	[ObservableProperty] private bool _isClientExportEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED).BoolValue;
	[ObservableProperty] private bool _isCoalitionAudioSecurityEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;
	[ObservableProperty] private bool _isDistanceLimitEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.DISTANCE_ENABLED).BoolValue;
	[ObservableProperty] private bool _isExternalModeEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue;
	[ObservableProperty] private bool _isHTTPServerEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED).BoolValue;
	[ObservableProperty] private bool _isIRLRadioRXEffectsEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.IRL_RADIO_TX).BoolValue;
	[ObservableProperty] private bool _isIRLRadioTXEffectsEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE).BoolValue;
	[ObservableProperty] private bool _isLineOfSightEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.LOS_ENABLED).BoolValue;
	[ObservableProperty] private bool _isLotAtcExportEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED).BoolValue;
	[ObservableProperty] private bool _isRadioEffectOverrideOnGlobalEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE).BoolValue;
	[ObservableProperty] private bool _isRadioEncryptionAllowed = serverSettings.GetServerSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION).BoolValue;
	[ObservableProperty] private bool _isRadioExpansionAllowed = serverSettings.GetServerSetting(ServerSettingsKeys.RADIO_EXPANSION).BoolValue;
	[ObservableProperty] private bool _isServerPresetsEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_PRESETS).BoolValue;
	[ObservableProperty] private bool _isShowTransmitterNameEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME).BoolValue;
	[ObservableProperty] private bool _isShowTunedCountEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.SHOW_TUNED_COUNT).BoolValue;
	[ObservableProperty] private bool _isSpectatorAudioDisabled = serverSettings.GetServerSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;
	[ObservableProperty] private bool _isStrictRadioEncryptionEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION).BoolValue;
	[ObservableProperty] private bool _isTransmissionLogEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue;
	[ObservableProperty] private bool _isUPNPEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.UPNP_ENABLED).BoolValue;
	
	[ObservableProperty] [Range(0,65535)] private int _httpServerPort = serverSettings.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT).IntValue;
	[ObservableProperty] [Range(0,65535)] private int _lotAtcExportPort = serverSettings.GetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT).IntValue;
	[ObservableProperty] [Range(0,7)] private int _retransmissionNodeLimit = serverSettings.GetServerSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT).IntValue;
	[ObservableProperty] [Range(0,65535)] private int _serverPort = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;
	[ObservableProperty] [Range(0,7)] private int _transmissionLogRetentionLimit = serverSettings.GetServerSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue;
	[ObservableProperty] [MaxLength(32767)] private string _clientExportFilePath = serverSettings.GetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH).StringValue;
	[ObservableProperty] [MaxLength(64)] private string _externalModePassBlue = serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD).StringValue;
	[ObservableProperty] [MaxLength(64)] private string _externalModePassRed = serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD).StringValue;
	[ObservableProperty] [MaxLength(256)] private string _lotAtcExportIP = serverSettings.GetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_IP).StringValue;
	[ObservableProperty] [MaxLength(256)] private string _serverIP = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_IP).StringValue;
	[ObservableProperty] [MaxLength(32767)] private string _serverPresetsPath = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_PRESETS).StringValue;
	
	//todo frequency list, create initialization and validation system
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

	partial void OnTestFrequenciesChanged(ObservableCollection<double> value)
	{
		serverSettings.SetServerSetting(ServerSettingsKeys.TEST_FREQUENCIES, value.ToString());
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged());
	}

	partial void OnGlobalLobbyFrequenciesChanged(ObservableCollection<double> value)
	{
		serverSettings.SetServerSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, value.ToString());
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged());
	}
}