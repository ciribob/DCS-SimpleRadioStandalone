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
	private GlobalSettingsModel _globalSettings = new GlobalSettingsModel();
	private Dictionary<string, ProfileSettingsModel> _profileSettings = new Dictionary<string, ProfileSettingsModel>();
	
	partial void OnGlobalSettingsChanged(GlobalSettingsModel value)
	{
		SaveSettings();
	}
	public ProfileSettingsModel CurrentProfile
	{
		get => _profileSettings[GlobalSettings.CurrentProfileName];
		set
		{
			Logger.Info(GlobalSettings.CurrentProfileName + " - Profile now in use");
			_profileSettings[GlobalSettings.CurrentProfileName] = value;
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
		GlobalSettings.CurrentProfileName = value;
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

		_configuration.GetSection("GlobalSettings").Bind(GlobalSettings);
		_configuration.GetSection("ProfileSettings").Bind(_profileSettings);
		
		var objValue = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut", "FALSE");
		if (objValue != null && objValue == "TRUE") { GlobalSettings.AllowAnonymousUsage = true; }
		else { GlobalSettings.AllowAnonymousUsage = false; }
		
		if (!_profileSettings.ContainsKey(GlobalSettings.CurrentProfileName)) { GlobalSettings.CurrentProfileName = "default"; }
		OnPropertyChanged(nameof(CurrentProfileName));
		
		WeakReferenceMessenger.Default.Register<SettingChangingMessage>(this, (r, m) =>
		{
			OnPropertyChanging();
			SaveSettings();
		});
	}
	
	public class SettingsModel
	{
		public string SettingsVersion = "1.0";
		public GlobalSettingsModel GlobalSettings = new GlobalSettingsModel();
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
			SettingsModel temp = new SettingsModel() { GlobalSettings = GlobalSettings, ProfileSettings = _profileSettings };
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

	public void Receive(SettingChangingMessage message)
	{ }
}



