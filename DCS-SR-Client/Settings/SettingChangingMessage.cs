namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

public class SettingChangingMessage(SettingCatagory category)
{
	public SettingCatagory ChangeType = category;
}
public enum SettingCatagory
{
	Client,
	Profile,
	Server
}