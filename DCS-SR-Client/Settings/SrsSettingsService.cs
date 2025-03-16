using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
	const string ClientSettingsFileName = "ClientSettings";
	const string ProfileFileNameSuffix = "Profile";
	private readonly string _configurationDirectoryPath = "\\Configuration\\";
	
	private readonly Logger Logger = LogManager.GetCurrentClassLogger();
	
	private IConfigurationRoot _configuration;

	// Global Application Settings
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CurrentProfileName))]
	private ClientSettingsModel _clientSettings = new ClientSettingsModel();

	private Dictionary<string, ProfileSettingsModel> _profileSettings = new Dictionary<string, ProfileSettingsModel>();
	
	public ProfileSettingsModel CurrentProfile
	{
		get => _profileSettings[ClientSettings.LastUsedProfileName];
		set
		{
			Logger.Info(ClientSettings.LastUsedProfileName + " - Profile now in use");
			_profileSettings[ClientSettings.LastUsedProfileName] = value;
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
		ClientSettings.LastUsedProfileName = value;
	}

	// Connected Server Settings
	/// <summary>
	/// This is only kept in memory and not written to file.
	/// On Each connection to a server, this will be changed.
	/// </summary>
	[ObservableProperty] private ServerSettingsModel _currentServerSettings = new ServerSettingsModel();
	
	public SrsSettingsService()
	{
		if (!File.Exists(ClientSettingsFileName) || 
		    File.ReadAllBytes(ClientSettingsFileName).Length <= 10) 
		{ CreateNewAppSettings(); }

		_configuration = new ConfigurationBuilder()
			.AddJsonFile(ClientSettingsFileName, reloadOnChange: false, optional: true)
			.Build();
		
		if (Directory.Exists(_configurationDirectoryPath))
		{
			foreach (string relativePathToFiles in Directory.EnumerateFiles(_configurationDirectoryPath,
				         $"*_{ProfileFileNameSuffix}.json",
				         SearchOption.AllDirectories))
			{
				string text = File.ReadAllText(relativePathToFiles);
				var profilejson = JsonConvert.DeserializeObject<ProfileSettingsModel>(text,
					new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

				string[] NameWithoutFolder = relativePathToFiles.Split(_configurationDirectoryPath);
				string[] ProfileNameParts = NameWithoutFolder[1].Split($"_{ProfileFileNameSuffix}.json");

				_profileSettings.Add(ProfileNameParts[0], profilejson);
			}
		}

		_configuration.GetSection("GlobalSettings").Bind(ClientSettings);
		_configuration.GetSection("ProfileSettings").Bind(_profileSettings);
		
		var objValue = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone", "SRSAnalyticsOptOut", "FALSE");
		if (objValue != null && objValue == "TRUE") { ClientSettings.AllowAnonymousUsage = true; }
		else { ClientSettings.AllowAnonymousUsage = false; }
		
		if (!_profileSettings.ContainsKey(ClientSettings.LastUsedProfileName)) { ClientSettings.LastUsedProfileName = "default"; }
		OnPropertyChanged(nameof(CurrentProfileName));
		
		WeakReferenceMessenger.Default.Register<SettingChangingMessage>(this, (r, m) =>
		{
			OnPropertyChanging();
			switch (m.ChangeType)
			{
				case SettingCatagory.Client:
					SaveSettings(ClientSettings, $"{ClientSettingsFileName}.json");
					break;
				case SettingCatagory.Profile:
					SaveSettings(CurrentProfile, $"{CurrentProfileName}_{ProfileFileNameSuffix}.json");
					break;
			}
		});
	}
	
	public class SettingsModel
	{
		public string SettingsVersion = "1.0";
		public ClientSettingsModel ClientSettings = new ClientSettingsModel();
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
	
	
	// Todo: Make this save method more robust with a Queue or something
	private bool isSaving = false; // quick and dirty locking to avoid spamming saves.
	public void SaveSettings(object ModelToSave, string Filename)
	{
		if (isSaving) { return; }
		try
		{
			if (!Directory.Exists(_configurationDirectoryPath))
			{
				Directory.CreateDirectory(_configurationDirectoryPath);
			};
			var path = Path.Join(_configurationDirectoryPath, Filename); 
			
			isSaving = true;
			string json = JsonConvert.SerializeObject(ModelToSave, Formatting.Indented);
			File.WriteAllTextAsync(path, json, Encoding.UTF8);
			isSaving = false;
		}
		catch (Exception e) { }
	}
	
	public void CreateNewAppSettings()
	{
		string json = JsonConvert.SerializeObject(new SettingsModel(), Formatting.Indented);
		File.WriteAllText($"./{ClientSettingsFileName}.json", json, Encoding.UTF8);
	}

	public void Receive(SettingChangingMessage message)
	{ }
}



