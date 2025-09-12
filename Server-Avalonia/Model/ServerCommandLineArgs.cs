using System;
using System.ComponentModel;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using CommandLine;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Model;

public class ServerCommandLineArgs
{
    private ServerSettingsStore ServerSettings { get => Ioc.Default.GetRequiredService<ServerSettingsStore>(); }
    
    private string _configFile;
    
    // This section is for stuff that only exists for this run of the program.
    // Stuff like "Headless" mode, etc. It its meant to be saved to file or
    // touch the Server Setting Store Singleton, use the "Saved Server Settings" region.
    #region Ephemeral Server Settings
    [Option("consoleLogs",
        HelpText = "Show basic console logs. Default is true",
        Default = false,
        Required = false)]
    public bool OptionConsoleLogs { get; set; }
    
    [Option("headless",
        HelpText = "Enable's or disables the Sever UI. Default is false",
        Default = false,
        Required = false)]
    public bool OptionHeadless { get; set; }
    
    [Option('c', "cfg", Required = false,
        HelpText =
            "Configuration file path. Must be the full path to the config file. i.e -cfg=C:\\some-path\\server.cfg")]
    public string OptionConfigFile
    {
        get => _configFile;
        set
        {
            //tidy up if the value is  fg=xxxxx as it strips -c of the -cfg with a single -
            _configFile = value;

            if (_configFile != null)
            {
                _configFile = _configFile.Trim();
                if (_configFile.StartsWith("fg=")) _configFile = _configFile.Replace("fg=", "");
            }
        }
    }
    
