using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public partial class ServerSettingsModel : ObservableObject
{
	protected override void OnPropertyChanging(PropertyChangingEventArgs e)
	{
		WeakReferenceMessenger.Default.Send(new SettingChangingMessage(SettingCatagory.Server));
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
	[ObservableProperty] private bool _isRadioEncryptionAllowed = false;
	[ObservableProperty] private bool _isExternalModeAllowed = false;
	[ObservableProperty] private string _externalModeBluePassword = string.Empty;
	[ObservableProperty] private string _externalModeRedPassword = string.Empty;
	
	[ObservableProperty] private bool _isCoalitionAudioSeperated = false;
	
	[ObservableProperty] private bool _isDistanceCheckingEnabled = false;
	[ObservableProperty] private bool _isRadioRxInterferenceEnabled = false;
	[ObservableProperty] private bool _isLineOfSightCheckingEnabled = false;
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

	[JsonIgnore] public List<double> GlobalLobbyFrequenciesList => FreqStringToList(GlobalLobbyFrequencies);
	[JsonIgnore] public List<double> TestFrequenciesList => FreqStringToList(TestFrequencies);
	
	private List<double> FreqStringToList(string freqString)
	{
		List<double> frequencies = new List<double>();
		foreach (var freq in freqString.Split(','))
		{
			freq.Trim();
			frequencies.Add(double.Parse(freq));
		}
		
		return frequencies;
	}
	
	public void Decode(Dictionary<string, string> encodedServerSettings)
	{
		foreach (KeyValuePair<string, string> kvp in encodedServerSettings)
		{
			switch (kvp.Key)
			{
				case "0": case "SERVER_PORT":
					ServerPort = int.Parse(kvp.Value);
					break;
				case "1": case "COALITION_AUDIO_SECURITY": 
					IsCoalitionAudioSeperated = bool.Parse(kvp.Value);
					break; 
				case "2": case "SPECTATORS_AUDIO_DISABLED":
					IsSpectatorsAudioDisabled = bool.Parse(kvp.Value);
					break;
				case "3": case "CLIENT_EXPORT_ENABLED":
					IsClientExportAllowed = bool.Parse(kvp.Value);
					break;
				case "4": case "LOS_ENABLED":
					IsLineOfSightCheckingEnabled = bool.Parse(kvp.Value);
					break;
				case "5": case "DISTANCE_ENABLED": 
					IsDistanceCheckingEnabled = bool.Parse(kvp.Value);
					break;
				case "6": case "IRL_RADIO_TX": 
					IsRadioTxEffectsEnabled = bool.Parse(kvp.Value);
					break;
				case "7": case "IRL_RADIO_RX_INTERFERENCE": 
					IsRadioRxInterferenceEnabled = bool.Parse(kvp.Value);
					break;
				case "8": case "IRL_RADIO_STATIC": 
					IsRadioStaticEffectsEnabled = bool.Parse(kvp.Value);
					break;
				case "9": case "RADIO_EXPANSION": 
					IsExpandedRadiosAllowed = bool.Parse(kvp.Value);
					break;
				case "10": case "EXTERNAL_AWACS_MODE": 
					IsExternalModeAllowed = bool.Parse(kvp.Value);
					break;
				case "11": case "EXTERNAL_AWACS_MODE_BLUE_PASSWORD": 
					ExternalModeBluePassword = kvp.Value;
					break;
				case "12": case "EXTERNAL_AWACS_MODE_RED_PASSWORD": 
					ExternalModeRedPassword = kvp.Value;
					break;
				case "13": case "CLIENT_EXPORT_FILE_PATH": 
					ClientExportFileName = kvp.Value;
					break;
				case "14": case "CHECK_FOR_BETA_UPDATES": 
					CheckForBetaUpdates = bool.Parse(kvp.Value);
					break;
				case "15": case "ALLOW_RADIO_ENCRYPTION": 
					IsRadioEncryptionAllowed = bool.Parse(kvp.Value);
					break;
				case "16": case "TEST_FREQUENCIES": 
					TestFrequencies = kvp.Value;
					break;
				case "17": case "SHOW_TUNED_COUNT": 
					IsShowTunedListenerCount = bool.Parse(kvp.Value);
					break;
				case "18": case "GLOBAL_LOBBY_FREQUENCIES": 
					GlobalLobbyFrequencies = kvp.Value;
					break;
				case "19": case "SHOW_TRANSMITTER_NAME": 
					IsShowTransmitterNameEnabled = bool.Parse(kvp.Value);
					break;
				case "20": case "LOTATC_EXPORT_ENABLED": 
					IsLotAtcExportEnabled = bool.Parse(kvp.Value);
					break;
				case "21": case "LOTATC_EXPORT_PORT": 
					LotAtcExportPort = int.Parse(kvp.Value);
					break;
				case "22": case "LOTATC_EXPORT_IP": 
					LotAtcExportIp = kvp.Value;
					break;
				case "23": case "UPNP_ENABLED": 
					IsUpnpEnabled = bool.Parse(kvp.Value);
					break;
				case "24": case "RETRANSMISSION_NODE_LIMIT": 
					RetransmissionNodeLimit = int.Parse(kvp.Value);
					break;
				case "25": case "STRICT_RADIO_ENCRYPTION": 
					IsStrictRadioEncryptionEnabled = bool.Parse(kvp.Value);
					break;
				case "26": case "TRANSMISSION_LOG_ENABLED": 
					IsTransmissionLogEnabled = bool.Parse(kvp.Value);
					break;
				case "27": case "TRANSMISSION_LOG_RETENTION": 
					TransmissionLogRetentionLimit = int.Parse(kvp.Value);
					break;
				case "28": case "RADIO_EFFECT_OVERRIDE": 
					IsRadioEffectOverrideEnabled = bool.Parse(kvp.Value);
					break;
				case "29": case "SERVER_IP": 
					ServerIp = kvp.Value;
					break;
			}
		}
	}
}