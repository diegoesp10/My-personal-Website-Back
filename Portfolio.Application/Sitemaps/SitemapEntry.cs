namespace Portfolio.Application.Sitemaps;

/// <summary>
/// Public page of the site in one language, relative to its root, with the same page in every language it exists in
/// (including itself) and "x-default".
/// </summary>
public sealed record SitemapEntry(string Path, DateOnly? LastModified, IReadOnlyList<SitemapAlternate> Alternates);

/// <summary>Address of the page in a language, relative to the site root.</summary>
public sealed record SitemapAlternate(string Language, string Path);
