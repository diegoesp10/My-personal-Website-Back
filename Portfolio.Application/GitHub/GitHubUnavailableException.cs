namespace Portfolio.Application.GitHub;

/// <summary>
/// GitHub could not be read. When GitHub said when it can be tried again (its rate limit reset or a Retry-After),
/// <see cref="RetryAt"/> carries that moment so the service does not insist before.
/// </summary>
public sealed class GitHubUnavailableException(string message, Exception? innerException = null, DateTimeOffset? retryAt = null)
    : Exception(message, innerException)
{
    public DateTimeOffset? RetryAt { get; } = retryAt;
}
