using System;
using System.Collections.Generic;
using System.Reflection;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;

public enum ServerSettingsKeys
{
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(int))]
    [SrValue(5002, 0, 65535)]
    SERVER_PORT = 0,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    COALITION_AUDIO_SECURITY = 1,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    SPECTATORS_AUDIO_DISABLED = 2,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    CLIENT_EXPORT_ENABLED = 3,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    LOS_ENABLED = 4,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    DISTANCE_ENABLED = 5,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    IRL_RADIO_TX = 6,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    IRL_RADIO_RX_INTERFERENCE = 7,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    IRL_RADIO_STATIC = 8, // Not used
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    RADIO_EXPANSION = 9,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    EXTERNAL_AWACS_MODE = 10,
    
    [SrSetting(ServerSettingsKeysExtensions.ExternalSection, typeof(string))]
    [SrValue("", 0, 256)]
    EXTERNAL_AWACS_MODE_BLUE_PASSWORD = 11,
    
    [SrSetting(ServerSettingsKeysExtensions.ExternalSection, typeof(string))]
    [SrValue("", 0, 256)]
    EXTERNAL_AWACS_MODE_RED_PASSWORD = 12,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(string))]
    [SrValue("", 0, 256)]
    CLIENT_EXPORT_FILE_PATH = 13,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(bool))]
    [SrValue(false)]
    CHECK_FOR_BETA_UPDATES = 14,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(true)]
    ALLOW_RADIO_ENCRYPTION = 15,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(string))]
    [SrValue("247.2,120.3", 0, 256)]
    TEST_FREQUENCIES = 16,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(true)]
    SHOW_TUNED_COUNT = 17,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(string))]
    [SrValue("248.22", 0, 256)]
    GLOBAL_LOBBY_FREQUENCIES = 18,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    SHOW_TRANSMITTER_NAME = 19,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    LOTATC_EXPORT_ENABLED = 20,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(int))]
    [SrValue(10712, 0, 65535)]
    LOTATC_EXPORT_PORT = 21,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(string))]
    [SrValue("127.0.0.1", 0, 256)]
    LOTATC_EXPORT_IP = 22,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(bool))]
    [SrValue(true)]
    UPNP_ENABLED = 23,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(int))]
    [SrValue(0, 0, 7)]
    RETRANSMISSION_NODE_LIMIT = 24,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    STRICT_RADIO_ENCRYPTION = 25,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    TRANSMISSION_LOG_ENABLED = 26,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(int))]
    [SrValue(2, 0, 7)]
    TRANSMISSION_LOG_RETENTION = 27,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    RADIO_EFFECT_OVERRIDE = 28,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(string))]
    [SrValue("0.0.0.0", 0, 256)]
    SERVER_IP = 29,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    SERVER_PRESETS_ENABLED = 30,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(string))]
    [SrValue("", 0, 65535)]
    SERVER_PRESETS = 31,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(bool))]
    [SrValue(false)]
    HTTP_SERVER_ENABLED = 32,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(int))]
    [SrValue(10712, 0, 65535)]
    HTTP_SERVER_PORT = 33,
    
    [SrSetting(ServerSettingsKeysExtensions.ServerSection, typeof(string))]
    [SrValue("0.0.0.0", 0, 256)]
    HTTP_SERVER_API_KEY = 34,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(bool))]
    [SrValue(false)]
    SERVER_EAM_RADIO_PRESET_ENABLED = 35,
    
    [SrSetting(ServerSettingsKeysExtensions.GeneralSection, typeof(string))]
    [SrValue("", 0, 65535)]
    SERVER_EAM_RADIO_PRESET = 36,
}

public class SrSettingAttribute : Attribute
{
    public string Section {get;}
    public Type Type {get;}
    
    public SrSettingAttribute(string section, Type type)
    {
        Section = section;
        Type = type;
    }
}

public class SrValueAttribute : Attribute
{
    public dynamic DefaultValue {get;}
    public int? Minimum {get;}
    public int? Maximum {get;}
    
    public SrValueAttribute(bool defaultValue)
    {
        DefaultValue = defaultValue;
    }
    public SrValueAttribute(int defaultValue, int minimum, int maximum)
    {
        DefaultValue = defaultValue;
        Minimum = minimum;
        Maximum = maximum;
    }
    public SrValueAttribute(string defaultValue, int minimum, int maximum)
    {
        DefaultValue = defaultValue;
        Minimum = minimum;
        Maximum = maximum;
    }
}

public static class ServerSettingsKeysExtensions
{
    // todo Add a dedictated "Syncronized" section
    public const string GeneralSection = "General Settings";
    public const string ServerSection = "Server Settings";
    public const string ExternalSection = "External AWACS Mode Settings";
    
    public static string GetSettingSection(this ServerSettingsKeys key)
    {
        FieldInfo field = key.GetType().GetField(key.ToString());
        
        if (field != null)
        {
            if (Attribute.GetCustomAttribute(field, typeof(SrSettingAttribute)) is SrSettingAttribute attribute) 
                return attribute.Section;
        }

        return null;
    }
    
    public static Type GetSettingType(this ServerSettingsKeys key)
    {
        FieldInfo field = key.GetType().GetField(key.ToString());
        
        if (field != null)
        {
            if (Attribute.GetCustomAttribute(field, typeof(SrSettingAttribute)) is SrSettingAttribute attribute) 
                return attribute.Type;
        }

        return null;
    }
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