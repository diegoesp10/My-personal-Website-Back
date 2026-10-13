namespace Portfolio.Api.Contracts.Requests;

/// <summary>Optional page view from navigator.sendBeacon; language is never passed to the visit tracker.</summary>
public sealed record VisitBeacon : LocalizedRequest
{
    public string? Path { get; init; }
    public string? Referrer { get; init; }
}
