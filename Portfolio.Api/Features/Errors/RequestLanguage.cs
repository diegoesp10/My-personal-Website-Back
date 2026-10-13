namespace Portfolio.Api.Features.Errors;

/// <summary>Per-request transport metadata; never persisted in portfolio content.</summary>
public sealed class RequestLanguage
{
    public string Value { get; set; } = "es";
}
