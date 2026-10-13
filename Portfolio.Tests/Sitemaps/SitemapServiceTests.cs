using Portfolio.Application.Portfolios;
using Portfolio.Application.Sitemaps;
using Portfolio.Domain.Portfolios;
using Portfolio.Tests.Portfolios;

namespace Portfolio.Tests.Sitemaps;

public sealed class SitemapServiceTests
{
    [Fact]
    public async Task Lists_public_pages_projects_and_entries_in_every_language()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var service = new SitemapService(new FixedRepository(seed, PortfolioValidatorTests.LoadSeed(ContentLanguage.English)));

        var entries = await service.GetEntriesAsync(CancellationToken.None);

        Assert.Contains(entries, entry => entry.Path == "/es" && entry.LastModified is null);
        Assert.Contains(entries, entry => entry.Path == "/en");
        Assert.Contains(entries, entry => entry.Path == "/es/experiencia");
        Assert.Contains(entries, entry => entry.Path == "/en/legal/privacidad");
        Assert.DoesNotContain(entries, entry => entry.Path.EndsWith("/legal") || entry.Path.EndsWith("/error")
            || entry.Path.EndsWith("/editor"));
        Assert.All(seed.Projects, project =>
        {
            Assert.Contains(entries, entry => entry.Path == "/es/proyectos/" + project.Id);
            Assert.Contains(entries, entry => entry.Path == "/en/proyectos/" + project.Id);
        });
        var journal = Assert.Single(entries, entry => entry.Path == "/es/diario/" + seed.Entries[0].Id);
        Assert.Equal(DateOnly.Parse(seed.Entries[0].Date), journal.LastModified);
    }

    [Fact]
    public async Task Links_each_page_to_its_translations()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var service = new SitemapService(new FixedRepository(seed, seed));

        var entries = await service.GetEntriesAsync(CancellationToken.None);

        var english = Assert.Single(entries, entry => entry.Path == "/en/proyectos");
        Assert.Equal([("es", "/es/proyectos"), ("en", "/en/proyectos"), ("x-default", "/es/proyectos")],
            english.Alternates.Select(item => (item.Language, item.Path)));
    }

    [Fact]
    public async Task Leaves_out_a_project_in_the_languages_that_do_not_have_it()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var english = PortfolioValidatorTests.LoadSeed(ContentLanguage.English) with { Projects = [] };

        var entries = await new SitemapService(new FixedRepository(seed, english)).GetEntriesAsync(CancellationToken.None);

        var path = "/proyectos/" + seed.Projects[0].Id;
        var spanish = Assert.Single(entries, entry => entry.Path == "/es" + path);
        Assert.DoesNotContain(entries, entry => entry.Path == "/en" + path);
        Assert.Equal(["es", "x-default"], spanish.Alternates.Select(item => item.Language));
    }

    [Fact]
    public async Task Escapes_identifiers_in_paths()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var portfolio = seed with { Projects = [seed.Projects[0] with { Id = "mi proyecto" }], Entries = [] };

        var entries = await new SitemapService(new FixedRepository(portfolio, portfolio)).GetEntriesAsync(CancellationToken.None);

        Assert.Contains(entries, entry => entry.Path == "/es/proyectos/mi%20proyecto");
    }

    private sealed class FixedRepository(PortfolioData spanish, PortfolioData english) : IPortfolioRepository
    {
        public Task<PortfolioData> GetAsync(string language, CancellationToken cancellationToken)
            => Task.FromResult(language == ContentLanguage.English ? english : spanish);

        public Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, string language,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
