using System.Globalization;
using Portfolio.Application.Portfolios;

namespace Portfolio.Application.Sitemaps;

public sealed class SitemapService(IPortfolioRepository repository) : ISitemapService
{
    // Public routes of the React frontend. Keep in sync with the router in the frontend repository.
    private static readonly string[] Pages =
        ["/", "/proyectos", "/experiencia", "/github", "/sobre-mi", "/diario", "/contacto"];

    public async Task<IReadOnlyList<SitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        var portfolio = await repository.GetAsync(cancellationToken);
        return [
            .. Pages.Select(path => new SitemapEntry(path, null)),
            .. portfolio.Projects.Select(project => new SitemapEntry("/proyectos/" + Uri.EscapeDataString(project.Id), null)),
            .. portfolio.Entries.Select(entry => new SitemapEntry("/diario/" + Uri.EscapeDataString(entry.Id), ParseDate(entry.Date)))
        ];
    }

    private static DateOnly? ParseDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : null;
}
