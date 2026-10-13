namespace Portfolio.Api.Configuration;

public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>Public origin used to build absolute links, such as those in the sitemap.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Image shown when a page is shared (1200×630), relative to <see cref="BaseUrl"/>. Served by the frontend.</summary>
    public string DefaultImage { get; set; } = "/og-image.png";
}
