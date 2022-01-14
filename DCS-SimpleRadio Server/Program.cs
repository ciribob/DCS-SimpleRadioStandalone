using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System.Runtime;
using LogManager = NLog.LogManager;

void SetupLogging()
{
    // If there is a configuration file then this will already be set
    if (LogManager.Configuration != null)
    {
        return;
    }

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
#if DEBUG
    var consoleWrapper = new ColoredConsoleTarget();
    config.AddTarget("console", consoleWrapper);
    config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleWrapper));
#endif
    config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, wrapper));

    // only add transmission logging at launch if its enabled, defer rule and target creation otherwise
    if (ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue)
    {
        config = LoggingHelper.GenerateTransmissionLoggingConfig(config,
                    ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue);
    }

    LogManager.Configuration = config;
}

Console.WriteLine("SRS Server is running");
SetupLogging();
Logger logger = LogManager.GetCurrentClassLogger();
GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
ServerState state = new();
ServerSettingsStore store = ServerSettingsStore.Instance;
logger.Info("Server Started on port " + store.GetServerPort());
