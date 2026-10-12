using Portfolio.Domain.GitHub;

namespace Portfolio.Application.GitHub;

public interface IGitHubActivityService
{
    Task<GitHubActivity> GetAsync(CancellationToken cancellationToken);
}
