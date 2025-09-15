using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Octokit;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

public abstract class GitHubUpdaterBase
{
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    protected static readonly string GitHubUsername = "ciribob";
    protected static readonly string GitHubRepository = "DCS-SimpleRadioStandalone";
    protected static readonly string GitHubUserAgent = $"{GitHubUsername}_{GitHubRepository}";

    // Semaphore to serialize all update checks across all inheritors
    private static readonly SemaphoreSlim UpdateSemaphore = new(1, 1);

    protected readonly GitHubClient GitHubClient;

    protected GitHubUpdaterBase(string version = "1.0.0.0")
    {
        GitHubClient = new GitHubClient(new ProductHeaderValue(GitHubUserAgent, version));
    }

    protected async Task<T> ExecuteGitHubRequestWithRateLimitAsync<T>(Func<Task<T>> githubCall, int maxRetries = 3)
    {
        int attempt = 0;
        await UpdateSemaphore.WaitAsync();
        Logger.Debug("Update semaphore acquired for GitHubUpdaterBase.");
        try
        {
            while (attempt < maxRetries)
            {
                try
                {
                    return await githubCall();
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
            Logger.Debug("Releasing update semaphore for GitHubUpdaterBase.");
            UpdateSemaphore.Release();
        }
    }

    protected async Task<IReadOnlyList<Release>> GetAllReleasesAsync()
    {
        return await ExecuteGitHubRequestWithRateLimitAsync(() =>
            GitHubClient.Repository.Release.GetAll(GitHubUsername, GitHubRepository));
    }

}