using System;
using System.Collections.Generic;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public class ProfileSettingStoreFacade
{
	// Profile Setting Store
	public Dictionary<string, Dictionary<InputBinding, InputDevice>> InputProfiles { get; set; } = new Dictionary<string, Dictionary<InputBinding, InputDevice>>();
	public string CurrentProfileName;
	public List<string> ProfileNames;
	
	public List<string> GetProfiles()
	{
		throw new NotImplementedException();
	}
	public void AddNewProfile(string profileName)
	{
		throw new NotImplementedException();
	}
	private string GetProfileName(string cfg)
	{
		throw new NotImplementedException();
	}
	public InputDevice GetControlSetting(InputBinding key, Configuration configuration)
	{
		throw new NotImplementedException();
	}
	public void SetControlSetting(InputDevice device)
	{
		throw new NotImplementedException();
	}
	public void RemoveControlSetting(InputBinding binding)
	{
		throw new NotImplementedException();
	}
	private Setting GetSetting(string section, string setting)
	{
		throw new NotImplementedException();
	}
	public bool GetClientSettingBool(ProfileSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public float GetClientSettingFloat(ProfileSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public string GetClientSettingString(ProfileSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public void SetClientSettingBool(ProfileSettingsKeys key, bool value)
	{
		throw new NotImplementedException();
	}
	public void SetClientSettingFloat(ProfileSettingsKeys key, float value)
	{
		throw new NotImplementedException();
	}
	public void SetClientSettingString(ProfileSettingsKeys key, string value)
	{
		throw new NotImplementedException();
	}
	private void SetSetting(string section, string key, object setting)
	{
		throw new NotImplementedException();
	}
	public void RemoveProfile(string profile)
	{
		throw new NotImplementedException();
	}
	public void RenameProfile(string oldName,string newName)
	{
		throw new NotImplementedException();
	}
	public void CopyProfile(string profileToCopy, string profileName)
	{
		throw new NotImplementedException();
	}
}