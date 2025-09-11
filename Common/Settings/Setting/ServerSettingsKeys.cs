using System;
using System.Collections.Generic;
using System.ComponentModel;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;

public enum ServerSettingsKeys
{
    [SrIntegerSetting(ServerSettingsKeysExtensions.GeneralSettingSection, 5002, 0, 20000)]
    SERVER_PORT = 0,
    
    [SrBooleanSetting(ServerSettingsKeysExtensions.GeneralSettingSection, false)]
    COALITION_AUDIO_SECURITY = 1,
    
    
    SPECTATORS_AUDIO_DISABLED = 2,
    CLIENT_EXPORT_ENABLED = 3,
    LOS_ENABLED = 4,
    DISTANCE_ENABLED = 5,
    IRL_RADIO_TX = 6,
    IRL_RADIO_RX_INTERFERENCE = 7,
    IRL_RADIO_STATIC = 8, // Not used
    RADIO_EXPANSION = 9,
    EXTERNAL_AWACS_MODE = 10,
    EXTERNAL_AWACS_MODE_BLUE_PASSWORD = 11,
    EXTERNAL_AWACS_MODE_RED_PASSWORD = 12,
    CLIENT_EXPORT_FILE_PATH = 13,
    CHECK_FOR_BETA_UPDATES = 14,
    ALLOW_RADIO_ENCRYPTION = 15,
    TEST_FREQUENCIES = 16,
    SHOW_TUNED_COUNT = 17,
    GLOBAL_LOBBY_FREQUENCIES = 18,
    SHOW_TRANSMITTER_NAME = 19,
    LOTATC_EXPORT_ENABLED = 20,
    LOTATC_EXPORT_PORT = 21,
    LOTATC_EXPORT_IP = 22,
    UPNP_ENABLED = 23,
    RETRANSMISSION_NODE_LIMIT = 24,
    STRICT_RADIO_ENCRYPTION = 25,
    TRANSMISSION_LOG_ENABLED = 26,
    TRANSMISSION_LOG_RETENTION = 27,
    RADIO_EFFECT_OVERRIDE = 28,
    SERVER_IP = 29,
    SERVER_PRESETS_ENABLED = 30,
    SERVER_PRESETS = 31,
    HTTP_SERVER_ENABLED = 32,
    HTTP_SERVER_PORT = 33,
    HTTP_SERVER_API_KEY = 34,
    SERVER_EAM_RADIO_PRESET_ENABLED = 35,
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
    public static string ToSection(this ServerSettingsKeys key)
    {
        return Section[key];
    }
    public static string ToDefaultValue(this ServerSettingsKeys key)
    {
        return DefaultValue[key];
    }
    
    public const string GeneralSettingSection = "General Settings";
    public const string ServerSettingsSection = "Server Settings";
    public const string ExternalSettingsSection = "External AWACS Mode Settings";
    
    public static readonly Dictionary<ServerSettingsKeys, string> Section = new(){
        { ServerSettingsKeys.CLIENT_EXPORT_ENABLED, GeneralSettingSection },
        { ServerSettingsKeys.COALITION_AUDIO_SECURITY , GeneralSettingSection },
        { ServerSettingsKeys.DISTANCE_ENABLED, GeneralSettingSection},
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE , GeneralSettingSection},
        { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES , GeneralSettingSection},
        { ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE , GeneralSettingSection},
        { ServerSettingsKeys.IRL_RADIO_TX , GeneralSettingSection},
        { ServerSettingsKeys.IRL_RADIO_STATIC , GeneralSettingSection},
        { ServerSettingsKeys.LOS_ENABLED , GeneralSettingSection },
        { ServerSettingsKeys.LOTATC_EXPORT_ENABLED , GeneralSettingSection},
        { ServerSettingsKeys.RADIO_EFFECT_OVERRIDE , GeneralSettingSection},
        { ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, GeneralSettingSection },
        { ServerSettingsKeys.RADIO_EXPANSION , GeneralSettingSection},
        { ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT , GeneralSettingSection},
        { ServerSettingsKeys.SERVER_PRESETS_ENABLED , GeneralSettingSection},
        { ServerSettingsKeys.SHOW_TRANSMITTER_NAME , GeneralSettingSection},
        { ServerSettingsKeys.SHOW_TUNED_COUNT , GeneralSettingSection},
        { ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED , GeneralSettingSection},
        { ServerSettingsKeys.STRICT_RADIO_ENCRYPTION , GeneralSettingSection},
        { ServerSettingsKeys.TEST_FREQUENCIES , GeneralSettingSection},
        { ServerSettingsKeys.TRANSMISSION_LOG_ENABLED , GeneralSettingSection},
        { ServerSettingsKeys.TRANSMISSION_LOG_RETENTION , GeneralSettingSection},

