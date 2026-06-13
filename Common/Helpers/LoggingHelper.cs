using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

public static class LoggingHelper
{
    public static LoggingConfiguration GenerateTransmissionLoggingConfig(LoggingConfiguration config, int archiveFiles)
    {
        var transmissionFileTarget = new FileTarget
        {
            FileName = @"${date:format=yyyy-MM-dd}-transmissionlog.csv",
            ArchiveOldFileOnStartup = true,
            ArchiveFileName = @"${basedir}/TransmissionLogArchive/transmissionlog.old.csv",
            MaxArchiveFiles = archiveFiles,
            ArchiveEvery = FileArchivePeriod.Day,
            Layout = @"${longdate}, ${message}"
        };


        var transmissionWrapper =
            new AsyncTargetWrapper(transmissionFileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);

        config.AddTarget("asyncTransmissionFileTarget", transmissionWrapper);


        var transmissionRule = new LoggingRule(
            "Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server.TransmissionLogging.TransmissionLoggingQueue",
            LogLevel.Info,
            transmissionWrapper
        );
        transmissionRule.Final = true;

        config.LoggingRules.Add(transmissionRule);

        return config;
    }
}