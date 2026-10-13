namespace Portfolio.Api.Features.Errors;

/// <summary>
/// Language of the request: selects the copy of the content and the language of the messages. It is transport
/// metadata, never stored inside the content.
/// </summary>
public sealed class RequestLanguage
{
    public string Value { get; set; } = "es";
}