        { ServerSettingsKeys.CHECK_FOR_BETA_UPDATES , ServerSettingsSection},
        { ServerSettingsKeys.UPNP_ENABLED , ServerSettingsSection},
        { ServerSettingsKeys.SERVER_PORT , ServerSettingsSection},
        { ServerSettingsKeys.SERVER_IP , ServerSettingsSection},
        { ServerSettingsKeys.HTTP_SERVER_ENABLED , ServerSettingsSection},
        { ServerSettingsKeys.HTTP_SERVER_PORT , ServerSettingsSection},
        { ServerSettingsKeys.HTTP_SERVER_API_KEY , ServerSettingsSection},
        { ServerSettingsKeys.LOTATC_EXPORT_PORT , ServerSettingsSection},
        { ServerSettingsKeys.LOTATC_EXPORT_IP , ServerSettingsSection},
        { ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH , ServerSettingsSection },
        { ServerSettingsKeys.SERVER_PRESETS , ServerSettingsSection},

        { ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD , ExternalSettingsSection},
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD , ExternalSettingsSection },
    };
    
    public static readonly Dictionary<ServerSettingsKeys, string> DefaultValue = new()
    {
        { ServerSettingsKeys.CLIENT_EXPORT_ENABLED, "false" },
        { ServerSettingsKeys.COALITION_AUDIO_SECURITY, "false" },
        { ServerSettingsKeys.DISTANCE_ENABLED, "false" },
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE, "false" },
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, "" },
        { ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, "" },
        { ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, "false" },
        { ServerSettingsKeys.IRL_RADIO_STATIC, "false" },
        { ServerSettingsKeys.IRL_RADIO_TX, "false" },
        { ServerSettingsKeys.LOS_ENABLED, "false" },
        { ServerSettingsKeys.RADIO_EXPANSION, "false" },
        { ServerSettingsKeys.SERVER_PORT, "5002" },
        { ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, "false" },
        { ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, "clients-list.json" },
        { ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, "false" },
        { ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, "true" },
        { ServerSettingsKeys.TEST_FREQUENCIES, "247.2,120.3" },
        { ServerSettingsKeys.SHOW_TUNED_COUNT, "true" },
        { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, "248.22" },
        { ServerSettingsKeys.LOTATC_EXPORT_ENABLED, "false" },
        { ServerSettingsKeys.LOTATC_EXPORT_PORT, "10712" },
        { ServerSettingsKeys.LOTATC_EXPORT_IP, "127.0.0.1" },
        { ServerSettingsKeys.UPNP_ENABLED, "true" },
        { ServerSettingsKeys.SHOW_TRANSMITTER_NAME, "false" },
        { ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, "0" },
        { ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, "false" },
        { ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, "false" },
        { ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, "2" },
        { ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, "false" },
        { ServerSettingsKeys.SERVER_IP, "0.0.0.0" },
        { ServerSettingsKeys.SERVER_PRESETS_ENABLED, "false" },
        { ServerSettingsKeys.HTTP_SERVER_ENABLED, "false" },
        { ServerSettingsKeys.HTTP_SERVER_PORT, "8080" },
        { ServerSettingsKeys.HTTP_SERVER_API_KEY, ShortGuid.NewGuid() },
        { ServerSettingsKeys.SERVER_EAM_RADIO_PRESET_ENABLED, "false" },
    };
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