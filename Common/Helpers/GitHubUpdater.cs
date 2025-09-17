using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Octokit;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

public static class GitHubUpdater
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly string GitHubUsername = "ciribob";
    private static readonly string GitHubRepository = "DCS-SimpleRadioStandalone";
    private static readonly string GitHubUserAgent = $"{GitHubUsername}_{GitHubRepository}";

    private static readonly string DefaultVersion = "1.0.0.0";

    // Semaphore to serialize all update checks
    private static readonly SemaphoreSlim UpdateSemaphore = new(1, 1);

    public static async Task<T> ExecuteGitHubRequestWithRateLimitAsync<T>(
        Func<GitHubClient, Task<T>> githubCall,
        string version = "",
        int maxRetries = 3)
    {
        int attempt = 0;
        version = (string.IsNullOrWhiteSpace(version)) ? DefaultVersion : version; //if version is empty use default
        await UpdateSemaphore.WaitAsync();
        Logger.Debug("Update semaphore acquired for GitHubUpdater.");
        try
        {
            var client = new GitHubClient(new ProductHeaderValue(GitHubUserAgent, version));
            while (attempt < maxRetries)
            {
                try
                {
                    return await githubCall(client);
                }
                catch (RateLimitExceededException ex)
                {
                    attempt++;
                    var waitFor = ex.Reset - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                    if (waitFor < TimeSpan.Zero)
                        waitFor = TimeSpan.FromSeconds(60);

                    Logger.Warn($"GitHub API rate limit exceeded. Waiting {waitFor.TotalSeconds:N0} seconds before retrying (attempt {attempt}/{maxRetries})");

                    if (attempt >= maxRetries)
                        throw;

                    await Task.Delay(waitFor);
                }
            }
            throw new Exception("Maximum retry attempts reached for GitHub API call.");
        }
        finally
        {
            Logger.Debug("Releasing update semaphore for GitHubUpdater.");
            UpdateSemaphore.Release();
        }
    }

    public static async Task<IReadOnlyList<Release>> GetAllReleasesAsync(string version = "")
    {
        return await ExecuteGitHubRequestWithRateLimitAsync(
            client => client.Repository.Release.GetAll(GitHubUsername, GitHubRepository),
            version);
    }
}