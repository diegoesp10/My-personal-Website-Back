namespace Portfolio.Api.Configuration;

public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>Public origin used to build absolute links, such as those in the sitemap.</summary>
    public string BaseUrl { get; set; } = "";
}
