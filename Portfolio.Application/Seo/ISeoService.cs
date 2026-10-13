namespace Portfolio.Application.Seo;

public interface ISeoService
{
    /// <summary>
    /// Metadata of the frontend route <paramref name="path"/>, in the language of its prefix (/en/…; none means Spanish).
    /// Query and trailing slash are ignored.
    /// </summary>
    Task<PageMetadata> GetPageAsync(string path, SiteAddress site, CancellationToken cancellationToken);
}
