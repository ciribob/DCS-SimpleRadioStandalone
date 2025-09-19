using System.Net;
using System.Runtime;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.EventMessages;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using CommandLine;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Sentry;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

internal class Program : IHandle<SRSClientStatus>
{
    private readonly EventAggregator _eventAggregator = new();
    private ServerState _serverState;

    public Program()
    {
        SentrySdk.Init("https://0935ffeb7f9c46e28a420775a7f598f4@o414743.ingest.sentry.io/5315043");
        SetupLogging();
    }

    public bool ConsoleLogs { get; set; }

    public Task HandleAsync(SRSClientStatus message, CancellationToken cancellationToken)
    {
        if (ConsoleLogs)
        {
            if (message.Connected)
                Console.WriteLine($"SRS Client Connected: {message.ClientIP}");
            else
                Console.WriteLine($"SRS Client Disconnected: {message.ClientIP} - {message.SRSGuid}");
        }

        return Task.CompletedTask;
    }

    private static void Main(string[] args)
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(ProcessArgs);
    }

    private static void ProcessArgs(Options options)
    {
        if (options.ConfigFile != null && options.ConfigFile.Trim().Length > 0)
            ServerSettingsStore.CFG_FILE_NAME = options.ConfigFile.Trim();
        
        UpdaterChecker.Instance.CheckForUpdate(
            ServerSettingsStore.Instance.ServerSettings.IsCheckForBetaUpdatesEnabled,
            result =>
            {
                if (result.UpdateAvailable)
                    Console.WriteLine($"Update Available! Version {result.Version}-{result.Branch} @ {result.Url}");
            });
        Console.WriteLine($"Settings From Command Line: \n{options}");

        var p = new Program();
        new Thread(() => { p.StartServer(options); }).Start();

        var waitForProcessShutdownStart = new ManualResetEventSlim();
        var waitForMainExit = new ManualResetEventSlim();

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            // We got a SIGTERM, signal that graceful shutdown has started
            waitForProcessShutdownStart.Set();

            Console.WriteLine("Shutting down gracefully...");
            // Don't unwind until main exists
            waitForMainExit.Wait();
        };

        Console.WriteLine("Waiting for shutdown SIGTERM");
        // Wait for shutdown to start
        waitForProcessShutdownStart.Wait();

        // This is where the application performs graceful shutdown
        p.StopServer();

        Console.WriteLine("Shutdown complete");
        // Now we're done with main, tell the shutdown handler
        waitForMainExit.Set();
    }

    private void StopServer()
    {
        _serverState.StopServer();

        EventBus.Instance.Unsubcribe(this);
    }


    public void StartServer(Options options)
    {
        EventBus.Instance.SubscribeOnPublishedThread(this);

        ConsoleLogs = options.ConsoleLogs;

        if (options.Port != null && options.Port.HasValue)
            ServerSettingsStore.Instance.ServerSettings.ServerPort = options.Port.Value;
        if (options.CoalitionSecurity != null && options.CoalitionSecurity.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsCoalitionAudioSecurityEnabled = options.CoalitionSecurity.Value;
        if (options.SpectatorAudioDisabled != null && options.SpectatorAudioDisabled.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsSpectatorAudioDisabled = options.SpectatorAudioDisabled.Value;
        if (options.ClientExportEnabled != null && options.ClientExportEnabled.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsClientExportEnabled = options.ClientExportEnabled.Value;
        if (options.RealRadioTX != null && options.RealRadioTX.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsIrlRadioTxEffectsEnabled = options.RealRadioTX.Value;
        if (options.RealRadioRX != null && options.RealRadioRX.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsIrlRadioRxEffectsEnabled = options.RealRadioRX.Value;
        if (options.RadioExpansion != null && options.RadioExpansion.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsRadioExpansionAllowed = options.RadioExpansion.Value;
        if (options.EnableEAM != null && options.EnableEAM.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsExternalModeEnabled = options.EnableEAM.Value;
        if (options.EAMRedPassword != null && options.EAMRedPassword.Trim().Length > 0)
            ServerSettingsStore.Instance.ExternalModeSettings.ExternalModePassRed = options.EAMRedPassword;
        if (options.EAMBluePassword != null && options.EAMBluePassword.Trim().Length > 0)
            ServerSettingsStore.Instance.ExternalModeSettings.ExternalModePassBlue = options.EAMBluePassword;
        if (options.CheckBETAUpdates != null && options.CheckBETAUpdates.HasValue)
            ServerSettingsStore.Instance.ServerSettings.IsCheckForBetaUpdatesEnabled = options.CheckBETAUpdates.Value;
        if (options.AllowRadioEncryption != null && options.AllowRadioEncryption.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsRadioEncryptionAllowed = options.AllowRadioEncryption.Value;
        if (options.ShowTunedCount != null && options.ShowTunedCount.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsShowTunedCountEnabled = options.ShowTunedCount.Value;
        if (options.ShowTransmitterName != null && options.ShowTransmitterName.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsShowTransmitterNameEnabled = options.ShowTransmitterName.Value;
        if (options.LOTATCExport != null && options.LOTATCExport.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsLotAtcExportEnabled = options.LOTATCExport.Value;
        if (options.LotATCExportPort != null && options.LotATCExportPort.HasValue)
            ServerSettingsStore.Instance.ServerSettings.LotAtcExportPort = options.LotATCExportPort.Value;
        if (options.LotATCExportIP != null && options.LotATCExportIP.Trim().Length > 0)
            ServerSettingsStore.Instance.ServerSettings.LotAtcExportIp = IPAddress.Parse(options.LotATCExportIP);
        if (options.TestFrequencies != null && options.TestFrequencies.Trim().Length > 0)
            ServerSettingsStore.Instance.SynchronizedSettings.TestFrequencies = 
                new List<double>( (options.TestFrequencies).Split(',').Select(double.Parse).ToList() );
        if (options.RetransmissionNodeLimit != null && options.RetransmissionNodeLimit.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.RetransmissionNodeLimit = options.RetransmissionNodeLimit.Value;
        if (options.StrictRadioEncryption != null && options.StrictRadioEncryption.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsStrictRadioEncryptionEnabled = options.StrictRadioEncryption.Value;
        if (options.TransmissionLogEnabled != null && options.TransmissionLogEnabled.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsTransmissionLogEnabled = options.TransmissionLogEnabled.Value;
        if (options.RadioEffectOverride != null && options.RadioEffectOverride.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsRadioEffectOverrideOnGlobalEnabled = options.RadioEffectOverride.Value;
        if (options.ServerBindIP != null && options.ServerBindIP.Trim().Length > 0)
            ServerSettingsStore.Instance.ServerSettings.ServerBindIp = IPAddress.Parse(options.ServerBindIP);
        if (options.ClientExportPath != null && options.ClientExportPath.Trim().Length > 0)
            ServerSettingsStore.Instance.ServerSettings.ClientExportFilePath = options.ClientExportPath;
        if (options.ServerPresetChannelsEnabled != null && options.ServerPresetChannelsEnabled.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsServerPresetsEnabled = options.ServerPresetChannelsEnabled.Value;
        if (options.ServerEAMRadioPresetEnabled != null && options.ServerEAMRadioPresetEnabled.HasValue)
            ServerSettingsStore.Instance.SynchronizedSettings.IsServerPresetsEnabled = options.ServerEAMRadioPresetEnabled.Value;
        if (options.HttpServerEnabled != null && options.HttpServerEnabled.HasValue)
            ServerSettingsStore.Instance.ServerSettings.IsHttpServerEnabled = options.HttpServerEnabled.Value;
        if (options.HttpServerPort != null && options.HttpServerPort.HasValue)
            ServerSettingsStore.Instance.ServerSettings.HttpServerPort = options.HttpServerPort.Value;

        Console.WriteLine("Final Settings:");
        foreach (var setting in ServerSettingsStore.Instance.GetAllSettings()) Console.WriteLine(setting);

        _serverState = new ServerState(_eventAggregator);
    }

    private void SetupLogging()
    {
        // If there is a configuration file then this will already be set
        if (LogManager.Configuration != null) return;

        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget
        {
            FileName = "serverlog.txt",
            ArchiveFileName = "serverlog.old.txt",
            MaxArchiveFiles = 1,
            ArchiveAboveSize = 104857600,
            Layout =
                @"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=1}"
        };

        var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
        config.AddTarget("asyncFileTarget", wrapper);
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, wrapper));

        LogManager.Configuration = config;
    }
}

public class Options
{
    private string _configFile;

    [Option("consoleLogs",
        HelpText = "Show basic console logs. Default is true",
        Default = true,
        Required = false)]
    public bool ConsoleLogs { get; set; }

    [Option('p', "port",
        HelpText = "Port - 5002 is the default",
        Required = false)]
    public int? Port { get; set; }

    [Option("coalitionSecurity",
        HelpText = "Stops radio transmissions between coalitions. Default is false.",
        Required = false)]
    public bool? CoalitionSecurity { get; set; }

    [Option("spectatorAudioDisabled",
        HelpText = "Stops spectators from talking. Default is false.",
        Required = false)]
    public bool? SpectatorAudioDisabled { get; set; }

    [Option("clientExportEnabled",
        HelpText = "Exports the current clients every second to a .json file. Default is false.",
        Required = false)]
    public bool? ClientExportEnabled { get; set; }

    [Option("realRadioTx",
        HelpText =
            "Forces radios to be half duplex (can only send or receive - not both at the same time. Default is false",
        Required = false)]
    public bool? RealRadioTX { get; set; }

    [Option("realRadioTx",
        HelpText = "Enables receiving radio interference from other transmissions. Default is false",
        Required = false)]
    public bool? RealRadioRX { get; set; }

    [Option("radioExpansion",
        HelpText = "Enables Expansion (virtual) radios for aircraft which have few to improve comms. Default is false",
        Required = false)]
    public bool? RadioExpansion { get; set; }

    [Option("enableEAM",
        HelpText =
            "Enables External AWACS Mode - allows clients to connect without DCS running once they input the correct password. Default is false",
        Required = false)]
    public bool? EnableEAM { get; set; }

    [Option("eamBluePassword",
        HelpText = "Sets the password for the Blue coalition for EAM",
        Required = false)]
    public string EAMBluePassword { get; set; }

    [Option("eamRedPassword",
        HelpText = "Sets the password for the Red coalition for EAM",
        Required = false)]
    public string EAMRedPassword { get; set; }

    [Option("clientExportPath",
        HelpText = "Sets a custom client export path. Default is the current directory. It must be the full path!",
        Required = false)]
    public string ClientExportPath { get; set; }

    [Option("betaUpdates",
        HelpText = "Checks and notifies for BETA updates. Default is false",
        Required = false)]
    public bool? CheckBETAUpdates { get; set; }

    [Option("allowRadioEncryption",
        HelpText = "Enables the ability for players to encrypt radio comms. Default is true",
        Required = false)]
    public bool? AllowRadioEncryption { get; set; }

    [Option("testFrequencies",
        HelpText = "Enables frequencies to playback transmissions to test radios",
        Required = false)]
    public string TestFrequencies { get; set; }

    [Option("showTunedCount",
        HelpText =
            "Enables the ability for players to see how many people are tuned to that frequency. Default is true",
        Required = false)]
    public bool? ShowTunedCount { get; set; }

    [Option("globalLobbyFrequencies",
        HelpText =
            "Enables frequencies to that all players can always communicate on - even if coalition security is enabled as a lobby.",
        Required = false)]
    public string GlobalFrequencies { get; set; }

    [Option("showTransmitterName",
        HelpText = "Enables the ability for players to see who's transmitting. Default is false",
        Required = false)]
    public bool? ShowTransmitterName { get; set; }

    [Option("lotATCExport",
        HelpText = "Enables the export of Transponder data to LOTATC. Default is false.",
        Required = false)]
    public bool? LOTATCExport { get; set; }

    [Option("lotATCExportPort",
        HelpText = "Sets the port to set the Transponder data to on LotATC",
        Required = false)]
    public int? LotATCExportPort { get; set; }

    [Option("lotATCExportIP",
        HelpText = "Sets the IP to set the Transponder data to on LotATC",
        Required = false)]
    public string LotATCExportIP { get; set; }

    [Option("retransmitNodeLimit",
        HelpText =
            "Sets the maximum number of nodes that a transmission can pass through. Default 0 disables retransmission",
        Required = false)]
    public int? RetransmissionNodeLimit { get; set; }

    [Option("strictRadioEncryption",
        HelpText =
            "If enabled and radio encryption is on, players can only hear encrypted radio transmissions. Default is false.",
        Required = false)]
    public bool? StrictRadioEncryption { get; set; }

    [Option("transmissionLogEnabled",
        HelpText = "Log all transmissions to a CSV. Default is false.",
        Required = false)]
    public bool? TransmissionLogEnabled { get; set; }

    [Option("httpServerEnabled",
        HelpText = "Enables the HTTP Server. Default is false.",
        Required = false)]
    public bool? HttpServerEnabled { get; set; }

    [Option("httpServerPort",
        HelpText = "Sets the HTTP Server Port if Enabled. Default is 8080.",
        Required = false)]
    public int? HttpServerPort { get; set; }

    [Option("radioEffectOverride",
        HelpText = "Disables Radio Effects on the global frequency (for Music etc). Default is false",
        Required = false)]
    public bool? RadioEffectOverride { get; set; }

    [Option("serverBindIP",
        HelpText = "Server Bind IP. Default is 0.0.0.0. Dont change unless you know what you're doing!",
        Required = false)]
    public string ServerBindIP { get; set; }

    [Option('c', "cfg", Required = false,
        HelpText =
            "Configuration file path. Must be the full path to the config file. i.e -cfg=C:\\some-path\\server.cfg")]
    public string ConfigFile
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

    [Option("serverPresetChannelsEnabled",
        HelpText =
            "Enables Server Channel Presets to be used by clients - put the *.txt files in a folder called Presets alongside your server.cfg file",
        Required = false)]
    public bool? ServerPresetChannelsEnabled { get; set; }
    
    [Option("serverEAMRadioPresetEnabled",
        HelpText =
            $"Enables Server EAM Presets to be used by clients - put the awacs-radios-custom.json file in a folder called Presets alongside your server.cfg file",
        Required = false)]
    public bool? ServerEAMRadioPresetEnabled { get; set; }

    public override string ToString()
    {
        return
            $"{nameof(ConfigFile)}: {ConfigFile}, \n" +
            $"{nameof(ConsoleLogs)}: {ConsoleLogs}, \n" +
            $"{nameof(Port)}: {Port}, \n" +
            $"{nameof(CoalitionSecurity)}: {CoalitionSecurity}, \n" +
            $"{nameof(SpectatorAudioDisabled)}: {SpectatorAudioDisabled}, \n" +
            $"{nameof(ClientExportEnabled)}: {ClientExportEnabled}, \n" +
            $"{nameof(RealRadioTX)}: {RealRadioTX}, \n" +
            $"{nameof(RealRadioRX)}: {RealRadioRX}, \n" +
            $"{nameof(RadioExpansion)}: {RadioExpansion}, \n" +
            $"{nameof(EnableEAM)}: {EAMBluePassword}, \n" +
            $"{nameof(EAMRedPassword)}: {ClientExportPath}, \n" +
            $"{nameof(CheckBETAUpdates)}: {CheckBETAUpdates}, \n" +
            $"{nameof(EAMBluePassword)}: {EAMBluePassword}, \n" +
            $"{nameof(AllowRadioEncryption)}: {AllowRadioEncryption}, \n" +
            $"{nameof(TestFrequencies)}: {TestFrequencies}, \n" +
            $"{nameof(ShowTunedCount)}: {ShowTunedCount}, \n" +
            $"{nameof(ShowTransmitterName)}: {ShowTransmitterName}, \n" +
            $"{nameof(LOTATCExport)}: {LOTATCExport}, \n" +
            $"{nameof(LotATCExportPort)}: {LotATCExportPort}, \n" +
            $"{nameof(LotATCExportIP)}: {LotATCExportIP}, \n" +
            $"{nameof(RetransmissionNodeLimit)}: {RetransmissionNodeLimit}, \n" +
            $"{nameof(StrictRadioEncryption)}: {StrictRadioEncryption}, \n" +
            $"{nameof(TransmissionLogEnabled)}: {TransmissionLogEnabled}, \n" +
            $"{nameof(RadioEffectOverride)}: {RadioEffectOverride}, \n" +
            $"{nameof(ServerPresetChannelsEnabled)}: {ServerPresetChannelsEnabled}, \n" +
            $"{nameof(ServerEAMRadioPresetEnabled)}: {ServerEAMRadioPresetEnabled}, \n" +
            $"{nameof(HttpServerEnabled)}: {HttpServerEnabled}, \n" +
            $"{nameof(HttpServerPort)}: {HttpServerPort}, \n" +
            $"{nameof(ServerBindIP)}: {ServerBindIP}";
    }
}