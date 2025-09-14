using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

public struct SynchronizedSettings
{
	private readonly ServerSettingsStore Store { get; }
	private const string SectionName = "General Settings";
	
	public SynchronizedSettings(ServerSettingsStore store)
	{
		Store = store;
	}

	public bool IsClientExportEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, value);
	}
	public bool IsCoalitionAudioSecurityEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value);
	}
	public bool IsDistanceLimitEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED, value);
	}
	public bool IsExternalModeEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, value);
	}
	public bool IsIrlRadioRxEffectsEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, value);
	}
	public bool IsIrlRadioTxEffectsEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX, value);
	}
	public bool IsLineOfSightEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.LOS_ENABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.LOS_ENABLED, value);
	}
	public bool IsLotAtcExportEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, value);
	}
	public bool IsRadioEffectOverrideOnGlobalEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, value);
	}
	public bool IsRadioEncryptionAllowed
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, value);
	}
	public bool IsRadioExpansionAllowed
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION, value);
	}
	public bool IsServerPresetsEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS_ENABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS_ENABLED, value);
	}
	public bool IsShowTransmitterNameEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, value);
	}
	public bool IsShowTunedCountEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, value);
	}
	public bool IsSpectatorAudioDisabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, value);
	}
	public bool IsStrictRadioEncryptionEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, value);
	}
	public bool IsTransmissionLogEnabled
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, value);
	}
	public int RetransmissionNodeLimit
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT).IntValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, value);
	}
	public int TransmissionLogRetentionLimit
	{
		get => Store.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue;
		set => Store.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, value);
	}

	public List<double> GlobalLobbyFrequencies
	{
		get
		{
			return new List<double>(
				(Store.GetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES).StringValue)
				.Split(',').Select(double.Parse).ToList()
			);
		}
		set
		{
			Store.SetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, String.Join(",", value));
		}
	}

	public List<double> TestFrequencies
	{
		get
		{
			return new List<double>(
				(Store.GetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES).StringValue)
				.Split(',').Select(double.Parse).ToList()
			);
		}
		set
		{
			Store.SetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES, String.Join(",", value));
		}
	}
}

public struct ExternalModeSettings
{
	private const string SectionName = "External AWACS Mode Settings";
	private ServerSettingsStore Store { get; }
	
	public ExternalModeSettings(ServerSettingsStore store)
	{
		Store = store;
	}
	
	public string ExternalModePassBlue
	{
		get => Store.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD).StringValue;
		set => Store.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, value);
	}
	public string ExternalModePassRed
	{
		get => Store.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD).StringValue;
		set => Store.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, value);
	}
}

public struct ServerSettings
{
	private const string SectionName = "Server Settings";
	private ServerSettingsStore Store { get; }
	
	public ServerSettings(ServerSettingsStore store)
	{
		Store = store;
	}
	
	public bool IsCheckForBetaUpdatesEnabled
	{
		get => Store.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue;
		set => Store.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value);
	}

	public bool IsUpnpEnabled
	{
		get => Store.GetServerSetting(ServerSettingsKeys.UPNP_ENABLED).BoolValue;
		set => Store.SetServerSetting(ServerSettingsKeys.UPNP_ENABLED, value);
	}
	public int ServerPort
	{
		get => Store.GetServerSetting(ServerSettingsKeys.SERVER_PORT).IntValue;
		set => Store.SetServerSetting(ServerSettingsKeys.SERVER_PORT, value);
	}
	public IPAddress ServerBindIp
	{
		get
		{
			var str = Store.GetServerSetting(ServerSettingsKeys.SERVER_IP).RawValue;
			if (IPAddress.TryParse(str, out var address)) return address;
			return IPAddress.Any;
		}
		set => Store.SetServerSetting(ServerSettingsKeys.SERVER_IP, value.ToString());
	}

	public bool IsHttpServerEnabled
	{
		get => Store.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED).BoolValue;
		set => Store.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED, value);
	}
	public int HttpServerPort
	{
		get => Store.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT).IntValue;
		set => Store.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT, value);
	}
	public string HttpServerApiKey
	{
		get => Store.GetServerSetting(ServerSettingsKeys.HTTP_SERVER_API_KEY).RawValue.Trim();
		set => Store.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_API_KEY, value.Trim());
	}
	
	public int LotAtcExportPort
	{
		get => Store.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).IntValue;
		set => Store.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value);
	}
	public IPAddress LotAtcExportIp
	{
		get
		{
			var str = Store.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).RawValue;
			if (IPAddress.TryParse(str, out var address)) return address;
			return IPAddress.Any;
		}
		set => Store.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value.ToString());
	}

	public string ClientExportFilePath
	{
		get => Store.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).StringValue;
		set => Store.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value);
	}
	
	public string ServerPresetsPath
	{
		get => Store.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).StringValue;
		set => Store.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value);
	}
}