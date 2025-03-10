using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI.MainWindow;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Sentry;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server
{
    public class Bootstrapper : BootstrapperBase
    {
        private readonly SimpleContainer _simpleContainer = new SimpleContainer();
        private bool loggingReady = false;

        public Bootstrapper()
        {
            SentrySdk.Init("https://0935ffeb7f9c46e28a420775a7f598f4@o414743.ingest.sentry.io/5315043");

            Initialize();
            SetupLogging();

            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            Analytics.Log("Server", "Startup", Guid.NewGuid().ToString());
        }

        private void SetupLogging()
        {
            // If there is a configuration file then this will already be set
            if (LogManager.Configuration != null)
            {
                loggingReady = true;
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
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, wrapper));

            // only add transmission logging at launch if its enabled, defer rule and target creation otherwise
            if (ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_ENABLED).BoolValue)
            {
                config = LoggingHelper.GenerateTransmissionLoggingConfig(config,
                            ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.TRANSMISSION_LOG_RETENTION).IntValue);
            }

            LogManager.Configuration = config;
            loggingReady = true;
        }

        protected override void Configure()
        {
            _simpleContainer.Singleton<IWindowManager, WindowManager>();
            _simpleContainer.Singleton<IEventAggregator, EventAggregator>();
            _simpleContainer.Singleton<ServerState>();

            _simpleContainer.Singleton<MainViewModel>();
            _simpleContainer.Singleton<ClientAdminViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            var instance = _simpleContainer.GetInstance(service, key);
            if (instance != null)
                return instance;

            throw new InvalidOperationException("Could not locate any instances.");
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return _simpleContainer.GetAllInstances(service);
        }


        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            IDictionary<string, object> settings = new Dictionary<string, object>
            {
                {"Icon", new BitmapImage(new Uri("pack://application:,,,/SR-Server;component/server-10.ico"))},
                {"ResizeMode", ResizeMode.CanMinimize}
            };
            //create an instance of serverState to actually start the server
            _simpleContainer.GetInstance(typeof(ServerState), null);

            DisplayRootViewForAsync<MainViewModel>(settings);

            UpdaterChecker.CheckForUpdate(Settings.ServerSettingsStore.Instance.GetServerSetting(Common.Setting.ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue);
        }

        protected override void BuildUp(object instance)
        {
            _simpleContainer.BuildUp(instance);
        }


        protected override void OnExit(object sender, EventArgs e)
        {
            var serverState = (ServerState)_simpleContainer.GetInstance(typeof(ServerState), null);
            serverState.StopServer();
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (loggingReady)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error(e.Exception, "Received unhandled exception, exiting");
            }

            base.OnUnhandledException(sender, e);
        }
    }
}