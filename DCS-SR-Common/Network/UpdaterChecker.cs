using System;
using System.Diagnostics;
using System.Security.Principal;
using NLog;
using Octokit;
#if NET461
using System.Windows;
#endif

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    //Quick and dirty update checker based on GitHub Published Versions
    public class UpdaterChecker
    {
        public static readonly string GITHUB_USERNAME = "ciribob";
        public static readonly string GITHUB_REPOSITORY = "DCS-SimpleRadioStandalone";
        // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
        public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";

        public static readonly string MINIMUM_PROTOCOL_VERSION = "1.9.0.0";

        public static readonly string VERSION = "2.0.0.0";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static async void CheckForUpdate(bool checkForBetaUpdates)
        {
            Version currentVersion = Version.Parse(VERSION);

#if DEBUG
            _logger.Info("Skipping update check due to DEBUG mode");
#else
            try
            {
                var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, VERSION));

                var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);

                Version latestStableVersion = new Version();
                Release latestStableRelease = null;
                Version latestBetaVersion = new Version();
                Release latestBetaRelease = null;

                // Retrieve last stable and beta branch release as tagged on GitHub
                foreach (Release release in releases)
                {
                    Version releaseVersion;

                    if (Version.TryParse(release.TagName.Replace("v", ""), out releaseVersion))
                    {
                        if (release.Prerelease && releaseVersion > latestBetaVersion)
                        {
                            latestBetaRelease = release;
                            latestBetaVersion = releaseVersion;
                        }
                        else if (!release.Prerelease && releaseVersion > latestStableVersion)
                        {
                            latestStableRelease = release;
                            latestStableVersion = releaseVersion;
                        }
                    }
                    else
                    {
                        _logger.Warn($"Failed to parse GitHub release version {release.TagName}");
                    }
                }

                // Compare latest versions with currently running version depending on user branch choice
                if (checkForBetaUpdates && latestBetaVersion > currentVersion)
                {
                    ShowUpdateAvailableDialog("beta", latestBetaVersion, latestBetaRelease.HtmlUrl, true);
                }
                else if (latestStableVersion > currentVersion)
                {
                    ShowUpdateAvailableDialog("stable", latestStableVersion, latestStableRelease.HtmlUrl, false);
                }
                else if (checkForBetaUpdates && latestBetaVersion == currentVersion)
                {
                    _logger.Warn($"Running latest beta version: {currentVersion}");
                }
                else if (latestStableVersion == currentVersion)
                {
                    _logger.Warn($"Running latest stable version: {currentVersion}");
                }
                else
                {
                    _logger.Warn($"Running development version: {currentVersion}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check for updated version");
            }
#endif
        }

        public static void ShowUpdateAvailableDialog(string branch, Version version, string url, bool beta)
        {
            _logger.Warn($"New {branch} version available on GitHub: {version}");
#if NET461
            var result = MessageBox.Show($"New {branch} version {version} available!\n\nDo you want to Auto update? This will close SRS\n\nYes - Auto Update\nNo - Manual Update\nCancel - Ignore",
                "Update available", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    LaunchUpdater(beta);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to Auto Update - please download latest version manually",
                        "Auto Update Error", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

                    Process.Start(url);
                }

            }
            else if (result == MessageBoxResult.No)
            {
                Process.Start(url);
            }
#else
            _logger.Info($"New {branch} version {version} available!");
#endif
        }

#if NET461
        private static void LaunchUpdater(bool beta)
        {
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = principal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!hasAdministrativeRight)
            {
               
                    var location = AppDomain.CurrentDomain.BaseDirectory;

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = location,
                        FileName = location + "SRS-AutoUpdater.exe",
                        Verb = "runas"
                    };

                    if (beta)
                    {
                        startInfo.Arguments = "-beta";
                    }
                  
                    try
                    {
                        Process p = Process.Start(startInfo);
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        MessageBox.Show(
                            "SRS Auto Update Requires Admin Rights",
                            "UAC Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
            }
            else
            {
                if (beta)
                {
                    Process.Start("SRS-AutoUpdater.exe", "-beta");
                }
                else
                {
                    Process.Start("SRS-AutoUpdater.exe");
                }
            }
        }
#endif
    }
}