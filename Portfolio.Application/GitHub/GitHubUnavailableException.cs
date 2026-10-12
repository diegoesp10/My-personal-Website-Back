namespace Portfolio.Application.GitHub;

public sealed class GitHubUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
