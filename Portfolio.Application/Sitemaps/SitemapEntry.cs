namespace Portfolio.Application.Sitemaps;

/// <summary>Public page of the site, relative to its root.</summary>
public sealed record SitemapEntry(string Path, DateOnly? LastModified);
