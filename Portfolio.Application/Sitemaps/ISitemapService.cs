namespace Portfolio.Application.Sitemaps;

public interface ISitemapService
{
    Task<IReadOnlyList<SitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken);
}