    #endregion
    
    
    #region General Settings
    [Option("clientExportEnabled",
        HelpText = "Exports the current clients every second to a .json file. Default is false.",
        Required = false)]
    public bool? OptionClientExportEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, value.ToString()); }

    [Option("coalitionSecurity",
        HelpText = "Stops radio transmissions between coalitions. Default is false.",
        Required = false)]
    public bool? OptionCoalitionAudioSecurityEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, value.ToString()); }
    [Option("distanceLimit",
        HelpText = "Enables distance limit behavior. Default is false.",
        Required = false)]
    public bool? OptionDistanceLimitEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.DISTANCE_ENABLED, value.ToString()); }
    [Option("enableEAM",
        HelpText =
            "Enables External Mode - allows clients to connect without DCS running once they input the correct password. Default is false",
        Required = false)]
    public bool? OptionExternalModeEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE, value.ToString()); }
    [Option("realRadioTx",
        HelpText = "Enables receiving radio interference from other transmissions. Default is false",
        Required = false)]
    public bool? OptionIrlRadioRxEffectsEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE, value.ToString()); }
    [Option("realRadioTx",
        HelpText =
            "Forces radios to be half duplex (can only send or receive - not both at the same time). Default is false",
        Required = false)]
    public bool? OptionIrlRadioTxEffectsEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX, value.ToString()); }
    [Option("lineOfSight",
        HelpText = "Enables Line Of Sight behavior. Default is false.",
        Required = false)]
    public bool? OptionLineOfSightEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.LOS_ENABLED, value.ToString()); }
    [Option("lotATCExport",
        HelpText = "Enables the export of Transponder data to LOTATC. Default is false.",
        Required = false)]
    public bool? OptionLotAtcExportEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_ENABLED, value.ToString()); }
    [Option("radioEffectOverride",
        HelpText = "Disables Radio Effects on the global frequency (for Music etc). Default is false",
        Required = false)]
    public bool? OptionRadioEffectOverrideOnGlobalEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.RADIO_EFFECT_OVERRIDE, value.ToString()); }
    [Option("allowRadioEncryption",
        HelpText = "Enables the ability for players to encrypt radio comms. Default is true",
        Required = false)]
    public bool? OptionRadioEncryptionAllowed { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION, value.ToString()); }
    [Option("radioExpansion",
        HelpText = "Enables Expansion (virtual) radios for aircraft which have few to improve comms. Default is false",
        Required = false)]
    public bool? OptionRadioExpansionAllowed { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.RADIO_EXPANSION, value.ToString()); }
    [Option("serverPresetChannelsEnabled",
        HelpText =
            "Enables Server Channel Presets to be used by clients - put the *.txt files in a folder called Presets alongside your server.cfg file",
        Required = false)]
    public bool? OptionServerPresetsEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS, value.ToString()); }
    [Option("showTransmitterName",
        HelpText = "Enables the ability for players to see who's transmitting. Default is false",
        Required = false)]
    public bool? OptionShowTransmitterNameEnable { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, value.ToString()); }
    [Option("showTunedCount",
        HelpText =
            "Enables the ability for players to see how many people are tuned to that frequency. Default is true",
        Required = false)]
    public bool? OptionShowTunedCountEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, value.ToString()); }
    [Option("spectatorAudioDisabled",
        HelpText = "Stops spectators from talking. Default is false.",
        Required = false)]
    public bool? OptionSpectatorAudioDisabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, value.ToString()); }
    [Option("strictRadioEncryption",
        HelpText =
            "If enabled and radio encryption is on, players can only hear encrypted radio transmissions. Default is false.",
        Required = false)]
    public bool? OptionStrictRadioEncryptionEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.STRICT_RADIO_ENCRYPTION, value.ToString()); }
    [Option("transmissionLogEnabled",
        HelpText = "Log all transmissions to a CSV. Default is false.",
        Required = false)]
    public bool? OptionTransmissionLogEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED, value.ToString()); }
    
    [Option("retransmitNodeLimit",
        HelpText =
            "Sets the maximum number of nodes that a transmission can pass through. Default 0 disables retransmission",
        Required = false)]
    public int? OptionRetransmissionNodeLimit { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.RETRANSMISSION_NODE_LIMIT, value.ToString()); }
    [Option("transmissionLogRetentionLimit",
        HelpText = "Sets the transmission log retention limit. Default 2.",
        Required = false)]
    public int? OptionTransmissionLogRetentionLimit { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION, value.ToString()); }
    
    [Option("globalLobbyFrequencies",
        HelpText =
            "Enables frequencies to that all players can always communicate on - even if coalition security is enabled as a lobby. Comma seperated.",
        Required = false)]
    public string? OptionGlobalLobbyFrequencies { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, value); }
    [Option("testFrequencies",
        HelpText = "Enables frequencies to playback transmissions to test radios. Comma seperated.",
        Required = false)]
    public string? OptionTestFrequencies { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.TEST_FREQUENCIES, value); }
    
    
    #endregion
    
    #region Saved Server Settings
    [Option("betaUpdates",
        HelpText = "Checks and notifies for BETA updates. Default is false",
        Required = false)]
    public bool? OptionCheckForBetaUpdatesEnabled { set => ServerSettings.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, value.ToString()); }


        #region Server Networking
        [Option("upnpEnabled",
            HelpText = "UPNP for NAT navigation. Default is true",
            Required = false)]
        public bool? OptionUpnpEnabled { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.UPNP_ENABLED, value.ToString()); }
        
        [Option('p', "port",
            HelpText = "Port - 5002 is the default",
            Required = false)]
        public int? OptionServerPort { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_PORT, value.ToString()); }
        
        [Option("serverBindIP",
            HelpText = "Server Bind IP. Default is 0.0.0.0. Dont change unless you know what you're doing!",
            Required = false)]
        public string? OptionServerBindIp { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_IP, value?.ToString()); }
        #endregion

        #region Server Accessories
        [Option("httpServerEnabled",
            HelpText = "Enables the HTTP Server. Default is false.",
            Required = false)]
        public bool? OptionHttpServerEnabled { set => ServerSettings.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_ENABLED, value?.ToString()); }
        [Option("httpServerPort",
            HelpText = "Sets the HTTP Server Port if Enabled. Default is 8080.",
            Required = false)]
        public int? OptionHttpServerPort { set => ServerSettings.SetServerSetting(ServerSettingsKeys.HTTP_SERVER_PORT, value.ToString()); }
        
        [Option("lotATCExportPort",
            HelpText = "Sets the port to set the Transponder data to on LotATC",
            Required = false)]
        public int? OptionLotAtcExportPort { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_PORT, value.ToString()); }

        [Option("lotATCExportIP",
            HelpText = "Sets the IP to set the Transponder data to on LotATC. Default 127.0.0.1",
            Required = false)]
        public string? OptionLotAtcExportIp { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.LOTATC_EXPORT_IP, value?.ToString()); }
        
        #endregion
    
    [Option("clientExportPath",
        HelpText = "Sets a custom client export path. Default is the current directory. It must be the full path!",
        Required = false)]
    public string? OptionClientExportFilePath { set => ServerSettings.SetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, value?.ToString()); }
    
    [Option("serverPresetChannelsPath",
        HelpText =
            $"Enables External Mode Presets to be used by clients - put the awacs-radios-custom.json file in a folder called Presets alongside your server.cfg file",
        Required = false)]
    public bool? OptionServerPresetsPath { set => ServerSettings.SetGeneralSetting(ServerSettingsKeys.SERVER_PRESETS, value.ToString()); }
    
    
    #endregion
    
    #region External AWACS Mode Settings
    [Option("eamBluePassword",
        HelpText = "Sets the password for the Blue coalition for EAM",
        Required = false)]
    public string OptionExternalModePassBlue { set => ServerSettings.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD, value.ToString()); }

    [Option("eamRedPassword",
        HelpText = "Sets the password for the Red coalition for EAM",
        Required = false)]
    public string OptionExternalModePassRed { set => ServerSettings.SetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD, value.ToString()); }
    
    #endregion

    public override string ToString()
    {
        string result = "";
        foreach(PropertyDescriptor descriptor in TypeDescriptor.GetProperties(this))
        {
            result = string.Concat(result, $@"{descriptor.Name}={descriptor.GetValue(this)},",  Environment.NewLine);
        }
        return result;
    }
}