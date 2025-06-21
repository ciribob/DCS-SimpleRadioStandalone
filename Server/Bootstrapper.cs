using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Server;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Properties;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI.ClientAdmin;
using Ciribob.DCS.SimpleRadio.Standalone.Server.UI.MainWindow;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Sentry;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server;

public class Bootstrapper : BootstrapperBase
{
    private readonly SimpleContainer _simpleContainer = new();
    private bool loggingReady;

    public Bootstrapper()
    {
        InitCfgPath();
        
        SentrySdk.Init("https://0935ffeb7f9c46e28a420775a7f598f4@o414743.ingest.sentry.io/5315043");

        Initialize();
        SetupLogging();

        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }
    
    private void InitCfgPath(){
        //check commandline
        var args = Environment.GetCommandLineArgs();

        foreach (var arg in args)
            if (arg.StartsWith("-cfg="))
                ServerSettingsStore.CFG_FILE_NAME = arg.Replace("-cfg=", "").Trim();
            else if (arg.StartsWith("--cfg="))
                ServerSettingsStore.CFG_FILE_NAME = arg.Replace("--cfg=", "").Trim();
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
            { "Icon", new BitmapImage(new Uri("pack://application:,,,/SRS-Server;component/server-10.ico")) },
            { "ResizeMode", ResizeMode.CanMinimize }
        };
        //create an instance of serverState to actually start the server
        _simpleContainer.GetInstance(typeof(ServerState), null);

        DisplayRootViewForAsync<MainViewModel>(settings);
        
        UpdaterChecker.Instance.CheckForUpdate(ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue,
            result =>
            {
                if (result.UpdateAvailable)
                {
                        //Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
                        var choice = MessageBox.Show($"{Properties.Resources.MsgBoxUpdate1} {result.Branch} {Properties.Resources.MsgBoxUpdate2} {result.Branch} {Properties.Resources.MsgBoxUpdate3}\n\n{Properties.Resources.MsgBoxUpdate4}",
                            Properties.Resources.MsgBoxUpdateTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                
                        if (choice == MessageBoxResult.Yes)
                        {
                            try
                            {
                                UpdaterChecker.Instance.LaunchUpdater(result.Beta);
                            }
                            catch (Exception)
                            {
                                MessageBox.Show($"{Properties.Resources.MsgBoxUpdateFailed}",
                                    Properties.Resources.MsgBoxUpdateFailedTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                
                                Process.Start( new ProcessStartInfo(result.Url)
                                    { UseShellExecute = true });
                            }
                
                        }
                        else if (choice == MessageBoxResult.No)
                        {
                            Process.Start( new ProcessStartInfo(result.Url)
                                { UseShellExecute = true });
                           
                        }
                }
            });
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
            var logger = LogManager.GetCurrentClassLogger();
            logger.Error(e.Exception, "Received unhandled exception, exiting");
        }

        base.OnUnhandledException(sender, e);
    }
}