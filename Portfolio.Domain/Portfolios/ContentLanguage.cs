namespace Portfolio.Domain.Portfolios;

/// <summary>
/// Languages the site is written in. Each one has its own complete copy of the content, with the same identifiers,
/// so a page can switch language and keep its address. Spanish is the original and the default.
/// </summary>
public static class ContentLanguage
{
    public const string Spanish = "es";
    public const string English = "en";
    public const string Default = Spanish;

    public static IReadOnlyList<string> All { get; } = [Spanish, English];

    public static bool IsSupported(string? code) => code is Spanish or English;
}
