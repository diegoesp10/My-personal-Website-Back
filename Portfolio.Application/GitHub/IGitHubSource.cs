using Portfolio.Domain.GitHub;

namespace Portfolio.Application.GitHub;

/// <summary>Reads public data from GitHub. Implementations throw <see cref="GitHubUnavailableException"/> when GitHub cannot answer.</summary>
public interface IGitHubSource
{
    Task<GitHubProfile> GetProfileAsync(string user, CancellationToken cancellationToken);
    Task<IReadOnlyList<GitHubRepository>> GetRepositoriesAsync(string user, CancellationToken cancellationToken);
    Task<IReadOnlyList<LanguageUsage>> GetLanguagesAsync(string owner, string repository, CancellationToken cancellationToken);
    Task<IReadOnlyList<GitHubCommit>> GetCommitsAsync(string owner, string repository, int count, CancellationToken cancellationToken);
}
