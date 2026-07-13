using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

public partial class ServerSettingsModel(IEventAggregator eventAggregator, ServerSettingsStore serverSettings) : ObservableValidator
{
	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		
		// Any time a property changes here, send this signal.
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerSettingsChangedMessage());
	}
	
	#region General Settings
	[ObservableProperty] private bool _isClientExportEnabled = serverSettings.GetSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED);
	[ObservableProperty] private bool _isCoalitionAudioSecurityEnabled = serverSettings.GetSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
	[ObservableProperty] private bool _isDistanceLimitEnabled = serverSettings.GetSetting(ServerSettingsKeys.DISTANCE_ENABLED);
	[ObservableProperty] private bool _isExternalModeEnabled = serverSettings.GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE);
	[ObservableProperty] private bool _isIrlRadioRxEffectsEnabled = serverSettings.GetSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE);
	[ObservableProperty] private bool _isIrlRadioTxEffectsEnabled = serverSettings.GetSetting(ServerSettingsKeys.IRL_RADIO_TX);
	[ObservableProperty] private bool _isLineOfSightEnabled = serverSettings.GetSetting(ServerSettingsKeys.LOS_ENABLED);
	[ObservableProperty] private bool _isLotAtcExportEnabled = serverSettings.GetSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED);
	[ObservableProperty] private bool _isRadioEffectOverrideOnGlobalEnabled = serverSettings.GetSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE);
	[ObservableProperty] private bool _isRadioEncryptionAllowed = serverSettings.GetSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION);
	[ObservableProperty] private bool _isRadioExpansionAllowed = serverSettings.GetSetting(ServerSettingsKeys.RADIO_EXPANSION);
	[ObservableProperty] private bool _isServerPresetsEnabled = serverSettings.GetSetting(ServerSettingsKeys.SERVER_PRESETS_ENABLED);
	[ObservableProperty] private bool _isShowTransmitterNameEnabled = serverSettings.GetSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME);
	[ObservableProperty] private bool _isShowTunedCountEnabled = serverSettings.GetSetting(ServerSettingsKeys.SHOW_TUNED_COUNT);
	[ObservableProperty] private bool _isSpectatorAudioDisabled = serverSettings.GetSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED);
	[ObservableProperty] private bool _isStrictRadioEncryptionEnabled = serverSettings.GetSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION);
	[ObservableProperty] private bool _isTransmissionLogEnabled = serverSettings.GetSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED);
	
	[ObservableProperty] [Range(0,7)] private int _retransmissionNodeLimit = serverSettings.GetSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT);
	[ObservableProperty] [Range(0,7)] private int _transmissionLogRetentionLimit = serverSettings.GetSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION);

	[ObservableProperty] private ObservableCollection<double> _globalLobbyFrequencies = new ObservableCollection<double>(
			((string)serverSettings.GetSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES))
			.Split(',').Select(double.Parse).ToList()
		);

	
	[ObservableProperty] private ObservableCollection<double> _testFrequencies = new ObservableCollection<double>(
		((string)serverSettings.GetSetting(ServerSettingsKeys.TEST_FREQUENCIES))
		.Split(',').Select(double.Parse).ToList()
	);

	partial void OnIsClientExportEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, value);
	partial void OnIsCoalitionAudioSecurityEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value);
	partial void OnIsDistanceLimitEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.DISTANCE_ENABLED, value);
	partial void OnIsExternalModeEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, value);
	partial void OnIsIrlRadioRxEffectsEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, value);
	partial void OnIsIrlRadioTxEffectsEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.IRL_RADIO_TX, value);
	partial void OnIsLineOfSightEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.LOS_ENABLED, value);
	partial void OnIsLotAtcExportEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, value);
	partial void OnIsRadioEffectOverrideOnGlobalEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, value);
	partial void OnIsRadioEncryptionAllowedChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, value);
	partial void OnIsRadioExpansionAllowedChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.RADIO_EXPANSION, value);
	partial void OnIsServerPresetsEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.SERVER_PRESETS, value);
	partial void OnIsShowTransmitterNameEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, value);
	partial void OnIsShowTunedCountEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, value);
	partial void OnIsSpectatorAudioDisabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, value);
	partial void OnIsStrictRadioEncryptionEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, value);
	partial void OnIsTransmissionLogEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, value);
	partial void OnRetransmissionNodeLimitChanged(int value) => serverSettings.SetSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, value);
	partial void OnTransmissionLogRetentionLimitChanged(int value) => serverSettings.SetSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, value);
	
	partial void OnGlobalLobbyFrequenciesChanged(ObservableCollection<double> value)
	{
		serverSettings.SetSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, String.Join(",", value));
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged());
	}
	partial void OnTestFrequenciesChanged(ObservableCollection<double> value)
	{
		serverSettings.SetSetting(ServerSettingsKeys.TEST_FREQUENCIES, String.Join(",", value));
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged());
	}

	#endregion

	#region Server Settings
	[ObservableProperty] private bool _isCheckForBetaUpdatesEnabled = serverSettings.GetSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES);

	[ObservableProperty] private bool _isUpnpEnabled = serverSettings.GetSetting(ServerSettingsKeys.UPNP_ENABLED);
	[ObservableProperty] [Range(0,65535)] private int _serverPort = serverSettings.GetSetting(ServerSettingsKeys.SERVER_PORT);
	[ObservableProperty] [MaxLength(256)] private string _serverBindIp = serverSettings.GetSetting(ServerSettingsKeys.SERVER_IP);

	[ObservableProperty] private bool _isHttpServerEnabled = serverSettings.GetSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED);
	[ObservableProperty] [Range(0,65535)] private int _httpServerPort = serverSettings.GetSetting(ServerSettingsKeys.HTTP_SERVER_PORT);
	
	
	[ObservableProperty] [Range(0,65535)] private int _lotAtcExportPort = serverSettings.GetSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT);
	[ObservableProperty] [MaxLength(256)] private string _lotAtcExportIp = serverSettings.GetSetting(ServerSettingsKeys.LOTATC_EXPORT_IP);

	[ObservableProperty] [MaxLength(32767)] private string _clientExportFilePath = serverSettings.GetSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH);
	[ObservableProperty] [MaxLength(32767)] private string _serverPresetsPath = serverSettings.GetSetting(ServerSettingsKeys.SERVER_PRESETS);
	
	
	partial void OnIsCheckForBetaUpdatesEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value);
	partial void OnIsUpnpEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.UPNP_ENABLED, value);
	partial void OnServerPortChanged(int value) => serverSettings.SetSetting(ServerSettingsKeys.SERVER_PORT, value);
	partial void OnServerBindIpChanged(string value) => serverSettings.SetSetting(ServerSettingsKeys.SERVER_IP, value);
	partial void OnIsHttpServerEnabledChanged(bool value) => serverSettings.SetSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED, value);
	partial void OnHttpServerPortChanged(int value) => serverSettings.SetSetting(ServerSettingsKeys.HTTP_SERVER_PORT, value);
	partial void OnLotAtcExportPortChanged(int value) => serverSettings.SetSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT, value);
	partial void OnLotAtcExportIpChanged(string value) => serverSettings.SetSetting(ServerSettingsKeys.LOTATC_EXPORT_IP, value);
	
	partial void OnClientExportFilePathChanged(string value) => serverSettings.SetSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, value);
	partial void OnServerPresetsPathChanged(string value) => serverSettings.SetSetting(ServerSettingsKeys.SERVER_PRESETS, value);
	
	#endregion

	#region External AWACS Mode Settings
	[ObservableProperty] [MaxLength(64)] private string _externalModePassBlue = serverSettings.GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD);
	[ObservableProperty] [MaxLength(64)] private string _externalModePassRed = serverSettings.GetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD);

	partial void OnExternalModePassBlueChanged(string value) => serverSettings.SetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, value);
	partial void OnExternalModePassRedChanged(string value) => serverSettings.SetSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, value);
	
	#endregion
	

	[RelayCommand]
	private void TestFrequencyAdd(double value)
	{
		TestFrequencies.Add(value);
		SaveAndPublish(ServerSettingsKeys.TEST_FREQUENCIES, TestFrequencies);
	}
	[RelayCommand]
	private void TestFrequencyRemove(double value)
	{
		TestFrequencies.Remove(value);
		SaveAndPublish(ServerSettingsKeys.TEST_FREQUENCIES, TestFrequencies);
	}
	[RelayCommand]
	private void GlobalFrequencyAdd(double value)
	{
		GlobalLobbyFrequencies.Add(value);
		SaveAndPublish(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, GlobalLobbyFrequencies);
	}
	[RelayCommand]
	private void GlobalFrequencyRemove(double value)
	{
		GlobalLobbyFrequencies.Remove(value);
		SaveAndPublish(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, GlobalLobbyFrequencies);
	}

	private void SaveAndPublish(ServerSettingsKeys key, ObservableCollection<double> collection)
	{
		serverSettings.SetSetting(key, String.Join(",", collection));
		eventAggregator.PublishOnBackgroundThreadAsync(
			new ServerFrequenciesChanged()
			{
				TestFrequencies = string.Join(",", TestFrequencies), 
				GlobalLobbyFrequencies = string.Join(",", GlobalLobbyFrequencies)
			} 
		);
	}
}