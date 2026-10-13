using Portfolio.Errors.Registry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Portfolio.Application.GitHub;
using Portfolio.Domain.GitHub;

namespace Portfolio.Tests.GitHub;

public sealed class GitHubActivityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Builds_the_summary_without_forks_and_with_recent_commits_first()
    {
        var source = new FakeGitHubSource();
        var service = CreateService(source, new ManualClock(Now));

        var activity = await service.GetAsync(CancellationToken.None);

        Assert.True(activity.Available);
        Assert.False(activity.Stale);
        Assert.Equal(["api", "web"], activity.Repositories.Select(repository => repository.Name));
        Assert.Equal(["Fix rounding", "Add endpoint"], activity.RecentCommits.Select(commit => commit.Message));
        Assert.Equal(Now, activity.RetrievedAt);
    }

    [Fact]
    public async Task Groups_languages_beyond_the_sixth_as_others()
    {
        var source = new FakeGitHubSource
        {
            Languages = ["C#", "TypeScript", "CSS", "HTML", "PowerShell", "Dockerfile", "Python", "JavaScript"]
        };
        var activity = await CreateService(source, new ManualClock(Now)).GetAsync(CancellationToken.None);

        Assert.Equal(7, activity.Languages.Length);
        Assert.Equal("Otros", activity.Languages[^1].Name);
        Assert.Equal(100, activity.Languages.Sum(language => language.Percentage), precision: 0);
    }

    [Fact]
    public async Task Keeps_only_the_first_line_of_long_commit_messages()
    {
        var source = new FakeGitHubSource { CommitMessage = new string('a', 200) + "\nbody" };
        var activity = await CreateService(source, new ManualClock(Now)).GetAsync(CancellationToken.None);

        Assert.All(activity.RecentCommits, commit => Assert.Equal(140, commit.Message.Length));
    }

    [Fact]
    public async Task Reuses_the_summary_while_it_is_fresh()
    {
        var source = new FakeGitHubSource();
        var clock = new ManualClock(Now);
        var service = CreateService(source, clock);

        await service.GetAsync(CancellationToken.None);
        clock.Now = Now.AddMinutes(29);
        await service.GetAsync(CancellationToken.None);

        Assert.Equal(1, source.ProfileRequests);
    }

    [Fact]
    public async Task Serves_the_last_summary_as_stale_when_github_fails()
    {
        var source = new FakeGitHubSource();
        var clock = new ManualClock(Now);
        var service = CreateService(source, clock);
        await service.GetAsync(CancellationToken.None);

        source.Fail = true;
        clock.Now = Now.AddHours(1);
        var stale = await service.GetAsync(CancellationToken.None);
        clock.Now = Now.AddHours(1).AddMinutes(1);
        await service.GetAsync(CancellationToken.None);

        Assert.True(stale.Available);
        Assert.True(stale.Stale);
        Assert.Equal(2, source.ProfileRequests); // the second failure waits for the retry delay
    }

    [Fact]
    public async Task Reports_unavailable_when_there_is_nothing_recent_to_serve()
    {
        var source = new FakeGitHubSource { Fail = true };
        var activity = await CreateService(source, new ManualClock(Now)).GetAsync(CancellationToken.None);

        Assert.False(activity.Available);
        Assert.Equal("diegoesp10", activity.User);
    }

    [Fact]
    public async Task Does_not_call_github_when_disabled()
    {
        var source = new FakeGitHubSource();
        var service = CreateService(source, new ManualClock(Now), options => options.Enabled = false);

        var activity = await service.GetAsync(CancellationToken.None);

        Assert.False(activity.Available);
        Assert.Equal(0, source.ProfileRequests);
    }

    private static GitHubActivityService CreateService(IGitHubSource source, TimeProvider clock,
        Action<GitHubOptions>? configure = null, RecordingReporter? reporter = null)
    {
        var options = new GitHubOptions { User = "diegoesp10" };
        configure?.Invoke(options);
        return new GitHubActivityService(source, Options.Create(options), clock, NullLogger<GitHubActivityService>.Instance, reporter ?? new RecordingReporter(), new TestCorrelation());
    }

    [Fact]
    public async Task Records_dependency_failure_once_per_refresh_with_safe_context()
    {
        var source = new FakeGitHubSource { Fail = true };
        var reporter = new RecordingReporter();
        using var service = CreateService(source, new ManualClock(Now), reporter: reporter);
        await service.GetAsync(CancellationToken.None);
        await service.GetAsync(CancellationToken.None);
        var entry = Assert.Single(reporter.Entries);
        Assert.Equal("github.unavailable", entry.Code);
        Assert.Equal("test-trace", entry.TraceId);
        Assert.Equal(200, entry.Status); // The public API continues to serve its degraded contract.
        Assert.DoesNotContain("403", entry.ExceptionType);
    }

    private sealed class RecordingReporter : IErrorReporter
    {
        public List<ErrorRecord> Entries { get; } = [];
        public Task ReportAsync(ErrorRecord entry) { Entries.Add(entry); return Task.CompletedTask; }
    }
    private sealed class TestCorrelation : IErrorCorrelation
    {
        public string CurrentTraceId => "test-trace";
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeGitHubSource : IGitHubSource
    {
        public bool Fail { get; set; }
        public int ProfileRequests { get; private set; }
        public string[] Languages { get; init; } = ["C#", "TypeScript"];
        public string? CommitMessage { get; init; }

        public Task<GitHubProfile> GetProfileAsync(string user, CancellationToken cancellationToken)
        {
            ProfileRequests++;
            if (Fail) throw new GitHubUnavailableException("GitHub respondió 403.");
            return Task.FromResult(new GitHubProfile(user, "Diego", "https://avatars.example/u", "https://github.com/" + user, "", 3, 1));
        }

        public Task<IReadOnlyList<GitHubRepository>> GetRepositoriesAsync(string user, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GitHubRepository>>([
                Repository("web", Now.AddDays(-3)),
                Repository("fork", Now, isFork: true),
                Repository("api", Now.AddDays(-1))
            ]);

        public Task<IReadOnlyList<LanguageUsage>> GetLanguagesAsync(string owner, string repository, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LanguageUsage>>(
                Languages.Select((name, index) => new LanguageUsage(name, 1000 - index * 100)).ToArray());

        public Task<IReadOnlyList<GitHubCommit>> GetCommitsAsync(string owner, string repository, int count, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GitHubCommit>>(repository == "api"
                ? [new GitHubCommit(repository, CommitMessage ?? "Fix rounding\n\nDetails", "abc1234", "https://github.com/c/1", Now.AddHours(-2))]
                : [new GitHubCommit(repository, CommitMessage ?? "Add endpoint", "def5678", "https://github.com/c/2", Now.AddDays(-3))]);

        private static GitHubRepository Repository(string name, DateTimeOffset pushedAt, bool isFork = false)
            => new(name, "", "https://github.com/diegoesp10/" + name, "", "C#", 0, 0, isFork, false, pushedAt, []);
    }
}
