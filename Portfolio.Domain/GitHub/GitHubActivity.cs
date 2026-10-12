namespace Portfolio.Domain.GitHub;

/// <summary>Public GitHub activity shown on the portfolio.</summary>
public sealed record GitHubActivity
{
    public bool Available { get; init; }
    public bool Stale { get; init; }
    public string User { get; init; } = "";
    public DateTimeOffset? RetrievedAt { get; init; }
    public GitHubProfile? Profile { get; init; }
    public GitHubRepository[] Repositories { get; init; } = [];
    public GitHubCommit[] RecentCommits { get; init; } = [];
    public LanguageShare[] Languages { get; init; } = [];

    public static GitHubActivity Unavailable(string user) => new() { User = user };
}

public sealed record GitHubProfile(
    string Login, string Name, string AvatarUrl, string Url, string Bio, int PublicRepositories, int Followers);

public sealed record GitHubRepository(
    string Name, string Description, string Url, string Homepage, string Language,
    int Stars, int Forks, bool IsFork, bool IsArchived, DateTimeOffset PushedAt, string[] Topics);

public sealed record GitHubCommit(string Repository, string Message, string Sha, string Url, DateTimeOffset Date);

public sealed record LanguageUsage(string Name, long Bytes);

public sealed record LanguageShare(string Name, double Percentage);
