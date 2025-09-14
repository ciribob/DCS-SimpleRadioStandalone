using System.Collections.Concurrent;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server.TransmissionLogging;

internal class TransmissionLoggingQueue
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;
    private FileTarget _fileTarget;
    private bool _log;
    private bool _stop;

    public TransmissionLoggingQueue()
    {
        _stop = false;
    }

    private ConcurrentDictionary<SRClientBase, TransmissionLog> _currentTransmissionLog { get; } = new();

    public void LogTransmission(SRClientBase client)
    {
        if (!_stop)
            try
            {
                if (_log)
                    _currentTransmissionLog.AddOrUpdate(client,
                        new TransmissionLog(client.LastTransmissionReceived, client.TransmittingFrequency),
                        (k, v) => UpdateTransmission(client, v));
            }
            catch
            {
            }
    }

    private TransmissionLog UpdateTransmission(SRClientBase client, TransmissionLog log)
    {
        log.TransmissionEnd = client.LastTransmissionReceived;
        return log;
    }

    public void Start()
    {
        new Thread(LogCompleteTransmissions).Start();
    }

    public void Stop()
    {
        _stop = true;
    }

    private void LogCompleteTransmissions()
    {
        while (!_stop)
        {
            Thread.Sleep(500);
            if (_log != _serverSettings.SynchronizedSettings.IsTransmissionLogEnabled)
            {
                _log = _serverSettings.SynchronizedSettings.IsTransmissionLogEnabled;
                var newSetting = _log ? "TRANSMISSION LOGGING ENABLED" : "TRANSMISSION LOGGING DISABLED";

                if (_log && _fileTarget == null) // require initialization of transmission logging filetarget and rule
                {
                    var config = LogManager.Configuration;

                    config = LoggingHelper.GenerateTransmissionLoggingConfig(config,
                        _serverSettings.SynchronizedSettings.TransmissionLogRetentionLimit);

                    LogManager.Configuration = config;

                    var b = (WrapperTargetBase)LogManager.Configuration.FindTargetByName("asyncTransmissionFileTarget");
                    _fileTarget = (FileTarget)b.WrappedTarget;
                }

                Logger.Info($"EVENT, {newSetting}");
            }

            if (_serverSettings.SynchronizedSettings.IsTransmissionLogEnabled &&
                _fileTarget.MaxArchiveFiles != _serverSettings.SynchronizedSettings.TransmissionLogRetentionLimit)
            {
                _fileTarget.MaxArchiveFiles = _serverSettings.SynchronizedSettings.TransmissionLogRetentionLimit;
                LogManager.ReconfigExistingLoggers();
            }

            if (_log && !_currentTransmissionLog.IsEmpty)
                foreach (var LoggedTransmission in _currentTransmissionLog)
                    if (LoggedTransmission.Value.IsComplete())
                        if (_currentTransmissionLog.TryRemove(LoggedTransmission.Key, out var completedLog))
                            Logger.Info(
                                $"TRANSMISSION, {LoggedTransmission.Key.ClientGuid}, {LoggedTransmission.Key.Name}, " +
                                $"{LoggedTransmission.Key.Coalition}, {LoggedTransmission.Value.TransmissionFrequency}. " +
                                $"{completedLog.TransmissionStart}, {completedLog.TransmissionEnd}, {LoggedTransmission.Key.VoipPort}");
        }
    }
}