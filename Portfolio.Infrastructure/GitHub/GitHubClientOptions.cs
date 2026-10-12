namespace Portfolio.Infrastructure.GitHub;

public sealed class GitHubClientOptions
{
    public const string SectionName = "GitHub";

    public string BaseAddress { get; set; } = "https://api.github.com/";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Optional token. Without it GitHub allows 60 anonymous requests per hour, enough with the cache.</summary>
    public string Token { get; set; } = "";
}
