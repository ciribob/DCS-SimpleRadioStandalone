using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network.Models
{
    class TransmissionLoggingQueue
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private ConcurrentDictionary<SRClient, TransmissionLog> _currentTransmissionLog { get; } = new ConcurrentDictionary<SRClient, TransmissionLog>();
        private bool _stop;
        private bool _log;
        private FileTarget _fileTarget;
        private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;

        public TransmissionLoggingQueue()
        {
            _log = _serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue;
            _stop = false;

            WrapperTargetBase b = (WrapperTargetBase)LogManager.Configuration.FindTargetByName("asyncTransmissionFileTarget");
            _fileTarget = b != null ? (FileTarget)b.WrappedTarget : null;
            //_fileTarget = (FileTarget)b.WrappedTarget;
        }

        public void LogTransmission(SRClient client)
        {
            if (!_stop)
            {
                try
                {
                    _currentTransmissionLog.AddOrUpdate(client,
                        new TransmissionLog(client.LastTransmissionReceived, client.TransmittingFrequency),
                        (k, v) => UpdateTransmission(client, v));
                }
                catch
                {
                }

            }
        }

        private TransmissionLog UpdateTransmission(SRClient client, TransmissionLog log)
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
                if (_log != !_serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue)
                {
                    _log = !_serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue;
                    string newSetting = _log ? "TRANSMISSION LOGGING ENABLED" : "TRANSMISSION LOGGING DISABLED";

                    if (_serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue
                        && _fileTarget == null) // require initialization of transmission logging filetarget and rule
                    {
                        LoggingConfiguration config = LogManager.Configuration;

                        config = LoggingHelper.GenerateTransmissionLoggingConfig(config,
                            _serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue);

                        LogManager.Configuration = config;

                        WrapperTargetBase b = (WrapperTargetBase)LogManager.Configuration.FindTargetByName("asyncTransmissionFileTarget");
                        _fileTarget = (FileTarget)b.WrappedTarget;
                    }

                    Logger.Info($"EVENT, {newSetting}");
                }

                if (_serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue &&
                    _fileTarget.MaxArchiveFiles != _serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue)
                {
                    _fileTarget.MaxArchiveFiles = _serverSettings.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue;
                    LogManager.ReconfigExistingLoggers();
                }

                if (_log && !_currentTransmissionLog.IsEmpty)
                {
                    foreach (KeyValuePair<SRClient, TransmissionLog> LoggedTransmission in _currentTransmissionLog)
                    {
                        if (LoggedTransmission.Value.IsComplete())
                        {
                            if (_currentTransmissionLog.TryRemove(LoggedTransmission.Key, out TransmissionLog completedLog))
                            {
                                Logger.Info($"TRANSMISSION, {LoggedTransmission.Key.ClientGuid}, {LoggedTransmission.Key.Name}, " +
                                    $"{LoggedTransmission.Key.Coalition}, {LoggedTransmission.Value.TransmissionFrequency}. " +
                                    $"{completedLog.TransmissionStart}, {completedLog.TransmissionEnd}, {LoggedTransmission.Key.VoipPort}");
                            }
                        }
                    }
                }
            }
        }
    }
}
