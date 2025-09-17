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
    private static readonly int DefaultMaxRetries = 3;

    // Semaphore to serialize all update checks
    private static readonly SemaphoreSlim UpdateSemaphore = new(1, 1);

    public static async Task<T> ExecuteGitHubRequestWithRateLimitAsync<T>(
        Func<GitHubClient, Task<T>> githubCall,
        Action<TimeSpan, int, int>? onRateLimitWait = null,
        string version = "",
        int maxRetries = 0,
        CancellationToken cancellationToken = default
    )
    {
        int attempt = 0;
        maxRetries = (maxRetries <= 0) ? DefaultMaxRetries : maxRetries; //if maxRetries is 0 or less use default
        version = (string.IsNullOrWhiteSpace(version)) ? DefaultVersion : version; //if version is empty use default
        await UpdateSemaphore.WaitAsync();
        Logger.Debug("Update semaphore acquired for GitHubUpdater.");
        try
        {
            var client = new GitHubClient(new ProductHeaderValue(GitHubUserAgent, version));
            while (attempt < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await githubCall(client);
                }
                catch (RateLimitExceededException ex)
                {
                    attempt++;
                    var waitFor = ex.Reset - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                    if (waitFor < TimeSpan.Zero)
                        waitFor = TimeSpan.FromSeconds(60); //wait at least 60 seconds if the reset time is in the past

                    Logger.Warn($"GitHub API rate limit exceeded. Waiting {waitFor.TotalSeconds:N0} seconds before retrying (attempt {attempt}/{maxRetries})");

                    if (attempt >= maxRetries)
                        throw;

                    // Notify the caller about the wait
                    onRateLimitWait?.Invoke(waitFor, attempt, maxRetries);

                    await Task.Delay(waitFor, cancellationToken);
                }
            }
            throw new Exception($"Maximum retry attempts ({maxRetries}) reached for GitHub API call.");
        }
        finally
        {
            Logger.Debug("Releasing update semaphore for GitHubUpdater.");
            UpdateSemaphore.Release();
        }
    }

    public static async Task<IReadOnlyList<Release>> GetAllReleasesAsync(
        Action<TimeSpan, int, int>? onRateLimitWait = null,
        string version = "",
        int maxRetries = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await ExecuteGitHubRequestWithRateLimitAsync(
            client => client.Repository.Release.GetAll(GitHubUsername, GitHubRepository),
            onRateLimitWait,
            version,
            maxRetries,
            cancellationToken
        );
    }
}