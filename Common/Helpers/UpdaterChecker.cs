using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Octokit;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

//Quick and dirty update checker based on GitHub Published Versions
//TODO make this a singleton
public class UpdaterChecker
{
    public delegate void UpdateCallback(UpdateCallbackResult result);

    private static UpdaterChecker _instance;
    private static readonly object _lock = new();

    public static readonly string GITHUB_USERNAME = "ciribob";

    public static readonly string GITHUB_REPOSITORY = "DCS-SimpleRadioStandalone";

    // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
    public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";

    public static readonly string MINIMUM_PROTOCOL_VERSION = "1.9.0.0";

    public static readonly string VERSION = "2.3.7.0";

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static UpdaterChecker Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null) _instance = new UpdaterChecker();
            }

            return _instance;
        }
    }

    public async void CheckForUpdate(bool checkForBetaUpdates, UpdateCallback updateCallback)
    {
        var currentVersion = Version.Parse(VERSION);


        try
        {
            var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, VERSION));

#if DEBUG
            return;
#endif
            var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);

            var latestStableVersion = new Version();
            Release latestStableRelease = null;
            var latestBetaVersion = new Version();
            Release latestBetaRelease = null;

            // Retrieve last stable and beta branch release as tagged on GitHub
            foreach (var release in releases)
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
                updateCallback?.Invoke(new UpdateCallbackResult
                {
                    Beta = true,
                    Branch = "beta",
                    UpdateAvailable = true,
                    Version = latestBetaVersion,
                    Url = latestBetaRelease.HtmlUrl,
                    Error = false
                });
            }
            else if (latestStableVersion > currentVersion)
            {
                updateCallback?.Invoke(new UpdateCallbackResult
                {
                    Beta = false,
                    Branch = "stable",
                    UpdateAvailable = true,
                    Version = latestStableVersion,
                    Url = latestStableRelease.HtmlUrl,
                    Error = false
                });
            }
            else if (checkForBetaUpdates && latestBetaVersion == currentVersion)
            {
                updateCallback?.Invoke(new UpdateCallbackResult
                {
                    Beta = true,
                    Branch = "beta",
                    UpdateAvailable = false,
                    Version = latestBetaVersion,
                    Url = latestBetaRelease.HtmlUrl,
                    Error = false
                });
                _logger.Warn($"Running latest beta version: {currentVersion}");
            }
            else if (latestStableVersion == currentVersion)
            {
                updateCallback?.Invoke(new UpdateCallbackResult
                {
                    Beta = false,
                    Branch = "stable",
                    UpdateAvailable = false,
                    Version = latestStableVersion,
                    Url = latestStableRelease.HtmlUrl,
                    Error = false
                });
                _logger.Warn($"Running latest stable version: {currentVersion}");
            }
            else
            {
                updateCallback?.Invoke(new UpdateCallbackResult
                {
                    Beta = false,
                    Branch = "stable",
                    UpdateAvailable = false,
                    Version = latestStableVersion,
                    Url = latestStableRelease.HtmlUrl,
                    Error = false
                });
                _logger.Warn($"Running development version: {currentVersion}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check for updated version");
            updateCallback?.Invoke(new UpdateCallbackResult
            {
                Beta = false,
                Branch = "unknown",
                UpdateAvailable = false,
                Version = null,
                Url = null,
                Error = true
            });
        }
    }

    private bool IsDCSRunning()
    {
        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.ProcessName.ToLower().Trim().Equals("dcs"))
                return true;

        return false;
    }

    public bool LaunchUpdater(bool beta)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var location = AppDomain.CurrentDomain.BaseDirectory;
        var autoUpdatePath = "";

        if (File.Exists(Path.Combine(location, "../SRS-AutoUpdater.exe")))
        {
            autoUpdatePath = Path.Combine(location, "../SRS-AutoUpdater.exe");
        }
        else if (File.Exists(Path.Combine(location, "SRS-AutoUpdater.exe")))
        {
            autoUpdatePath = Path.Combine(location, "SRS-AutoUpdater.exe");
        }
        else
        {
            _logger.Error("Unable to find SRS-AutoUpdater.exe");
            return false;
        }


        Task.Run(() =>
        {
            while (IsDCSRunning()) Thread.Sleep(5000);

#pragma warning disable CA1416
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
#pragma warning restore CA1416
#pragma warning disable CA1416
            var hasAdministrativeRight = principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416

            if (!hasAdministrativeRight)
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = location,
                    FileName = autoUpdatePath,
                    Verb = "runas"
                };

                if (beta) startInfo.Arguments = "-beta";

                try
                {
                    var p = Process.Start(startInfo);
                }
                catch (Win32Exception)
                {
                    //TODO sort this out with a callback
                    // MessageBox.Show(
                    //     "SRS Auto Update Requires Admin Rights",
                    //     "UAC Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                if (beta)
                    Process.Start(new ProcessStartInfo(autoUpdatePath, "-beta")
                        { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo(autoUpdatePath)
                        { UseShellExecute = true });
            }
        });

        return true;
    }
}

public class UpdateCallbackResult
{
    public bool UpdateAvailable { get; set; }
    public string Branch { get; set; }
    public Version Version { get; set; }
    public string Url { get; set; }
    public bool Beta { get; set; }
    public bool Error { get; set; }
}