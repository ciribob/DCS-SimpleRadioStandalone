using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using IWshRuntimeLibrary;
using Microsoft.Win32;
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
        private const string CLIENT_REG_PATH = "HKEY_CURRENT_USER\\SOFTWARE";
        private const string DCS_PATH = "HKEY_CURRENT_USER\\SOFTWARE\\EAGLE DYNAMICS\\DCS WORLD";
        private readonly string currentDirectory;

        //   private readonly string currentPath;

        public MainWindow()
        {
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = fvi.FileVersion;

            intro.Content = intro.Content + " v" + version;

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
            var currentPath = Assembly.GetExecutingAssembly().CodeBase;

            //once you have the path you get the directory with:
            currentDirectory = Path.GetDirectoryName(currentPath);

            if (currentDirectory.StartsWith("file:\\"))
            {
                currentDirectory = currentDirectory.Replace("file:\\", "");
            }
        }


        private static string ReadPath(string key)
        {
            var srPath = (string) Registry.GetValue(REG_PATH,
                key,
                "");

            return srPath ?? "";
        }

        private static string GetPathDCS()
        {
            var Path = (string) Registry.GetValue(DCS_PATH,
                "PATH",
                "");

            return Path ?? "";
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
            catch (Exception ex)
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
            catch (Exception ex)
            {
            }
        }


        //
        private static bool Is_SimpleRadio_running()
        {
#if DEBUG
            return false;
#endif
            foreach (var clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-"))
                {
                    return true;
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

        private void Install_Release(object sender, RoutedEventArgs e)
        {
            if (Is_SimpleRadio_running())
            {
                MessageBox.Show("Please close SimpleRadio Overlay before updating!", "SR Standalone Installer",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);

                return;
            }


            // string savedGamesPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Saved Games\\";
            var paths = FindValidDCSFolders(dcsScriptsPath.Text);

            if (paths.Count == 0)
            {
                MessageBox.Show(
                    "Unable to find DCS Folder in Saved Games!\n\nPlease check the path to the \"Saved Games\" folder\n\nMake sure you are selecting the \"Saved Games\" folder - NOT the DCS folder inside \"Saved Games\" and NOT the DCS installation directory",
                    "SR Standalone Installer",
                    MessageBoxButton.OK, MessageBoxImage.Error);


                return;
            }

            InstallButton.IsEnabled = false;
            RemoveButton.IsEnabled = false;

            InstallButton.Content = "Installing...";

            foreach (var path in paths)
            {
                InstallScripts(path + "\\Scripts");
            }

            //install program
            InstallProgram(srPath.Text);

            WritePath(srPath.Text, "SRPathStandalone");
            WritePath(dcsScriptsPath.Text, "ScriptsPath");

            if (CreateStartMenuShortcut.IsChecked ?? true)
            {
                InstallShortcuts(srPath.Text);
            }

            string message = "Installation / Update Completed Succesfully!\nInstalled DCS Scripts to: \n";

            foreach (var path in paths)
            {
                message += ("\n" + path);
            }

            MessageBox.Show(message, "SR Standalone Installer",
                MessageBoxButton.OK, MessageBoxImage.Information);

            //open to installation location
            Process.Start("explorer.exe", srPath.Text);

            Environment.Exit(0);
        }

        private static List<string> FindValidDCSFolders(string path)
        {
            var paths = new List<string>();

            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                var dirs = directory.Split('\\');

                var end = dirs[dirs.Length - 1];

                if (end.ToUpper().StartsWith("DCS.") || end.ToUpper().Equals("DCS"))
                {
                    paths.Add(directory);
                }
            }

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
                catch (Exception ex)
                {
                }
            }
        }

        private void InstallProgram(string path)
        {

            if (Directory.Exists(path) && File.Exists(path + "\\SR-ClientRadio.exe"))
            {
                DeleteFileIfExists(path + "\\SR-ClientRadio.exe");
                DeleteFileIfExists(path + "\\opus.dll");
                DeleteFileIfExists(path + "\\speexdsp.dll");
                DeleteFileIfExists(path + "\\SR-ClientRadio.exe");
                DeleteFileIfExists(path + "\\SR-Server.exe");
                DeleteFileIfExists(path + "\\DCS-SimpleRadioStandalone.lua");
                DeleteFileIfExists(path + "\\DCS-SRSGameGUI.lua");
                DeleteFileIfExists(path + "\\DCS-SRS-AutoConnectGameGUI.lua");
                DeleteFileIfExists(path + "\\DCS-SRS-OverlayGameGUI.lua");
                DeleteFileIfExists(path + "\\DCS-SRS-Overlay.dlg");
                DeleteFileIfExists(path + "\\DCS-SRS-hook.lua");
            }
            //sleep! WTF directory is lagging behind state here...
            Thread.Sleep(200);

            CreateDirectory(path);
            CreateDirectory(path + "\\AudioEffects");

            //sleep! WTF directory is lagging behind state here...
            Thread.Sleep(200);

            File.Copy(currentDirectory + "\\opus.dll", path + "\\opus.dll", true);
            File.Copy(currentDirectory + "\\speexdsp.dll", path + "\\speexdsp.dll", true);
            if (!File.Exists(path + "\\awacs-radios.json"))
            {
                File.Copy(currentDirectory + "\\awacs-radios.json", path + "\\awacs-radios.json", true);
            }
            File.Copy(currentDirectory + "\\SR-ClientRadio.exe", path + "\\SR-ClientRadio.exe", true);
            File.Copy(currentDirectory + "\\SR-Server.exe", path + "\\SR-Server.exe", true);

            File.Copy(currentDirectory + "\\beep-connected.wav", path + "\\AudioEffects\\beep-connected.wav", true);
            File.Copy(currentDirectory + "\\beep-disconnected.wav", path + "\\AudioEffects\\beep-disconnected.wav", true);
            File.Copy(currentDirectory + "\\KY-58-TX-1600.wav", path + "\\AudioEffects\\KY-58-TX-1600.wav", true);
            File.Copy(currentDirectory + "\\KY-58-RX-1600.wav", path + "\\AudioEffects\\KY-58-RX-1600.wav", true);
            File.Copy(currentDirectory + "\\Radio-TX-1600.wav", path + "\\AudioEffects\\Radio-TX-1600.wav", true);
            File.Copy(currentDirectory + "\\Radio-RX-1600.wav", path + "\\AudioEffects\\Radio-RX-1600.wav", true);


            //    File.Copy(currentDirectory + "\\Installer.exe", path + "\\Installer.exe", true);
            File.Copy(currentDirectory + "\\DCS-SimpleRadioStandalone.lua", path + "\\DCS-SimpleRadioStandalone.lua",
                true);
            File.Copy(currentDirectory + "\\DCS-SRSGameGUI.lua", path + "\\DCS-SRSGameGUI.lua",
                true);
            File.Copy(currentDirectory + "\\DCS-SRS-AutoConnectGameGUI.lua", path + "\\DCS-SRS-AutoConnectGameGUI.lua",
                true);

            File.Copy(currentDirectory + "\\DCS-SRS-OverlayGameGUI.lua", path + "\\DCS-SRS-OverlayGameGUI.lua",
                true);

            File.Copy(currentDirectory + "\\DCS-SRS-Overlay.dlg", path + "\\DCS-SRS-Overlay.dlg",
                true);
            File.Copy(currentDirectory + "\\DCS-SRS-hook.lua", path + "\\DCS-SRS-hook.lua",
                true);

            var luasocket = GetPathDCS() + "\\LuaSocket"
            if( !(File.Exists(luasocket+"\\socket\\http.lua") &&
                  File.Exists(luasocket+"\\socket\\url.lua")) )
            {
		if( !Directory.Exists(luasocket+"\\socket") )
		{
			Thread.Sleep(200);
			CreateDirectory(luasocket+"\\socket");
			Thread.Sleep(200);
		}
                File.Copy(luasocket+"\\http.lua", luasocket+"\\socket\\http.lua");
                File.Copy(luasocket+"\\url.lua", luasocket+"\\socket\\url.lua");
            }
        }

        private void InstallShortcuts(string path)
        {
            string executablePath = Path.Combine(path, "SR-ClientRadio.exe");
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "DCS-SRS Client.lnk");

            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

            shortcut.Description = "DCS-SimpleRadio Standalone Client";
            shortcut.TargetPath = executablePath;
            shortcut.WorkingDirectory = path;
            shortcut.Save();
        }

        private void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

                // Create the rules
                var writerule = new FileSystemAccessRule(sid, FileSystemRights.Write, AccessControlType.Allow);

                var dir = Directory.CreateDirectory(path);

                dir.Refresh();
                //sleep! WTF directory is lagging behind state here...
                Thread.Sleep(200);

                var dSecurity = dir.GetAccessControl();
                dSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                dir.SetAccessControl(dSecurity);
                dir.Refresh();
            }
        }

        private void InstallScripts(string path)
        {
            //if scripts folder doesnt exist, create it
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(path + "\\Hooks");
            Thread.Sleep(100);

            var write = true;
            //does it contain an export.lua?
            if (File.Exists(path + "\\Export.lua"))
            {
                var contents = File.ReadAllText(path + "\\Export.lua");

                if (contents.Contains("SimpleRadioStandalone.lua"))
                {
//                    contents =
//                        contents.Replace(
//                            "local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Scripts\\DCS-SimpleRadioStandalone.lua]])",
//                            "local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Scripts\\DCS-SimpleRadioStandalone.lua]])");
//                    contents = contents.Trim();
//
//                    File.WriteAllText(path + "\\Export.lua", contents);

                    // do nothing
                }
                else
                {
                    var writer = File.AppendText(path + "\\Export.lua");

                    writer.WriteLine(
                        "\n  local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Scripts\\DCS-SimpleRadioStandalone.lua]])\n");
                    writer.Close();
                }
            }
            else
            {
                var writer = File.CreateText(path + "\\Export.lua");

                writer.WriteLine(
                    "\n  local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Scripts\\DCS-SimpleRadioStandalone.lua]])\n");
                writer.Close();
            }

            try
            {
                File.Copy(currentDirectory + "\\DCS-SimpleRadioStandalone.lua",
                    path + "\\DCS-SimpleRadioStandalone.lua", true);

                File.Copy(currentDirectory + "\\DCS-SRSGameGUI.lua",
                    path + "\\DCS-SRSGameGUI.lua", true);

                File.Copy(currentDirectory + "\\DCS-SRS-OverlayGameGUI.lua", path + "\\DCS-SRS-OverlayGameGUI.lua",
                    true);

                File.Copy(currentDirectory + "\\DCS-SRS-Overlay.dlg", path + "\\DCS-SRS-Overlay.dlg",
                    true);

                File.Copy(currentDirectory + "\\DCS-SRS-hook.lua", path + "\\Hooks\\DCS-SRS-hook.lua",
                    true);
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(
                    "Install files not found - Unable to install! \n\nMake sure you extract all the files in the zip then run the Installer",
                    "Not Unzipped", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
        }

        //http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true
        //Recursive Directory Delete
        public static void DeleteDirectory(string target_dir)
        {
            var files = Directory.GetFiles(target_dir);
            var dirs = Directory.GetDirectories(target_dir);

            foreach (var file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }


        private void UninstallSR()
        {
            if (Is_SimpleRadio_running())
            {
                MessageBox.Show("Please close SimpleRadio Standalone Overlay before removing!",
                    "SR Standalone Installer", MessageBoxButton.OK, MessageBoxImage.Error);

                Environment.Exit(0);

                return;
            }

            InstallButton.IsEnabled = false;
            RemoveButton.IsEnabled = false;

            InstallButton.Content = "Removing...";

            var savedGamesPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) +
                                 "\\Saved Games\\";

            var dcsPath = savedGamesPath + "DCS";

            RemoveScripts(dcsPath + ".openalpha\\Scripts");
            RemoveScripts(dcsPath + ".openbeta\\Scripts");
            RemoveScripts(dcsPath + "\\Scripts");

            if (Directory.Exists(srPath.Text) && File.Exists(srPath.Text + "\\SR-ClientRadio.exe"))
            {
                DeleteFileIfExists(srPath.Text + "\\SR-ClientRadio.exe");
                DeleteFileIfExists(srPath.Text + "\\opus.dll");
                DeleteFileIfExists(srPath.Text + "\\speexdsp.dll");
                DeleteFileIfExists(srPath.Text + "\\awacs-radios.json");
                DeleteFileIfExists(srPath.Text + "\\SR-ClientRadio.exe");
                DeleteFileIfExists(srPath.Text + "\\SR-Server.exe");
                DeleteFileIfExists(srPath.Text + "\\DCS-SimpleRadioStandalone.lua");
                DeleteFileIfExists(srPath.Text + "\\DCS-SRSGameGUI.lua");
                DeleteFileIfExists(srPath.Text + "\\DCS-SRS-AutoConnectGameGUI.lua");
                DeleteFileIfExists(srPath.Text + "\\DCS-SRS-OverlayGameGUI.lua");
                DeleteFileIfExists(srPath.Text + "\\DCS-SRS-Overlay.dlg");
                DeleteFileIfExists(srPath.Text + "\\clientlog.txt");
                DeleteFileIfExists(srPath.Text + "\\DCS-SRS-hook.lua");
                DeleteFileIfExists(srPath.Text + "\\AudioEffects\\KY-58-RX-1600.wav");
                DeleteFileIfExists(srPath.Text + "\\AudioEffects\\KY-58-TX-1600.wav");
                DeleteFileIfExists(srPath.Text + "\\AudioEffects\\Radio-RX-1600.wav");
                DeleteFileIfExists(srPath.Text + "\\AudioEffects\\Radio-TX-1600.wav");
            }

            DeleteRegKeys();

            RemoveShortcuts();

            MessageBox.Show(
                "SR Standalone Removed Successfully!\n\nContaining folder left just incase you want favourites or frequencies",
                "SR Standalone Installer",
                MessageBoxButton.OK, MessageBoxImage.Information);

            Environment.Exit(0);
        }

        private void RemoveShortcuts()
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "DCS-SRS Client.lnk");

            DeleteFileIfExists(shortcutPath);
        }

        private void RemoveScripts(string path)
        {
            //does it contain an export.lua?
            if (File.Exists(path + "\\Export.lua"))
            {
                var contents = File.ReadAllText(path + "\\Export.lua");

                if (contents.Contains("SimpleRadioStandalone.lua"))
                {
                    contents = contents.Replace("dofile(lfs.writedir()..[[Scripts\\DCS-SimpleRadioStandalone.lua]])",
                        "");
                    contents =
                        contents.Replace(
                            "local dcsSr=require('lfs');dofile(dcsSr.writedir()..[[Scripts\\DCS-SimpleRadioStandalone.lua]])",
                            "");
                    contents = contents.Trim();

                    File.WriteAllText(path + "\\Export.lua", contents);
                }
            }

            DeleteFileIfExists(path + "\\DCS-SimpleRadioStandalone.lua");
            DeleteFileIfExists(path + "\\DCS-SRSGameGUI.lua");
            DeleteFileIfExists(path + "\\DCS-SRS-AutoConnectGameGUI.lua");
            DeleteFileIfExists(path + "\\DCS-SRS-Overlay.dlg");
            DeleteFileIfExists(path + "\\DCS-SRS-OverlayGameGUI.lua");
            DeleteFileIfExists(path + "\\Hooks\\DCS-SRS-Hook.lua");
        }

        private void Remove_Plugin(object sender, RoutedEventArgs e)
        {
            UninstallSR();
        }
    }
}
