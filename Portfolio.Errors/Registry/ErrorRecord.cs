namespace Portfolio.Errors.Registry;

/// <summary>No raw path, query, body, headers, IP, exception message or stack trace is stored.</summary>
public sealed record ErrorRecord(string Id, DateTimeOffset OccurredAt, string Code, int? Status,
    string TraceId, string Method, string Endpoint, string? ExceptionType, string Source = "http");

public interface IErrorRegistry
{
    Task AppendAsync(ErrorRecord entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<ErrorRecord>> ReadAsync(int limit, CancellationToken cancellationToken);
}
