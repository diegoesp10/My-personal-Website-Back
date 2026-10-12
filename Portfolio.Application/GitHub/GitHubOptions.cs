namespace Portfolio.Application.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public bool Enabled { get; set; } = true;
    public string User { get; set; } = "";
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan StaleDuration { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(2);
    public int MaxRepositories { get; set; } = 12;
    public int LanguageRepositories { get; set; } = 8;
    public int CommitRepositories { get; set; } = 5;
    public int CommitsPerRepository { get; set; } = 5;
    public int MaxCommits { get; set; } = 10;
}
