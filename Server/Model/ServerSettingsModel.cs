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
	[ObservableProperty] private bool _isClientExportEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED).BoolValue;
	[ObservableProperty] private bool _isCoalitionAudioSecurityEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;
	[ObservableProperty] private bool _isDistanceLimitEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED).BoolValue;
	[ObservableProperty] private bool _isExternalModeEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue;
	[ObservableProperty] private bool _isIRLRadioRXEffectsEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX).BoolValue;
	[ObservableProperty] private bool _isIRLRadioTXEffectsEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE).BoolValue;
	[ObservableProperty] private bool _isLineOfSightEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.LOS_ENABLED).BoolValue;
	[ObservableProperty] private bool _isLotAtcExportEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED).BoolValue;
	[ObservableProperty] private bool _isRadioEffectOverrideOnGlobalEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE).BoolValue;
	[ObservableProperty] private bool _isRadioEncryptionAllowed = serverSettings.GetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION).BoolValue;
	[ObservableProperty] private bool _isRadioExpansionAllowed = serverSettings.GetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION).BoolValue;
	[ObservableProperty] private bool _isServerPresetsEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS).BoolValue;
	[ObservableProperty] private bool _isShowTransmitterNameEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME).BoolValue;
	[ObservableProperty] private bool _isShowTunedCountEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT).BoolValue;
	[ObservableProperty] private bool _isSpectatorAudioDisabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;
	[ObservableProperty] private bool _isStrictRadioEncryptionEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION).BoolValue;
	[ObservableProperty] private bool _isTransmissionLogEnabled = serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue;
	
	[ObservableProperty] [Range(0,7)] private int _retransmissionNodeLimit = serverSettings.GetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT).IntValue;
	[ObservableProperty] [Range(0,7)] private int _transmissionLogRetentionLimit = serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue;

	[ObservableProperty] private ObservableCollection<double> _globalLobbyFrequencies = new(
		serverSettings.GetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES).StringValue
			.Split(',').Select(Convert.ToDouble).ToList()
	);
	
	[ObservableProperty] private ObservableCollection<double> _testFrequencies = new(
		serverSettings.GetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES)
			.StringValue.Split(',').Select(Convert.ToDouble).ToList()
	);

	partial void OnIsClientExportEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, value.ToString());
	partial void OnIsCoalitionAudioSecurityEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value.ToString());
	partial void OnIsDistanceLimitEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED, value.ToString());
	partial void OnIsExternalModeEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, value.ToString());
	partial void OnIsIRLRadioRXEffectsEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, value.ToString());
	partial void OnIsIRLRadioTXEffectsEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX, value.ToString());
	partial void OnIsLineOfSightEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.LOS_ENABLED, value.ToString());
	partial void OnIsLotAtcExportEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, value.ToString());
	partial void OnIsRadioEffectOverrideOnGlobalEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, value.ToString());
	partial void OnIsRadioEncryptionAllowedChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, value.ToString());
	partial void OnIsRadioExpansionAllowedChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION, value.ToString());
	partial void OnIsServerPresetsEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS, value.ToString());
	partial void OnIsShowTransmitterNameEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, value.ToString());
	partial void OnIsShowTunedCountEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, value.ToString());
	partial void OnIsSpectatorAudioDisabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, value.ToString());
	partial void OnIsStrictRadioEncryptionEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, value.ToString());
	partial void OnIsTransmissionLogEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, value.ToString());
	partial void OnRetransmissionNodeLimitChanged(int value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, value.ToString());
	partial void OnTransmissionLogRetentionLimitChanged(int value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, value.ToString());
	
	partial void OnGlobalLobbyFrequenciesChanged(ObservableCollection<double> value)
	{
		serverSettings.SetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, String.Join(",", value));
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged());
	}
	partial void OnTestFrequenciesChanged(ObservableCollection<double> value)
	{
		serverSettings.SetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES, String.Join(",", value));
		eventAggregator.PublishOnBackgroundThreadAsync(new ServerFrequenciesChanged());
	}

	#endregion

	#region Server Settings
	[ObservableProperty] private bool _isCheckForBetaUpdatesEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue;

	[ObservableProperty] private bool _isUPNPEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.UPNP_ENABLED).BoolValue;
	[ObservableProperty] [Range(0,65535)] private int _serverPort = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;
	[ObservableProperty] [MaxLength(256)] private string _serverIP = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_IP).StringValue;

	[ObservableProperty] private bool _isHTTPServerEnabled = serverSettings.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED).BoolValue;
	[ObservableProperty] [Range(0,65535)] private int _httpServerPort = serverSettings.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT).IntValue;
	
	
	[ObservableProperty] [Range(0,65535)] private int _lotAtcExportPort = serverSettings.GetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT).IntValue;
	[ObservableProperty] [MaxLength(256)] private string _lotAtcExportIP = serverSettings.GetServerSetting(ServerSettingsKeys.LOTATC_EXPORT_IP).StringValue;

	[ObservableProperty] [MaxLength(32767)] private string _clientExportFilePath = serverSettings.GetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH).StringValue;
	[ObservableProperty] [MaxLength(32767)] private string _serverPresetsPath = serverSettings.GetServerSetting(ServerSettingsKeys.SERVER_PRESETS).StringValue;
	
	
	
	partial void OnClientExportFilePathChanged(string value) => serverSettings.SetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, value.ToString());
	partial void OnHttpServerPortChanged(int value) => serverSettings.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT, value.ToString());
	partial void OnIsCheckForBetaUpdatesEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value.ToString());
	partial void OnIsHTTPServerEnabledChanged(bool value) => serverSettings.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED, value.ToString());
	partial void OnIsUPNPEnabledChanged(bool value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.UPNP_ENABLED, value.ToString());
	partial void OnLotAtcExportIPChanged(string value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_IP, value.ToString());
	partial void OnLotAtcExportPortChanged(int value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT, value.ToString());
	partial void OnServerIPChanged(string value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_IP, value.ToString());
	partial void OnServerPortChanged(int value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_PORT, value.ToString());
	partial void OnServerPresetsPathChanged(string value) => serverSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS, value.ToString());
	
	#endregion

	#region External AWACS Mode Settings
	[ObservableProperty] [MaxLength(64)] private string _externalModePassBlue = serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD).StringValue;
	[ObservableProperty] [MaxLength(64)] private string _externalModePassRed = serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD).StringValue;

	partial void OnExternalModePassBlueChanged(string value) => serverSettings.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, value.ToString());
	partial void OnExternalModePassRedChanged(string value) => serverSettings.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, value.ToString());
	
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
		serverSettings.SetGeneralSetting(key, String.Join(",", collection));
		eventAggregator.PublishOnBackgroundThreadAsync(
			new ServerFrequenciesChanged()
			{
				TestFrequencies = string.Join(",", TestFrequencies), 
				GlobalLobbyFrequencies = string.Join(",", GlobalLobbyFrequencies)
			} 
		);
	}
}