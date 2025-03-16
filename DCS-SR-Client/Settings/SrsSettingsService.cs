using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public partial class SrsSettingsService : ObservableRecipient, ISrsSettings
{
	const string SettingsFileName = "./appsettings.json";
	
	private readonly Logger Logger = LogManager.GetCurrentClassLogger();
	
	private IConfigurationRoot _configuration;

	// Global Application Settings
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CurrentProfileName))]
	private ClientSettingsModel _clientSettings = new ClientSettingsModel();
	private Dictionary<string, ProfileSettingsModel> _profileSettings = new Dictionary<string, ProfileSettingsModel>();
	
	partial void OnClientSettingsChanged(ClientSettingsModel value)
	{
		SaveSettings();
	}
	public ProfileSettingsModel CurrentProfile
	{
		get => _profileSettings[ClientSettings.CurrentProfileName];
		set
		{
			Logger.Info(ClientSettings.CurrentProfileName + " - Profile now in use");
			_profileSettings[ClientSettings.CurrentProfileName] = value;
		}
	}

	// Only the Current Profile
	public List<string> ProfileNames => _profileSettings.Keys.ToList();
	
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CurrentProfile))]
	[NotifyPropertyChangedFor(nameof(ProfileNames))]
	private string _currentProfileName = "default";
	
	partial void OnCurrentProfileNameChanged(string value)
	{
		ClientSettings.CurrentProfileName = value;
	}

	// Connected Server Settings
	/// <summary>
	/// This is only kept in memory and not written to file.
	/// On Each connection to a server, this will be changed.
	/// </summary>
	[ObservableProperty] private ServerSettingsModel _currentServerSettings = new ServerSettingsModel();
	
	public SrsSettingsService()
	{
		if (!File.Exists(SettingsFileName)) { CreateNewAppSettings(); }			
		if (File.ReadAllBytes(SettingsFileName).Length <= 10) { CreateNewAppSettings(); }
		
		_configuration = new ConfigurationBuilder()
			.AddJsonFile(SettingsFileName, reloadOnChange: false, optional: false)
			.Build();

		_configuration.GetSection("GlobalSettings").Bind(ClientSettings);
		_configuration.GetSection("ProfileSettings").Bind(_profileSettings);
		
		var objValue = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut", "FALSE");
		if (objValue != null && objValue == "TRUE") { ClientSettings.AllowAnonymousUsage = true; }
		else { ClientSettings.AllowAnonymousUsage = false; }
		
		if (!_profileSettings.ContainsKey(ClientSettings.CurrentProfileName)) { ClientSettings.CurrentProfileName = "default"; }
		OnPropertyChanged(nameof(CurrentProfileName));
		
		WeakReferenceMessenger.Default.Register<SettingChangingMessage>(this, (r, m) =>
		{
			OnPropertyChanging();
			switch (m.ChangeType)
			{
				case SettingCatagory.Client:
					SaveSettings(ClientSettings, $"./{ClientSettingsFileName}.json");
					break;
				case SettingCatagory.Profile:
					SaveSettings(CurrentProfile, $"./{CurrentProfile}_{ProfileFileNameSuffix}.json");
					break;
			}
		});
	}
	
	public class SettingsModel
	{
		public string SettingsVersion = "1.0";
		public ClientSettingsModel ClientSettings = new ClientSettingsModel();
		public Dictionary<string, ProfileSettingsModel> ProfileSettings =
			new() { {"default", new ProfileSettingsModel() } };
	}
	
	[RelayCommand]
	private void CreateProfile(string profileName)
	{
		ProfileSettingsModel newProfile = new ProfileSettingsModel();
		
		_profileSettings.Add(profileName, newProfile);
		CurrentProfileName = $"{profileName}";
		OnPropertyChanged(nameof(CurrentProfileName));
	}

	[RelayCommand] private void RenameProfile(string profileName)
	{
		string oldProfileName = CurrentProfileName;
		ProfileSettingsModel newProfile = (ProfileSettingsModel)CurrentProfile.Clone();
		_profileSettings.Add(profileName, newProfile);
		_profileSettings.Remove(oldProfileName);
		
		CurrentProfileName = $"{profileName}";
		OnPropertyChanged(nameof(CurrentProfileName));
	}

	[RelayCommand] private void DuplicateProfile(string profileName)
	{
		ProfileSettingsModel newProfile = (ProfileSettingsModel)CurrentProfile.Clone();
		
		_profileSettings.Add(profileName, newProfile);
		CurrentProfileName = $"{profileName}";
		OnPropertyChanged(nameof(CurrentProfileName));
	}

	[RelayCommand] private void DeleteProfile(string profileName)
	{
		if (profileName == "default") { return; }
		_profileSettings.Remove(profileName);
		CurrentProfileName = "default";
		OnPropertyChanged(nameof(CurrentProfileName));
	}

	private bool isSaving = false; // quick and dirty locking to avoid spamming saves.
	public void SaveSettings()
	{
		if (isSaving) { return; }
		try
		{
			isSaving = true;
			SettingsModel temp = new SettingsModel() { ClientSettings = ClientSettings, ProfileSettings = _profileSettings };
			string json = JsonConvert.SerializeObject(temp, Formatting.Indented);
			File.WriteAllText(SettingsFileName, json, Encoding.UTF8);
			isSaving = false;
		}
		catch (Exception e)
		{
			
		}
	}
	
	public void CreateNewAppSettings()
	{
		string json = JsonConvert.SerializeObject(new SettingsModel(), Formatting.Indented);
		File.WriteAllText(SettingsFileName, json, Encoding.UTF8);
	}

	
	public void Decode(Dictionary<string, string> encodedServerSettings)
	{
		ServerSettingsModel incomingSettings = new ServerSettingsModel();
		foreach (KeyValuePair<string, string> kvp in encodedServerSettings)
		{
			switch (kvp.Key)
			{
				case "0": case "SERVER_PORT":
					incomingSettings.ServerPort = int.Parse(kvp.Value);
					break;
				case "1": case "COALITION_AUDIO_SECURITY": 
					incomingSettings.IsCoalitionAudioSeperated = bool.Parse(kvp.Value);
					break; 
				case "2": case "SPECTATORS_AUDIO_DISABLED":
					incomingSettings.IsSpectatorsAudioDisabled = bool.Parse(kvp.Value);
					break;
				case "3": case "CLIENT_EXPORT_ENABLED":
					incomingSettings.IsClientExportAllowed = bool.Parse(kvp.Value);
					break;
				case "4": case "LOS_ENABLED":
					incomingSettings.IsLineOfSightCheckingEnabled = bool.Parse(kvp.Value);
					break;
				case "5": case "DISTANCE_ENABLED": 
					incomingSettings.IsDistanceCheckingEnabled = bool.Parse(kvp.Value);
					break;
				case "6": case "IRL_RADIO_TX": 
					incomingSettings.IsRadioTxEffectsEnabled = bool.Parse(kvp.Value);
					break;
				case "7": case "IRL_RADIO_RX_INTERFERENCE": 
					incomingSettings.IsRadioRxInterferenceEnabled = bool.Parse(kvp.Value);
					break;
				case "8": case "IRL_RADIO_STATIC": 
					incomingSettings.IsRadioStaticEffectsEnabled = bool.Parse(kvp.Value);
					break;
				case "9": case "RADIO_EXPANSION": 
					incomingSettings.IsExpandedRadiosAllowed = bool.Parse(kvp.Value);
					break;
				case "10": case "EXTERNAL_AWACS_MODE": 
					incomingSettings.IsExternalModeAllowed = bool.Parse(kvp.Value);
					break;
				case "11": case "EXTERNAL_AWACS_MODE_BLUE_PASSWORD": 
					incomingSettings.ExternalModeBluePassword = kvp.Value;
					break;
				case "12": case "EXTERNAL_AWACS_MODE_RED_PASSWORD": 
					incomingSettings.ExternalModeRedPassword = kvp.Value;
					break;
				case "13": case "CLIENT_EXPORT_FILE_PATH": 
					incomingSettings.ClientExportFileName = kvp.Value;
					break;
				case "14": case "CHECK_FOR_BETA_UPDATES": 
					incomingSettings.CheckForBetaUpdates = bool.Parse(kvp.Value);
					break;
				case "15": case "ALLOW_RADIO_ENCRYPTION": 
					incomingSettings.IsRadioEncryptionAllowed = bool.Parse(kvp.Value);
					break;
				case "16": case "TEST_FREQUENCIES": 
					incomingSettings.TestFrequencies = kvp.Value;
					break;
				case "17": case "SHOW_TUNED_COUNT": 
					incomingSettings.IsShowTunedListenerCount = bool.Parse(kvp.Value);
					break;
				case "18": case "GLOBAL_LOBBY_FREQUENCIES": 
					incomingSettings.GlobalLobbyFrequencies = kvp.Value;
					break;
				case "19": case "SHOW_TRANSMITTER_NAME": 
					incomingSettings.IsShowTransmitterNameEnabled = bool.Parse(kvp.Value);
					break;
				case "20": case "LOTATC_EXPORT_ENABLED": 
					incomingSettings.IsLotAtcExportEnabled = bool.Parse(kvp.Value);
					break;
				case "21": case "LOTATC_EXPORT_PORT": 
					incomingSettings.LotAtcExportPort = int.Parse(kvp.Value);
					break;
				case "22": case "LOTATC_EXPORT_IP": 
					incomingSettings.LotAtcExportIp = kvp.Value;
					break;
				case "23": case "UPNP_ENABLED": 
					incomingSettings.IsUpnpEnabled = bool.Parse(kvp.Value);
					break;
				case "24": case "RETRANSMISSION_NODE_LIMIT": 
					incomingSettings.RetransmissionNodeLimit = int.Parse(kvp.Value);
					break;
				case "25": case "STRICT_RADIO_ENCRYPTION": 
					incomingSettings.IsStrictRadioEncryptionEnabled = bool.Parse(kvp.Value);
					break;
				case "26": case "TRANSMISSION_LOG_ENABLED": 
					incomingSettings.IsTransmissionLogEnabled = bool.Parse(kvp.Value);
					break;
				case "27": case "TRANSMISSION_LOG_RETENTION": 
					incomingSettings.TransmissionLogRetentionLimit = int.Parse(kvp.Value);
					break;
				case "28": case "RADIO_EFFECT_OVERRIDE": 
					incomingSettings.IsRadioEffectOverrideEnabled = bool.Parse(kvp.Value);
					break;
				case "29": case "SERVER_IP": 
					incomingSettings.ServerIp = kvp.Value;
					break;
			}
		}
		CurrentServerSettings = incomingSettings;
	}
	
	public void Receive(SettingChangingMessage message)
	{ }
}



