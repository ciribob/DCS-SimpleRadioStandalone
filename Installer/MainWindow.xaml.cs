using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using File = System.IO.File;

namespace Installer
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string REG_PATH = "HKEY_CURRENT_USER\\SOFTWARE\\DCS-SR-Standalone";

        private const string EXPORT_SRS_LUA =
            "pcall(function() local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Mods\\Services\\DCS-SRS\\Scripts\\DCS-SimpleRadioStandalone.lua]]); end,nil)";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _currentDirectory;
        private ProgressBarDialog _progressBarDialog = null;

        public MainWindow()
        {
            SetupLogging();
            InitializeComponent();

            var assembly = Assembly.GetEntryAssembly();
            var version = assembly?.GetName().Version?.ToString();

            Title = Properties.Resources.Title;
            intro.Content = Properties.Resources.intro + " v" + version;
            step2.Content = Properties.Resources.step2;
            srPathButton.Content = Properties.Resources.srPathButton;
            step3.Content = Properties.Resources.step3;
            dcsPathButton.Content = Properties.Resources.dcsPathButton;
            InstallButton.Content = Properties.Resources.InstallButton;
            RemoveButton.Content = Properties.Resources.RemoveButton;
            step4.Content = Properties.Resources.step4;
            InstallScriptsCheckbox.Content = Properties.Resources.InstallScriptsCheckbox;
            CreateStartMenuShortcut.Content = Properties.Resources.CreateStartMenuShortcut;
            ServerNote.Text = Properties.Resources.ServerNote;

            //allows click and drag anywhere on the window
            containerPanel.MouseLeftButtonDown += GridPanel_MouseLeftButtonDown;

            var srPathStr = ReadPath("SRPathStandalone");
            if (srPathStr != "")
            {
                srPath.Text = srPathStr;
            }

            var scriptsPath = ReadPath("ScriptsPath");
            if (scriptsPath != "")
            {
                dcsScriptsPath.Text = scriptsPath;
            }
            else
            {
                dcsScriptsPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) +
                                      "\\Saved Games\\";
            }

            //To get the location the assembly normally resides on disk or the install directory
            var currentPath = GetWorkingDirectory();

            if (currentPath.StartsWith("file:\\"))
            {
                currentPath = currentPath.Replace("file:\\", "");
            }

            _currentDirectory = currentPath;

            Logger.Info("Listing Files / Directories for: " + _currentDirectory);
            ListFiles(_currentDirectory);
            Logger.Info("Finished Listing Files / Directories");

            if (!CheckExtracted())
            {
                MessageBox.Show(
                    Properties.Resources.MsgBoxExtractedText,
                    Properties.Resources.MsgBoxExtracted,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Logger.Warn("Files missing from Installation Directory");

                Environment.Exit(0);

                return;
            }

            new Action(async () =>
            {
                await Task.Delay(1).ConfigureAwait(false);

                if (((App)Application.Current).Arguments.Length > 0)
                {
                    if (IsAutoUpdate() && !IsSilentServer())
                    {
                        Application.Current.Dispatcher?.Invoke(() =>
                            {
                                Logger.Info("Silent Installer Running");
                                var result = MessageBox.Show(
                                    Properties.Resources.MsgBoxChangeText,
                                    Properties.Resources.MsgBoxChange,
                                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                                if (result == MessageBoxResult.Yes)
                                {
                                }
                                else
                                {
                                    InstallScriptsCheckbox.IsChecked = true;
                                    InstallReleaseButton(null, null);
                                }
                            }
                        ); //end-invoke
                    }
                    else if (IsAutoUpdate() && IsSilentServer())
                    {
                        Application.Current.Dispatcher?.Invoke(() =>
                            {
                                var path = ServerPath();
                                Logger.Info("Silent Server Installer Running - " + path);

                                srPath.Text = path;
                                InstallScriptsCheckbox.IsChecked = false;
                                InstallReleaseButton(null, null);
                            }
                        ); //end-invoke
                    }
                }
            }).Invoke();
        }

        private bool IsAutoUpdate()
        {
            foreach (var commandLineArg in Environment.GetCommandLineArgs())
            {
                if (commandLineArg.Trim().Equals("-autoupdate"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSilentServer()
        {
            foreach (var commandLineArg in Environment.GetCommandLineArgs())
            {
                if (commandLineArg.Trim().Equals("-server"))
                {
                    return true;
                }
            }

            return false;
        }

        private string ServerPath()
        {
            foreach (var commandLineArg in Environment.GetCommandLineArgs())
            {
                if (commandLineArg.Trim().StartsWith("-path="))
                {
                    var line = commandLineArg.Trim();
                    line = line.Replace("-path=", "");

                    return line;
                }
            }

            return "";
        }

        private bool ShouldRestart()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.Trim().Equals("-restart"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckExtracted()
        {
            return File.Exists(_currentDirectory + "\\Client\\opus.dll")
                   && File.Exists(_currentDirectory + "\\Client\\speexdsp.dll")
                   && File.Exists(_currentDirectory + "\\Client\\awacs-radios.json")
                   && File.Exists(_currentDirectory + "\\Client\\SR-ClientRadio.exe")
                   && File.Exists(_currentDirectory + "\\Scripts\\DCS-SRS\\Scripts\\DCS-SimpleRadioStandalone.lua")
                   && File.Exists(_currentDirectory + "\\Server\\SRS-Server.exe");
        }


        private void SetupLogging()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logFilePath = Path.Combine(baseDirectory, "installer-log.txt");
            string oldLogFilePath = Path.Combine(baseDirectory, "install-log.old.txt");

            FileInfo logFileInfo = new FileInfo(logFilePath);
            // Cleanup logfile if > 100MB, keep one old file copy
            if (logFileInfo.Exists && logFileInfo.Length >= 104857600)
            {
                if (File.Exists(oldLogFilePath))
                {
                    try
                    {
                        File.Delete(oldLogFilePath);
                    }
                    catch (Exception)
                    {
                    }
                }

                try
                {
                    File.Move(logFilePath, oldLogFilePath);
                }
                catch (Exception)
                {
                }
            }

            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget();

            fileTarget.FileName = "${basedir}/installer-log.txt";
            fileTarget.Layout =
                @"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=2}";

            var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
            config.AddTarget("file", wrapper);

#if DEBUG
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));
#else
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
#endif

            LogManager.Configuration = config;
        }

        private void ShowDCSWarning()
        {
            MessageBox.Show(
                Properties.Resources.MsgBoxDCSText,
                Properties.Resources.MsgBoxDCS,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void InstallReleaseButton(object sender, RoutedEventArgs e)
        {
            var dcScriptsPath = dcsScriptsPath.Text;
            if ((bool)!InstallScriptsCheckbox.IsChecked)
            {
                dcScriptsPath = null;
            }
            else
            {
                var paths = FindValidDCSFolders(dcScriptsPath);

                if (paths.Count == 0)
                {
                    MessageBox.Show(
                        Properties.Resources.MsgBoxFolderText,
                        Properties.Resources.MsgBoxFolder,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (IsDCSRunning())
                {
                    ShowDCSWarning();
                    return;
                }
            }

            InstallButton.IsEnabled = false;
            RemoveButton.IsEnabled = false;

            InstallButton.Content = Properties.Resources.InstallButton;

            _progressBarDialog = new ProgressBarDialog();
            _progressBarDialog.Owner = this;
            _progressBarDialog.Show();

            var srsPath = srPath.Text;

            var shortcut = CreateStartMenuShortcut.IsChecked ?? true;

            new Action(async () =>
            {
                int result = await Task.Run<int>(() => InstallRelease(srsPath, dcScriptsPath, shortcut));
                if (result == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        {
                            InstallButton.IsEnabled = true;
                            RemoveButton.IsEnabled = true;
                            InstallButton.Content = "Install";
                        }
                    ); //end-invoke
                    _progressBarDialog.UpdateProgress(true, "Error");
                }
                else if (result == 1)
                {
                    _progressBarDialog.UpdateProgress(true, "Installed SRS Successfully!");

                    Logger.Info($"Installed SRS Successfully!");

                    //open to installation location
                    // Process.Start("explorer.exe", srPath.Text);
                    Environment.Exit(0);
                }
                else
                {
                    _progressBarDialog.UpdateProgress(true, "Error with Installation");

                    MessageBox.Show(
                        Properties.Resources.MsgBoxInstallErrorText,
                        Properties.Resources.MsgBoxInstallError,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    //TODO fix process start
                    Process.Start(new ProcessStartInfo("https://discord.gg/baw7g3t")
                        { UseShellExecute = true });
                    Process.Start("explorer.exe", GetWorkingDirectory());
                    Environment.Exit(0);
                }
            }).Invoke();
        }

        private int InstallRelease(string srPath, string dcsScriptsPath, bool shortcut)
        {
            try
            {
                QuitSimpleRadio();

                var paths = new List<string>();
                if (dcsScriptsPath != null)
                {
                    paths = FindValidDCSFolders(dcsScriptsPath);

                    if (paths.Count == 0)
                    {
                        MessageBox.Show(
                            Properties.Resources.MsgBoxFolderText,
                            Properties.Resources.MsgBoxFolder,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return 0;
                    }

                    if (IsDCSRunning())
                    {
                        MessageBox.Show(
                            Properties.Resources.MsgBoxDCSText,
                            Properties.Resources.MsgBoxDCS,
                            MessageBoxButton.OK, MessageBoxImage.Error);

                        Logger.Warn("DCS is Running - Installer stopped");

                        return 0;
                    }

                    Logger.Info($"Installing - Paths: \nProgram:{srPath} \nDCS:{dcsScriptsPath} ");

                    ClearVersionPostModsServicesDCS(srPath, dcsScriptsPath);

                    foreach (var path in paths)
                    {
                        InstallScripts(path);
                    }
                }
                else
                {
                    Logger.Info($"Installing - Paths: \nProgram:{srPath} DCS: NO PATH - NO SCRIPTS");
                }

                //install program
                InstallProgram(srPath);

                WritePath(srPath, "SRPathStandalone");

                if (dcsScriptsPath != null)
                    WritePath(dcsScriptsPath, "ScriptsPath");

                if (shortcut)
                {
                    InstallShortcuts(srPath);
                }

                InstallVCRedist();

                if (dcsScriptsPath != null)
                {
                    string message = Properties.Resources.MsgBoxInstallSuccessText;

                    foreach (var path in paths)
                    {
                        message += ("\n" + path);
                    }

                    MessageBox.Show(message, Properties.Resources.MsgBoxInstallTitle,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    if (IsSilentServer())
                    {
                        if (ShouldRestart())
                        {
                            StartServer(srPath);
                            return 1;
                        }
                    }
                    else
                    {
                        string message = Properties.Resources.MsgBoxInstallSuccessText2;

                        MessageBox.Show(message, Properties.Resources.MsgBoxInstallTitle,
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }


                return 1;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error Running Installer");


                return -1;
            }
        }

        private void StartServer(string path)
        {
            Logger.Info($"Starting SRS Server - Paths: \nProgram:{path} ");
            ProcessStartInfo procInfo = new ProcessStartInfo
            {
                WorkingDirectory = path,
                FileName = (path + "\\Server\\" + "srs-server.exe"),
                UseShellExecute = false
            };
            Process.Start(procInfo);
        }

        private string GetWorkingDirectory()
        {
            return new FileInfo(AppContext.BaseDirectory).Directory.ToString();
        }

        private void InstallVCRedist()
        {
            _progressBarDialog.UpdateProgress(false, $"Installing VC Redist x64");
            Process.Start(GetWorkingDirectory() + "\\VC_redist.x64.exe",
                "/install /norestart /quiet /log \"vc_redist_2017_x64.log\"");
            _progressBarDialog.UpdateProgress(false, $"Finished installing VC Redist x64");
        }

        static void ListFiles(string sDir)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        Logger.Info(f);
                    }

                    ListFiles(d);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error listing files");
            }
        }

        private void CleanPreviousInstall(string programPath)
        {
            Logger.Info($"Removed SRS program files at {programPath}");
            _progressBarDialog.UpdateProgress(false, $"Removing SRS at {programPath}");
            
            if (Directory.Exists(programPath))
            {
                DeleteFileIfExists(programPath + "\\Server\\serverlog.txt");
                DeleteFileIfExists(programPath + "\\Server\\SRS-Server.exe");

                DeleteFileIfExists(programPath + "\\ServerCommandLine-Windows\\SRS-Server-Commandline.exe");
                DeleteFileIfExists(programPath + "\\ServerCommandLine-Windows\\serverlog.txt");

                DeleteFileIfExists(programPath + "\\ServerCommandLine-Linux\\SRS-Server-Commandline");

                DeleteDirectory(programPath + "\\ExternalAudio");

                DeleteDirectory(programPath + "\\Scripts");

                DeleteDirectory(programPath + "\\AudioEffects");
                DeleteDirectory(programPath + "\\zh-CN");

                DeleteDirectory(programPath + "\\Client\\AudioEffects\\Ambient");
                DeleteDirectory(programPath + "\\Client\\RadioModels");
                DeleteDirectory(programPath + "\\Client\\runtimes");
                DeleteFileIfExists(programPath + "\\Client\\awacs-radios.json");
                DeleteFileIfExists(programPath + "\\Client\\clientlog.txt");
                DeleteFileIfExists(programPath + "\\Client\\libmp3lame.dll");
                DeleteFileIfExists(programPath + "\\Client\\opus.dll");
                DeleteFileIfExists(programPath + "\\Client\\sni.dll");
                DeleteFileIfExists(programPath + "\\Client\\speexdsp.dll");
                DeleteFileIfExists(programPath + "\\Client\\SR-ClientRadio.exe");
                DeleteFileIfExists(programPath + "\\Client\\WebRtcVad.dll");

                //Old structure
                DeleteFileIfExists(programPath + "\\SR-ClientRadio.exe");
                DeleteFileIfExists(programPath + "\\DCS-SR-ExternalAudio.exe");
                DeleteFileIfExists(programPath + "\\grpc_csharp_ext.x64.dll");
                DeleteFileIfExists(programPath + "\\WebRtcVad.dll");
                DeleteFileIfExists(programPath + "\\libmp3lame.32.dll");
                DeleteFileIfExists(programPath + "\\libmp3lame.64.dll");
                DeleteFileIfExists(programPath + "\\opus.dll");
                DeleteFileIfExists(programPath + "\\speexdsp.dll");
                DeleteFileIfExists(programPath + "\\awacs-radios.json");
                DeleteFileIfExists(programPath + "\\SRS-AutoUpdater.exe");
                DeleteFileIfExists(programPath + "\\SR-Server.exe");
                DeleteFileIfExists(programPath + "\\serverlog.txt");
                DeleteFileIfExists(programPath + "\\clientlog.txt");
            }
        }

        private void ClearVersionPostModsServicesDCS(string programPath, string dcsPath)
        {
            Logger.Info($"Removed SRS Version Post Mods Services at {programPath} and {dcsPath}");

            var paths = FindValidDCSFolders(dcsPath);

            foreach (var path in paths)
            {
                _progressBarDialog.UpdateProgress(false, $"Removing SRS at {path}");
                RemoveScriptsPostModsServicesDCS(path);
            }
            
            Logger.Info($"Finished clearing scripts Post Mods ");
        }

        private void RemoveScriptsPostModsServicesDCS(string path)
        {
            Logger.Info($"Removing SRS Scripts at {path}");
            //SCRIPTS folder
            if (File.Exists(path + "\\Scripts\\Export.lua"))
            {
                var contents = File.ReadAllText(path + "\\Scripts\\Export.lua");

                if (contents.Contains("SimpleRadioStandalone.lua"))
                {
                    var lines = contents.Split('\n');

                    StringBuilder sb = new StringBuilder();

                    foreach (var line in lines)
                    {
                        if (!line.Contains("SimpleRadioStandalone.lua") && line.Trim().Length > 0)
                        {
                            sb.Append(line);
                            sb.Append("\n");
                        }
                        else
                        {
                            Logger.Info($"Removed SRS Scripts from Export.lua");
                        }
                    }

                    File.WriteAllText(path + "\\Scripts\\Export.lua", sb.ToString());
                }
            }

            Logger.Info($"Removed Hooks file");
            //Hooks Folder
            DeleteFileIfExists(path + "\\Scripts\\Hooks\\DCS-SRS-Hook.lua");

            //MODs folder
            if (Directory.Exists(path + "\\Mods\\Services\\DCS-SRS"))
            {
                Logger.Info($"Removed Mods/Services/DCS-SRS folder");
                Directory.Delete(path + "\\Mods\\Services\\DCS-SRS", true);
            }

            Logger.Info($"Finished Removing Mods/Services & Scripts for SRS");
        }

        private static string ReadPath(string key)
        {
            var srPath = (string)Registry.GetValue(REG_PATH,
                key,
                "");

            return srPath ?? "";
        }

        private static void WritePath(string path, string key)
        {
            Registry.SetValue(REG_PATH,
                key,
                path);
        }


        private static void DeleteRegKeys()
        {
            try
            {
                Registry.SetValue(REG_PATH,
                    "SRPathStandalone",
                    "");
                Registry.SetValue(REG_PATH,
                    "ScriptsPath",
                    "");
            }
            catch (Exception)
            {
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE", true))
                {
                    key.DeleteSubKeyTree("DCS-SimpleRadioStandalone", false);
                    key.DeleteSubKeyTree("DCS-SR-Standalone", false);
                }
            }
            catch (Exception)
            {
            }
        }

        private void QuitSimpleRadio()
        {
            Logger.Info($"Closing SRS Client & Server");
#if !DEBUG
            foreach (var clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-server") ||
                    clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-client"))
                {
                    Logger.Info($"Found & Terminating {clsProcess.ProcessName}");
                    clsProcess.Kill();
                    clsProcess.WaitForExit(5000);
                    clsProcess.Dispose();
                }
            }

            Logger.Info($"Closed SRS Client & Server");
#endif
        }

        private bool IsDCSRunning()
        {
            foreach (var clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.ToLower().Trim().Equals("dcs"))
                {
                    return true;
                    // bool suspended = true;
                    // foreach (var thread in clsProcess.Threads)
                    // {
                    //     var t = (System.Diagnostics.ProcessThread)thread;
                    //
                    //     if (t.ThreadState == ThreadState.Wait && t.WaitReason == ThreadWaitReason.Suspended)
                    //     {
                    //         Logger.Info($"DCS thread is suspended");
                    //     }
                    //     else
                    //     {
                    //         Logger.Info($"DCS thread is not suspended");
                    //         suspended = false;
                    //     }
                    // }
                    //
                    // return !suspended;
                }
            }

            return false;
        }

        private void GridPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }


        private void Set_Install_Path(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            var result = dlg.ShowDialog();
            if (result.ToString() == "OK")
            {
                // Open document
                var filename = dlg.SelectedPath;

                if (!filename.EndsWith("\\"))
                {
                    filename = filename + "\\";
                }

                filename = filename + "DCS-SimpleRadio-Standalone\\";

                srPath.Text = filename;
            }
        }

        private void Set_Scripts_Path(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            var result = dlg.ShowDialog();
            if (result.ToString() == "OK")
            {
                // Open document
                var filename = dlg.SelectedPath;

                if (!filename.EndsWith("\\"))
                {
                    filename = filename + "\\";
                }

                dcsScriptsPath.Text = filename;
            }
        }


        private static List<string> FindValidDCSFolders(string path)
        {
            Logger.Info($"Finding DCS Saved Games Path");
            var paths = new List<string>();

            if (path == null || path.Length == 0)
            {
                return paths;
            }

            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                if (directory.ToUpper().Contains("DCS.") || directory.ToUpper().EndsWith("DCS") 
                                                         || directory.ToUpper().Contains("MCS.") 
                                                         || directory.ToUpper().EndsWith("MCS"))
                {
                    //check for config/network.vault and options.lua
                    var network = directory + "\\config\\network.vault";
                    var config = directory + "\\config\\options.lua";
                    if (File.Exists(network) || File.Exists(config))
                    {
                        var split = directory.Split(Path.DirectorySeparatorChar);
                        if (!split.Last().ToUpper().Contains("SERVER") && !split.Last().ToUpper().Contains("DEDICATED"))
                        {
                            Logger.Info($"Found DCS Saved Games Path {directory}");
                            paths.Add(directory);
                        }
                        else
                        {
                            Logger.Info($"Found DCS Saved Games Path {directory} - Ignoring as its a Server path");
                        }
                    }
                }
            }

            Logger.Info($"Finished Finding DCS Saved Games Path");

            return paths;
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                }
            }
        }

        private void InstallProgram(string path)
        {
            try
            {
                CleanPreviousInstall(path);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error Cleaning Previous Install at {path}");
            }
            
            
            Logger.Info($"Installing SRS Program to {path}");
            _progressBarDialog.UpdateProgress(false, $"Installing SRS at {path}");
            //sleep! WTF directory is lagging behind state here...
            Task.Delay(TimeSpan.FromMilliseconds(200)).Wait();

            _progressBarDialog.UpdateProgress(false, $"Creating Directories at {path}");

            Logger.Info($"Creating Directories");
            CreateDirectory(path);

            //sleep! WTF directory is lagging behind state here...
            Task.Delay(TimeSpan.FromMilliseconds(200)).Wait();
            _progressBarDialog.UpdateProgress(false, $"Copying Program Files at {path}");

            Logger.Info($"Copying binaries");

            try
            {
                File.Copy(_currentDirectory + "\\Installer.exe", path + "\\Installer.exe", true);
            }
            catch (Exception)
            {
            }

            File.Copy(_currentDirectory + "\\SRS-AutoUpdater.exe", path + "\\SRS-AutoUpdater.exe", true);

            File.Copy(_currentDirectory + "\\Examples.txt", path + "\\Examples.txt", true);
            File.Copy(_currentDirectory + "\\Readme.txt", path + "\\Readme.txt", true);


            Logger.Info($"Copying directories");
            DirectoryCopy(_currentDirectory + "\\Server", path + "\\Server");
            DirectoryCopy(_currentDirectory + "\\ServerCommandLine-Windows", path + "\\ServerCommandLine-Windows");
            DirectoryCopy(_currentDirectory + "\\ServerCommandLine-Linux", path + "\\ServerCommandLine-Linux");
            DirectoryCopy(_currentDirectory + "\\Client", path + "\\Client");
            DirectoryCopy(_currentDirectory + "\\ExternalAudio", path + "\\ExternalAudio");
            DirectoryCopy(_currentDirectory + "\\Scripts", path + "\\Scripts");


            //Move old server.cfg
            try
            {
                if (File.Exists(path + "\\server.cfg"))
                {
                    File.Move(path + "\\server.cfg", path + "\\Server\\server.cfg");
                }
            }
            catch (Exception)
            {
                // ignored
            }


            //Move any existing .cfg files
            MoveClientCfgs(path);
            //Move custom radios
            MoveCustomRadios(path);
            MoveCustomRadiosJson(path);
            MoveFavourites(path);
            MoveRecordings(path);

            Logger.Info($"Finished installing SRS Program to {path}");
        }

        private void MoveClientCfgs(string path)
        {
            var files = Directory.GetFiles(path, "*.cfg");

            foreach (var file in files)
            {
                if (!file.ToLowerInvariant().Contains("server.cfg"))
                {
                    try
                    {
                        if (!File.Exists(path + "\\Client\\" + Path.GetFileName(file)))
                            File.Move(file, path + "\\Client\\" + Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error moving Client Configs {file}");
                    }
                }
            }
        }

        private void MoveCustomRadios(string path)
        {
            var files = Directory.GetFiles(path, "*.txt");

            foreach (var file in files)
            {
                if (!file.ToLowerInvariant().Contains("examples.txt") &&
                    !file.ToLowerInvariant().Contains("readme.txt"))
                {
                    try
                    {
                        if (!File.Exists(path + "\\Client\\" + Path.GetFileName(file)))
                            File.Move(file, path + "\\Client\\" + Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error moving custom radio file {file}");
                    }
                }
            }
        }

        private void MoveCustomRadiosJson(string path)
        {
            var files = Directory.GetFiles(path, "*.json");

            foreach (var file in files)
            {
                try
                {
                    if (!File.Exists(path + "\\Client\\" + Path.GetFileName(file)))
                        File.Move(file, path + "\\Client\\" + Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error moving custom radio json files {file}");
                }
            }
        }

        private void MoveFavourites(string path)
        {
            var files = Directory.GetFiles(path, "*.csv");

            foreach (var file in files)
            {
                try
                {
                    if (!File.Exists(path + "\\Client\\" + Path.GetFileName(file)))
                        File.Move(file, path + "\\Client\\" + Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error moving favourites file {file}");
                }
            }
        }
        
        private void MoveRecordings(string path)
        {
            if(Directory.Exists(path+"\\Recordings") && !Directory.Exists(path+"\\Client\\Recordings"))
            {
                try
                {
                    Directory.Move(path+"\\Recordings", path+"\\Client\\Recordings");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error moving Recordings");
                }
            }
        }

        private void InstallShortcuts(string path)
        {
            Logger.Info($"Adding SRS Shortcut");

            var shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                "DCS-SRS Client.lnk");
            
            if(File.Exists(shortcutPath))
                File.Delete(shortcutPath);

            Logger.Info(
                $"Adding SRS Shortcut {path + "Client\\SR-ClientRadio.exe"} - {shortcutPath} - Working Directory{
                    path + "\\Client"}");

            ShortcutHelper.CreateShortcut(shortcutPath, path + "\\Client\\SR-ClientRadio.exe", path + "\\Client", "",
                "",
                ShortcutHelper.ShortcutWindowStyles.WshNormalFocus, "DCS-SimpleRadio Standalone Client");
        }

        private void InstallScripts(string path)
        {
            Logger.Info($"Installing Scripts to {path}");
            _progressBarDialog.UpdateProgress(false, $"Creating Script folders @ {path}");
            //Scripts Path
            CreateDirectory(path + "\\Scripts");
            CreateDirectory(path + "\\Scripts\\Hooks");

            //Make Tech Path
            CreateDirectory(path + "\\Mods");
            CreateDirectory(path + "\\Mods\\Services");
            CreateDirectory(path + "\\Mods\\Services\\DCS-SRS");

            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();

            _progressBarDialog.UpdateProgress(false, $"Updating / Creating Export.lua @ {path}");
            Logger.Info($"Handling Export.lua");
            //does it contain an export.lua?
            if (File.Exists(path + "\\Scripts\\Export.lua"))
            {
                var contents = File.ReadAllText(path + "\\Scripts\\Export.lua");

                contents.Split('\n');

                if (contents.Contains("SimpleRadioStandalone.lua"))
                {
                    Logger.Info($"Updating existing Export.lua with existing SRS install");
                    var lines = contents.Split('\n');

                    StringBuilder sb = new StringBuilder();

                    foreach (var line in lines)
                    {
                        if (line.Contains("SimpleRadioStandalone.lua"))
                        {
                            sb.Append("\n");
                            sb.Append(EXPORT_SRS_LUA);
                            sb.Append("\n");
                        }
                        else if (line.Trim().Length > 0)
                        {
                            sb.Append(line);
                            sb.Append("\n");
                        }
                    }

                    File.WriteAllText(path + "\\Scripts\\Export.lua", sb.ToString());
                }
                else
                {
                    Logger.Info($"Appending to existing Export.lua");
                    var writer = File.AppendText(path + "\\Scripts\\Export.lua");

                    writer.WriteLine("\n" + EXPORT_SRS_LUA + "\n");
                    writer.Close();
                }
            }
            else
            {
                Logger.Info($"Creating new Export.lua");
                var writer = File.CreateText(path + "\\Scripts\\Export.lua");

                writer.WriteLine("\n" + EXPORT_SRS_LUA + "\n");
                writer.Close();
            }


            //Now sort out Scripts//Hooks folder contents
            Logger.Info($"Creating / installing Hooks & Mods / Services");
            _progressBarDialog.UpdateProgress(false, $"Creating / installing Hooks & Mods/Services @ {path}");
            try
            {
                File.Copy(_currentDirectory + "\\Scripts\\Hooks\\DCS-SRS-hook.lua",
                    path + "\\Scripts\\Hooks\\DCS-SRS-hook.lua",
                    true);

                DirectoryCopy(_currentDirectory + "\\Scripts\\DCS-SRS", path + "\\Mods\\Services\\DCS-SRS");
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show(
                    Properties.Resources.MsgBoxExtractedText2,
                    Properties.Resources.MsgBoxExtracted2, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }

            Logger.Info($"Scripts installed to {path}");

            _progressBarDialog.UpdateProgress(false, $"Installed Hooks & Mods/Services @ {path}");
        }

        public static void DeleteDirectory(string target_dir)
        {
            if (Directory.Exists(target_dir))
            {
                Directory.Delete(target_dir, true);
            }
        }

        private void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        private Task<bool> UninstallSR(string srPath, string dcsScriptsPath)
        {
            try
            {
                QuitSimpleRadio();
                Application.Current.Dispatcher.Invoke(() =>
                    {
                        InstallButton.IsEnabled = false;
                        RemoveButton.IsEnabled = false;

                        RemoveButton.Content = "Removing...";
                    }
                ); //end-invoke

                _progressBarDialog.UpdateProgress(false, $"Removing SRS");
                Logger.Info($"Removing - Paths: \nProgram:{srPath} \nDCS:{dcsScriptsPath} ");
                ClearVersionPostModsServicesDCS(srPath, dcsScriptsPath);

                DeleteRegKeys();

                RemoveShortcuts();

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error Running Uninstaller");
            }

            return Task.FromResult(false);
        }

        private void RemoveShortcuts()
        {
            Logger.Info($"Removed SRS Shortcut");
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                "DCS-SRS Client.lnk");

            DeleteFileIfExists(shortcutPath);
        }


        private void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                var dir = Directory.CreateDirectory(path);

                dir.Refresh();
                //sleep! WTF directory is lagging behind state here...
                Task.Delay(TimeSpan.FromMilliseconds(200)).Wait();
                try
                {
                    var dSecurity = dir.GetAccessControl();
                    if (WindowsIdentity.GetCurrent().Owner != null)
                    {
                        dSecurity.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Owner,
                            FileSystemRights.Modify,
                            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                            PropagationFlags.None, AccessControlType.Allow));
                    }

                    if (WindowsIdentity.GetCurrent().User != null)
                    {
                        dSecurity.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,
                            FileSystemRights.Modify,
                            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                            PropagationFlags.None, AccessControlType.Allow));
                    }

                    dir.SetAccessControl(dSecurity);
                    dir.Refresh();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Unable to set permissions on path ${path}", ex);
                }
            }

            //sometimes it says directory created and its not!
            do
            {
                Task.Delay(TimeSpan.FromMilliseconds(50)).Wait();
            } while (!Directory.Exists(path));

            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
        }


        private async void Remove_Plugin(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(dcsScriptsPath.Text))
            {
                dcsScriptsPath.Text = "";
                Logger.Info($"SRS Scripts path not valid - ignoring uninstall of scripts: {dcsScriptsPath.Text}");
            }
            else
            {
                if (IsDCSRunning())
                {
                    ShowDCSWarning();
                    return;
                }
            }

            _progressBarDialog = new ProgressBarDialog();
            _progressBarDialog.Owner = this;
            _progressBarDialog.Show();
            _progressBarDialog.UpdateProgress(false, Properties.Resources.MsgBoxUninstalling);

            var result = await UninstallSR(srPath.Text, dcsScriptsPath.Text);
            if (result)
            {
                _progressBarDialog.UpdateProgress(true, Properties.Resources.MsgBoxRemovedText2);
                Logger.Info($"Removed SRS Successfully!");

                MessageBox.Show(
                    Properties.Resources.MsgBoxRemovedText,
                    Properties.Resources.MsgBoxInstallTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _progressBarDialog.UpdateProgress(true, Properties.Resources.MsgBoxUninstallError);
                MessageBox.Show(
                    Properties.Resources.MsgBoxUninstallErrorText,
                    Properties.Resources.MsgBoxInstallError,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Process.Start(new ProcessStartInfo("https://discord.gg/baw7g3t")
                    { UseShellExecute = true });

                Process.Start(new ProcessStartInfo("explorer.exe", GetWorkingDirectory())
                    { UseShellExecute = true });
            }

            Environment.Exit(0);
        }

        private void InstallScriptsCheckbox_OnChecked(object sender, RoutedEventArgs e)
        {
            dcsScriptsPath.IsEnabled = true;
        }

        private void InstallScriptsCheckbox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            dcsScriptsPath.IsEnabled = false;
        }
    }
}