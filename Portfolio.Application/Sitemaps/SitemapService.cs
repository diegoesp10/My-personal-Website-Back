using System.Globalization;
using Portfolio.Application.Portfolios;
using Portfolio.Application.Seo;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Sitemaps;

/// <summary>Every public page in every language it exists in, linked to its translations.</summary>
public sealed class SitemapService(IPortfolioRepository repository) : ISitemapService
{
    // Public routes of the React frontend, without the language prefix. Keep in sync with its router and SeoService.
    private static readonly string[] Pages =
    [
        "/", "/proyectos", "/experiencia", "/github", "/visitas", "/sobre-mi", "/diario", "/contacto",
        "/legal/aviso-legal", "/legal/privacidad", "/legal/cookies", "/legal/terminos",
    ];

    public async Task<IReadOnlyList<SitemapEntry>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        var contents = new Dictionary<string, PortfolioData>(StringComparer.Ordinal);
        foreach (var language in ContentLanguage.All)
            contents[language] = await repository.GetAsync(language, cancellationToken);

        var entries = new List<SitemapEntry>();
        foreach (var page in Pages) Add(entries, page, ContentLanguage.All, _ => null);

        // Projects and entries keep their identifier in every language; one missing in a language is left out there.
        foreach (var id in contents.Values.SelectMany(content => content.Projects).Select(project => project.Id).Distinct())
            Add(entries, "/proyectos/" + Uri.EscapeDataString(id),
                [.. ContentLanguage.All.Where(language => contents[language].Projects.Any(project => project.Id == id))], _ => null);
        foreach (var id in contents.Values.SelectMany(content => content.Entries).Select(entry => entry.Id).Distinct())
            Add(entries, "/diario/" + Uri.EscapeDataString(id),
                [.. ContentLanguage.All.Where(language => contents[language].Entries.Any(entry => entry.Id == id))],
                language => ParseDate(contents[language].Entries.First(entry => entry.Id == id).Date));
        return entries;
    }

    private static void Add(List<SitemapEntry> entries, string route, IReadOnlyList<string> languages,
        Func<string, DateOnly?> lastModified)
    {
        var alternates = languages.Select(language => new SitemapAlternate(language, SiteRoutes.Localize(language, route))).ToList();
        var fallback = languages.Contains(ContentLanguage.Default) ? ContentLanguage.Default : languages[0];
        alternates.Add(new SitemapAlternate("x-default", SiteRoutes.Localize(fallback, route)));
        entries.AddRange(languages.Select(language =>
            new SitemapEntry(SiteRoutes.Localize(language, route), lastModified(language), alternates)));
    }

    private static DateOnly? ParseDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : null;
}
