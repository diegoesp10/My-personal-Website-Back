using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Portfolio.Application.GitHub;
using Portfolio.Domain.GitHub;

namespace Portfolio.Infrastructure.GitHub;

/// <summary>Public GitHub REST API. Uses a named client from the factory so the singleton never pins a handler.</summary>
public sealed class GitHubApiSource(IHttpClientFactory clientFactory) : IGitHubSource
{
    public const string ClientName = "github";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<GitHubProfile> GetProfileAsync(string user, CancellationToken cancellationToken)
    {
        var profile = await GetAsync<UserResponse>($"users/{Escape(user)}", cancellationToken)
            ?? throw new GitHubUnavailableException("GitHub devolvió un perfil vacío.");
        return new GitHubProfile(profile.Login, string.IsNullOrWhiteSpace(profile.Name) ? profile.Login : profile.Name.Trim(),
            profile.AvatarUrl, profile.HtmlUrl, profile.Bio?.Trim() ?? "", profile.PublicRepos, profile.Followers);
    }

    public async Task<IReadOnlyList<GitHubRepository>> GetRepositoriesAsync(string user, CancellationToken cancellationToken)
    {
        var repositories = await GetAsync<RepositoryResponse[]>(
            $"users/{Escape(user)}/repos?type=owner&sort=pushed&per_page=100", cancellationToken) ?? [];
        return repositories.Select(repository => new GitHubRepository(
                repository.Name, repository.Description ?? "", repository.HtmlUrl, repository.Homepage ?? "",
                repository.Language ?? "", repository.StargazersCount, repository.ForksCount, repository.Fork,
                repository.Archived, repository.PushedAt ?? repository.UpdatedAt, repository.Topics ?? []))
            .ToArray();
    }

    public async Task<IReadOnlyList<LanguageUsage>> GetLanguagesAsync(string owner, string repository,
        CancellationToken cancellationToken)
    {
        var languages = await GetAsync<Dictionary<string, long>>(
            $"repos/{Escape(owner)}/{Escape(repository)}/languages", cancellationToken) ?? [];
        return languages.Select(language => new LanguageUsage(language.Key, language.Value)).ToArray();
    }

    public async Task<IReadOnlyList<GitHubCommit>> GetCommitsAsync(string owner, string repository, int count,
        CancellationToken cancellationToken)
    {
        // GitHub answers 409 Conflict for a repository without commits.
        var commits = await GetAsync<CommitResponse[]>(
            $"repos/{Escape(owner)}/{Escape(repository)}/commits?per_page={count}", cancellationToken, allowConflict: true) ?? [];
        return commits.Select(commit => new GitHubCommit(repository, commit.Commit.Message, commit.Sha[..Math.Min(7, commit.Sha.Length)],
                commit.HtmlUrl, commit.Commit.Author?.Date ?? commit.Commit.Committer?.Date ?? DateTimeOffset.MinValue))
            .ToArray();
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken, bool allowConflict = false)
    {
        var client = clientFactory.CreateClient(ClientName);
        try
        {
            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (allowConflict && response.StatusCode == HttpStatusCode.Conflict) return default;
            if (!response.IsSuccessStatusCode)
            {
                var retryAt = RetryAt(response);
                throw new GitHubUnavailableException(
                    $"GitHub respondió {(int)response.StatusCode} a {path}{(retryAt is null ? "" : $"; límite agotado hasta {retryAt:u}")}.",
                    retryAt: retryAt);
            }
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new GitHubUnavailableException("No se pudo conectar con GitHub.", exception);
        }
        catch (JsonException exception)
        {
            throw new GitHubUnavailableException("GitHub devolvió una respuesta inesperada.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubUnavailableException("GitHub no respondió a tiempo.", exception);
        }
    }

    /// <summary>
    /// When GitHub refuses for its rate limit (403 or 429 with no remaining calls, or a Retry-After), the moment it
    /// can be tried again. Without a token the limit is 60 calls an hour per address, and one refresh spends about
    /// fifteen, so insisting every couple of minutes would keep the quota at zero for the whole hour.
    /// </summary>
    private static DateTimeOffset? RetryAt(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)) return null;
        if (response.Headers.RetryAfter?.Delta is { } delta) return DateTimeOffset.UtcNow + delta;
        if (response.Headers.RetryAfter?.Date is { } date) return date;
        if (Header(response, "X-RateLimit-Remaining") == "0"
            && long.TryParse(Header(response, "X-RateLimit-Reset"), out var reset))
            return DateTimeOffset.FromUnixTimeSeconds(reset).AddSeconds(5);
        return null;
    }

    private static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record UserResponse(string Login, string? Name, string AvatarUrl, string HtmlUrl, string? Bio,
        int PublicRepos, int Followers);

    private sealed record RepositoryResponse(string Name, string? Description, string HtmlUrl, string? Homepage,
        string? Language, int StargazersCount, int ForksCount, bool Fork, bool Archived, DateTimeOffset? PushedAt,
        DateTimeOffset UpdatedAt, string[]? Topics);

    private sealed record CommitResponse(string Sha, string HtmlUrl, CommitDetails Commit);

    private sealed record CommitDetails(string Message, CommitSignature? Author, CommitSignature? Committer);

    private sealed record CommitSignature(DateTimeOffset Date);
}
