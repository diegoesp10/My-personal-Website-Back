using System.Text.Json.Nodes;

namespace Portfolio.Application.Seo;

/// <summary>
/// Search and social metadata of a public page of the site. Links are absolute. <see cref="Url"/> (the canonical
/// address) is null for pages that must not be indexed, such as unknown routes.
/// </summary>
public sealed record PageMetadata(
    string Title,
    string Description,
    string? Url,
    string Image,
    string ImageAlt,
    string Type,
    bool Indexable,
    string SiteName,
    DateOnly? Published,
    JsonObject? StructuredData);

/// <summary>Public address of the site and its default sharing image, relative to that address.</summary>
public sealed record SiteAddress(string BaseUrl, string DefaultImage);
