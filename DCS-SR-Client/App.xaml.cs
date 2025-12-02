using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Sentry;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Logger Logger = LogManager.GetCurrentClassLogger();
    private NotifyIcon _notifyIcon;
    private bool loggingReady;
    private Mutex SingleInstanceMutex { get; set; }

    public App()
    {
        System.Windows.Forms.Application.EnableVisualStyles();

        // Common ones to use are -lang:en-us , -lang:zh , -lang:zh-cn , -lang:fr
        try
        {
            string lang = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith("-lang:")).Substring(6);
            if (!string.IsNullOrEmpty(lang))
            {
                Logger.Warn($"Command Line Set Language Code : {lang}");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);
            }
        } catch (CultureNotFoundException e){ Logger.Error(e.Message); }
        catch { /* ignored */ }


        SentrySdk.Init("https://1b22a96cbcc34ee4b9db85c7fa3fe4e3@o414743.ingest.sentry.io/5304752");
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

        var location = AppDomain.CurrentDomain.BaseDirectory;
        //var location = Assembly.GetExecutingAssembly().Location;

        var dllsToValidate = new[] { "opus.dll", "speexdsp.dll" };
        foreach (var dll in dllsToValidate)
        {
            if (!File.Exists(location + "\\" + dll))
            {
                TaskDialog.ShowDialog(new TaskDialogPage
                {
                    Caption = $"Installation Error!",
                    Heading = $"You are missing the {dll}",
                    Text = $"Reinstall using the Installer and don't move the client from the installation directory!",
                    Icon = TaskDialogIcon.Error,
                    Buttons = { TaskDialogButton.OK }
                });

                Environment.Exit(1);
            }
        }

        SetupLogging();

        ListArgs();

#if !DEBUG
            if (IsClientRunning())
            {
                //check environment flag

                var args = Environment.GetCommandLineArgs();
                var allowMultiple = false;

                foreach (var arg in args)
                {
                    if (arg.Contains("-allowMultiple"))
                    {
                        //restart flag to promote to admin
                        allowMultiple = true;
                    }
                }

                if (GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.AllowMultipleInstances) || allowMultiple)
                {
                    Logger.Warn("Another SRS instance is already running, allowing multiple instances due to config setting");
                }
                else
                {
                    Logger.Warn("Another SRS instance is already running, preventing second instance startup");

                    TaskDialog.ShowDialog(new TaskDialogPage
                    {
                        Caption = "Multiple SimpleRadio clients started!",
                        Heading = "Another instance of the SimpleRadio client is already running!",
                        Text = $"This one will now quit. Check your system tray for the SRS Icon",
                        Icon = TaskDialogIcon.Error,
                        Buttons = { TaskDialogButton.OK }
                    });

                    Environment.Exit(0);
                    return;
                }
            }
#endif

        RequireAdmin();

        InitNotificationIcon();
    }

    private void ListArgs()
    {
        Logger.Info("Arguments:");
        var args = Environment.GetCommandLineArgs();
        foreach (var s in args) Logger.Info(s);
    }

    private void RequireAdmin()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        var hasAdministrativeRight = principal.IsInRole(WindowsBuiltInRole.Administrator);
        
        Logger.Info($"User running as admin: {hasAdministrativeRight}");
        if (!GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin))
        {
            Logger.Info("Admin rights not required");
            return;
        }

        if (!hasAdministrativeRight &&
            GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin))
        {
            Logger.Info($"Attempting to elevate to admin");

            Task.Factory.StartNew(() =>
            {
                var location = AppDomain.CurrentDomain.BaseDirectory;

                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = "\"" + location + "\"",
                    FileName = "SR-ClientRadio.exe",
                    Verb = "runas",
                    Arguments = GetArgsString() + " -allowMultiple"
                };
                try
                {
                    //TODO fix process start
                    var p = Process.Start(startInfo);

                    //shutdown this process as another has started
                    Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (_notifyIcon != null)
                            _notifyIcon.Visible = false;

                        try
                        {
                            ClientStateSingleton.Instance.Close();
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        Environment.Exit(0);
                    }));
                }
                catch (Win32Exception)
                {
                    TaskDialog.ShowDialog(new TaskDialogPage
                    {
                        Caption = "UAC Error",
                        Heading = "SRS could not restart with elevated privilages.",
                        Text = $"Unless you have a very specific need you should disable the Require Admin option in the settings.",
                        Icon = TaskDialogIcon.Warning,
                        Buttons = { TaskDialogButton.OK }
                    });
                }
            });
        }
    }

    private string GetArgsString()
    {
        var builder = new StringBuilder();
        var args = Environment.GetCommandLineArgs();
        foreach (var s in args)
        {
            if (builder.Length > 0) builder.Append(" ");

            if (s.Contains("-cfg="))
            {
                var str = s.Replace("-cfg=", "-cfg=\"");

                builder.Append(str);
                builder.Append("\"");
            }
            else if (s.Contains("SR-ClientRadio.exe"))
            {
                ///ignore
            }
            else
            {
                builder.Append(s);
            }
        }

        return builder.ToString();
    }

    private bool IsClientRunning()
    {
        bool created;
        SingleInstanceMutex = new Mutex(true, "DCS-SimpleRadioStandalone", out created);
        return !created;
    }

    /*
     * Changes to the logging configuration in this method must be replicated in
     * this VS project's NLog.config file
     */
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
            FileName = "clientlog.txt",
            ArchiveFileName = "clientlog.old.txt",
            MaxArchiveFiles = 1,
            ArchiveAboveSize = 104857600,
            Layout =
                @"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=1}"
        };

        var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
        config.AddTarget("asyncFileTarget", wrapper);
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, wrapper));

        LogManager.Configuration = config;
        loggingReady = true;

        Logger = LogManager.GetCurrentClassLogger();
    }


    private void InitNotificationIcon()
    {
        if (_notifyIcon != null) return;
        var notifyIconContextMenuShow = new ToolStripMenuItem
        {
            Text = "Show"
        };
        notifyIconContextMenuShow.Click += NotifyIcon_Show;

        var notifyIconContextMenuQuit = new ToolStripMenuItem
        {
            Text = "Quit"
        };
        notifyIconContextMenuQuit.Click += NotifyIcon_Quit;

        var notifyIconContextMenu = new ContextMenuStrip();

        notifyIconContextMenu.Items.AddRange(new[] { notifyIconContextMenuShow, notifyIconContextMenuQuit });

        _notifyIcon = new NotifyIcon
        {
            Icon = Client.Properties.Resources.audio_headset,
            Visible = true
        };

        _notifyIcon.ContextMenuStrip = notifyIconContextMenu;
        _notifyIcon.DoubleClick += NotifyIcon_Show;
    }

    private void NotifyIcon_Show(object sender, EventArgs args)
    {
        MainWindow?.Show();
        MainWindow.WindowState = WindowState.Normal;
    }

    private void NotifyIcon_Quit(object sender, EventArgs args)
    {
        MainWindow?.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ClientStateSingleton.Instance.Close();
        
        if (_notifyIcon != null)
            _notifyIcon.Visible = false;
        base.OnExit(e);
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        if (loggingReady)
        {
            var logger = LogManager.GetCurrentClassLogger();
            logger.Error((Exception)e.ExceptionObject, "Received unhandled exception, {0}",
                e.IsTerminating ? "exiting" : "continuing");
        }
    }
}