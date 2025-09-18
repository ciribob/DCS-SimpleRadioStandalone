using System;
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
            ArchiveFileName = @"${basedir}/TransmissionLogArchive/{#}-transmissionlog.old.csv",
            ArchiveNumbering = "Date", // #TODO switch to ArchiveSuffixFormat.
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

    /// <summary>
    /// Executes an action, logs any exception with context, and invokes an error handler.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="context">Contextual message for logging.</param>
    /// <param name="onError">Error handler delegate (optional).</param>
    public static void TryOrLog(Logger logger, Action action, string context, Action<Exception>? onError = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed while {context}.");
            onError?.Invoke(ex);
            throw;
        }
    }
}