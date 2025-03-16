using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public interface ISrsSettings
{
	ClientSettingsModel ClientSettings { get; }
	ProfileSettingsModel CurrentProfile { get; }
	ServerSettingsModel CurrentServerSettings { get; }
	
	List<string> ProfileNames { get; }
	string CurrentProfileName { get; set; }

	IRelayCommand<string> CreateProfileCommand { get; }
	IRelayCommand<string> RenameProfileCommand { get; }
	IRelayCommand<string> DuplicateProfileCommand { get; }
	IRelayCommand<string> DeleteProfileCommand { get; }
}