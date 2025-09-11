using System;
using System.Collections.Generic;
using System.ComponentModel;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;

public enum ServerSettingsKeys
{
    [SrIntegerSetting(ServerSettingsKeysExtensions.GeneralSettingSection, 5002, 0, 65535)]
    SERVER_PORT = 0,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    COALITION_AUDIO_SECURITY = 1,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    SPECTATORS_AUDIO_DISABLED = 2,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    CLIENT_EXPORT_ENABLED = 3,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    LOS_ENABLED = 4,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    DISTANCE_ENABLED = 5,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    IRL_RADIO_TX = 6,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    IRL_RADIO_RX_INTERFERENCE = 7,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    IRL_RADIO_STATIC = 8, // Not used
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    RADIO_EXPANSION = 9,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    EXTERNAL_AWACS_MODE = 10,
    
    [SrStringSetting(ServerSettingsKeysExtensions.ExternalSettingsSection, "", 0, 256)]
    EXTERNAL_AWACS_MODE_BLUE_PASSWORD = 11,
    
    [SrStringSetting(ServerSettingsKeysExtensions.ExternalSettingsSection, "", 0, 256)]
    EXTERNAL_AWACS_MODE_RED_PASSWORD = 12,
    
    [SrStringSetting(ServerSettingsKeysExtensions.ServerSettingsSection, "", 0, 256)]
    CLIENT_EXPORT_FILE_PATH = 13,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    CHECK_FOR_BETA_UPDATES = 14,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, true)]
    ALLOW_RADIO_ENCRYPTION = 15,
    
    [SrStringSetting(ServerSettingsKeysExtensions.GeneralSettingSection, "247.2,120.3", 0, 256)]
    TEST_FREQUENCIES = 16,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, true)]
    SHOW_TUNED_COUNT = 17,
    
    [SrStringSetting(ServerSettingsKeysExtensions.GeneralSettingSection, "248.22", 0, 256)]
    GLOBAL_LOBBY_FREQUENCIES = 18,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    SHOW_TRANSMITTER_NAME = 19,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    LOTATC_EXPORT_ENABLED = 20,
    
    [SrIntegerSetting(ServerSettingsKeysExtensions.ServerSettingsSection, 10712, 0, 65535)]
    LOTATC_EXPORT_PORT = 21,
    
    [SrStringSetting(ServerSettingsKeysExtensions.ServerSettingsSection, "127.0.0.1", 0, 256)]
    LOTATC_EXPORT_IP = 22,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, true)]
    UPNP_ENABLED = 23,
    
    [SrIntegerSetting(ServerSettingsKeysExtensions.GeneralSettingSection, 0, 0, 7)]
    RETRANSMISSION_NODE_LIMIT = 24,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    STRICT_RADIO_ENCRYPTION = 25,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    TRANSMISSION_LOG_ENABLED = 26,
    
    [SrIntegerSetting(ServerSettingsKeysExtensions.GeneralSettingSection, 2, 0, 7)]
    TRANSMISSION_LOG_RETENTION = 27,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    RADIO_EFFECT_OVERRIDE = 28,
    
    [SrStringSetting(ServerSettingsKeysExtensions.ServerSettingsSection, "0.0.0.0", 0, 256)]
    SERVER_IP = 29,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    SERVER_PRESETS_ENABLED = 30,
    
    [SrStringSetting(ServerSettingsKeysExtensions.ServerSettingsSection, "", 0, 65535)]
    SERVER_PRESETS = 31,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    HTTP_SERVER_ENABLED = 32,
    
    [SrIntegerSetting(ServerSettingsKeysExtensions.ServerSettingsSection, 10712, 0, 65535)]
    HTTP_SERVER_PORT = 33,
    
    [SrStringSetting(ServerSettingsKeysExtensions.GeneralSettingSection, "0.0.0.0", 0, 256)]
    HTTP_SERVER_API_KEY = 34,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.ServerSettingsSection, false)]
    SERVER_EAM_RADIO_PRESET_ENABLED = 35,
    
    [SrStringSetting(ServerSettingsKeysExtensions.GeneralSettingSection, "", 0, 65535)]
    SERVER_EAM_RADIO_PRESET = 36,
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SrStringSettingAttribute : Attribute
{
    public string Section;
    public string DefaultValue;
    public int MinimumLength;
    public int MaximumLength;
    
    public SrStringSettingAttribute(string section, string defaultValue, int minimumLength, int maximumLength)
    {
        Section = section;
        DefaultValue = defaultValue;
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SrIntegerSettingAttribute : Attribute
{
    public string Section;
    public int DefaultValue;
    public int MinimumLength;
    public int MaximumLength;
    
    public SrIntegerSettingAttribute(string section, int defaultValue, int minimumValue, int maximumValue)
    {
        Section = section;
        DefaultValue = defaultValue;
        MinimumLength = minimumValue;
        MaximumLength = maximumValue;
    }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SrBooleanSettingAttribute : Attribute
{
    public string Section;
    public bool DefaultValue;
    
    public SrBooleanSettingAttribute(string section, bool defaultValue)
    {
        Section = section;
        DefaultValue = defaultValue;
    }
}

public static class ServerSettingsKeysExtensions
{
    public const string GeneralSettingSection = "General Settings";
    public const string ServerSettingsSection = "Server Settings";
    public const string ExternalSettingsSection = "External AWACS Mode Settings";
}

public class DefaultServerSettings
{
    public static readonly Dictionary<string, string> Defaults = new()
    {
        { ServerSettingsKeys.CLIENT_EXPORT_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.COALITION_AUDIO_SECURITY.ToString(), "false" },
        { ServerSettingsKeys.DISTANCE_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE.ToString(), "false" },
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD.ToString(), "" },
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD.ToString(), "" },
        { ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE.ToString(), "false" },
        { ServerSettingsKeys.IRL_RADIO_STATIC.ToString(), "false" },
        { ServerSettingsKeys.IRL_RADIO_TX.ToString(), "false" },
        { ServerSettingsKeys.LOS_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.RADIO_EXPANSION.ToString(), "false" },
        { ServerSettingsKeys.SERVER_PORT.ToString(), "5002" },
        { ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED.ToString(), "false" },
        { ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH.ToString(), "clients-list.json" },
        { ServerSettingsKeys.CHECK_FOR_BETA_UPDATES.ToString(), "false" },
        { ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION.ToString(), "true" },
        { ServerSettingsKeys.TEST_FREQUENCIES.ToString(), "247.2,120.3" },
        { ServerSettingsKeys.SHOW_TUNED_COUNT.ToString(), "true" },
        { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString(), "248.22" },
        { ServerSettingsKeys.LOTATC_EXPORT_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.LOTATC_EXPORT_PORT.ToString(), "10712" },
        { ServerSettingsKeys.LOTATC_EXPORT_IP.ToString(), "127.0.0.1" },
        { ServerSettingsKeys.UPNP_ENABLED.ToString(), "true" },
        { ServerSettingsKeys.SHOW_TRANSMITTER_NAME.ToString(), "false" },
        { ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT.ToString(), "0" },
        { ServerSettingsKeys.STRICT_RADIO_ENCRYPTION.ToString(), "false" },
        { ServerSettingsKeys.TRANSMISSION_LOG_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.TRANSMISSION_LOG_RETENTION.ToString(), "2" },
        { ServerSettingsKeys.RADIO_EFFECT_OVERRIDE.ToString(), "false" },
        { ServerSettingsKeys.SERVER_IP.ToString(), "0.0.0.0" },
        { ServerSettingsKeys.SERVER_PRESETS_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.HTTP_SERVER_ENABLED.ToString(), "false" },
        { ServerSettingsKeys.HTTP_SERVER_PORT.ToString(), "8080" },
        { ServerSettingsKeys.HTTP_SERVER_API_KEY.ToString(), ShortGuid.NewGuid() },
        { ServerSettingsKeys.SERVER_EAM_RADIO_PRESET_ENABLED.ToString(), "false" },
    };
}