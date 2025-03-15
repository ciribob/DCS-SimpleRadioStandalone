using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public interface ISrsSettings
{
	GlobalSettingsModel GlobalSettings { get; }
	ProfileSettingsModel CurrentProfile { get; }
	List<string> ProfileNames { get; }
	string CurrentProfileName { get; }

	IRelayCommand<string> CreateProfileCommand { get; }
	IRelayCommand<string> RenameProfileCommand { get; }
	IRelayCommand<string> DuplicateProfileCommand { get; }
	IRelayCommand<string> DeleteProfileCommand { get; }
}