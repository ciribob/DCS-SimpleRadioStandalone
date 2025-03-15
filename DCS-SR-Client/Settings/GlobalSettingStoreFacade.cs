using System;
using System.Collections.Generic;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public class GlobalSettingStoreFacade
{
	// Gobal Setting Store
	public int GetClientSettingInt(GlobalSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public void SetClientSettings(GlobalSettingsKeys key, string[] strArray)
	{
		throw new NotImplementedException();
	}

	public Setting GetPositionSetting(GlobalSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public void SetPositionSetting(GlobalSettingsKeys key, double value)
	{
		throw new NotImplementedException();
	}

	public double GetClientSettingDouble(GlobalSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public bool GetClientSettingBool(GlobalSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public Setting GetClientSetting(GlobalSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public void SetClientSetting(GlobalSettingsKeys key, string value)
	{
		throw new NotImplementedException();
	}
	public void SetClientSetting(GlobalSettingsKeys key, bool value)
	{
		throw new NotImplementedException();
	}
	public void SetClientSetting(GlobalSettingsKeys key, int value)
	{
		throw new NotImplementedException();
	}
	public void SetClientSetting(GlobalSettingsKeys key, double value)
	{
		throw new NotImplementedException();
	}
	public int GetNetworkSetting(GlobalSettingsKeys key)
	{
		throw new NotImplementedException();
	}
	public void SetNetworkSetting(GlobalSettingsKeys key, int value)
	{
		throw new NotImplementedException();
	}
	private Setting GetSetting(string section, string setting)
	{
		throw new NotImplementedException();
	}
	private void SetSetting(string section, string key, object setting)
	{
		throw new NotImplementedException();
	}
	private void Save()
	{
		throw new NotImplementedException();
	}
}