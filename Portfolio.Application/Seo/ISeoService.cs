namespace Portfolio.Application.Seo;

public interface ISeoService
{
    /// <summary>Metadata of the frontend route <paramref name="path"/> (query and trailing slash are ignored).</summary>
    Task<PageMetadata> GetPageAsync(string path, SiteAddress site, CancellationToken cancellationToken);
}
