using Microsoft.Extensions.Logging;
using Portfolio.Errors.Registry;
using Microsoft.Extensions.Options;
using Portfolio.Domain.GitHub;

namespace Portfolio.Application.GitHub;

/// <summary>
/// Builds the public GitHub summary and keeps it in memory. A single refresh runs at a time; when GitHub fails,
/// the last summary is served as stale for a limited time and new attempts wait for the retry delay.
/// </summary>
public sealed class GitHubActivityService(IGitHubSource source, IOptions<GitHubOptions> options,
    TimeProvider clock, ILogger<GitHubActivityService> logger, IErrorReporter reporter, IErrorCorrelation correlation) : IGitHubActivityService, IDisposable
{
    private const int MaximumCommitMessageLength = 140;
    private const int LanguagesShown = 6;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GitHubActivity? _cached;
    private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;

    public async Task<GitHubActivity> GetAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return GitHubActivity.Unavailable(settings.User);
        if (IsFresh(_cached, settings)) return _cached!;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh(_cached, settings)) return _cached!;
            if (clock.GetUtcNow() < _nextAttempt) return Fallback(settings);

            try
            {
                _cached = await LoadAsync(settings, cancellationToken);
                return _cached;
            }
            catch (GitHubUnavailableException exception)
            {
                _nextAttempt = clock.GetUtcNow() + settings.RetryDelay;
                var traceId = correlation.CurrentTraceId ?? Guid.NewGuid().ToString("N");
                logger.LogWarning("GitHub refresh failed: {ExceptionType}; correlation {TraceId}", exception.GetType().FullName, traceId);
                // The endpoint still serves its documented degraded response (HTTP 200).
                await reporter.ReportAsync(new ErrorRecord(Guid.NewGuid().ToString("N"), clock.GetUtcNow(),
                    "github.unavailable", 200, traceId, "GET", "api/github", exception.GetType().FullName, "dependency"));
                return Fallback(settings);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh(GitHubActivity? activity, GitHubOptions settings)
        => activity?.RetrievedAt is { } retrievedAt && clock.GetUtcNow() - retrievedAt < settings.CacheDuration;

    private GitHubActivity Fallback(GitHubOptions settings)
        => _cached?.RetrievedAt is { } retrievedAt && clock.GetUtcNow() - retrievedAt < settings.StaleDuration
            ? _cached with { Stale = true }
            : GitHubActivity.Unavailable(settings.User);

    private async Task<GitHubActivity> LoadAsync(GitHubOptions settings, CancellationToken cancellationToken)
    {
        var user = settings.User;
        var profileTask = source.GetProfileAsync(user, cancellationToken);
        var repositoriesTask = source.GetRepositoriesAsync(user, cancellationToken);
        await Task.WhenAll(profileTask, repositoriesTask);

        var repositories = repositoriesTask.Result
            .Where(repository => !repository.IsFork)
            .OrderByDescending(repository => repository.PushedAt)
            .Take(settings.MaxRepositories)
            .ToArray();

        var languagesTask = Task.WhenAll(repositories.Take(settings.LanguageRepositories)
            .Select(repository => source.GetLanguagesAsync(user, repository.Name, cancellationToken)));
        var commitsTask = Task.WhenAll(repositories.Where(repository => !repository.IsArchived)
            .Take(settings.CommitRepositories)
            .Select(repository => source.GetCommitsAsync(user, repository.Name, settings.CommitsPerRepository, cancellationToken)));
        await Task.WhenAll(languagesTask, commitsTask);

        return new GitHubActivity
        {
            Available = true,
            User = user,
            RetrievedAt = clock.GetUtcNow(),
            Profile = profileTask.Result,
            Repositories = repositories,
            RecentCommits = commitsTask.Result
                .SelectMany(commits => commits)
                .OrderByDescending(commit => commit.Date)
                .Take(settings.MaxCommits)
                .Select(commit => commit with { Message = Summarize(commit.Message) })
                .ToArray(),
            Languages = Share(languagesTask.Result.SelectMany(languages => languages))
        };
    }

    private static string Summarize(string message)
    {
        var firstLine = message.Split('\n', 2)[0].Trim();
        return firstLine.Length <= MaximumCommitMessageLength
            ? firstLine
            : string.Concat(firstLine.AsSpan(0, MaximumCommitMessageLength - 1), "…");
    }

    private static LanguageShare[] Share(IEnumerable<LanguageUsage> usages)
    {
        var totals = usages.Where(usage => usage.Bytes > 0)
            .GroupBy(usage => usage.Name)
            .Select(group => (Name: group.Key, Bytes: group.Sum(usage => usage.Bytes)))
            .OrderByDescending(language => language.Bytes)
            .ToArray();
        var total = totals.Sum(language => language.Bytes);
        if (total == 0) return [];

        var shares = totals.Take(LanguagesShown)
            .Select(language => new LanguageShare(language.Name, Percentage(language.Bytes, total)))
            .ToList();
        var others = totals.Skip(LanguagesShown).Sum(language => language.Bytes);
        if (others > 0) shares.Add(new LanguageShare("Otros", Percentage(others, total)));
        return [.. shares];
    }

    private static double Percentage(long bytes, long total) => Math.Round(bytes * 100d / total, 1);

    public void Dispose() => _gate.Dispose();
}
