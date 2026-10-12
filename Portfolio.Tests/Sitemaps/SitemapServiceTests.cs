using Portfolio.Application.Portfolios;
using Portfolio.Application.Sitemaps;
using Portfolio.Domain.Portfolios;
using Portfolio.Tests.Portfolios;

namespace Portfolio.Tests.Sitemaps;

public sealed class SitemapServiceTests
{
    [Fact]
    public async Task Lists_public_pages_projects_and_entries()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var service = new SitemapService(new FixedRepository(seed));

        var entries = await service.GetEntriesAsync(CancellationToken.None);

        Assert.Contains(entries, entry => entry.Path == "/" && entry.LastModified is null);
        Assert.Contains(entries, entry => entry.Path == "/experiencia");
        Assert.All(seed.Projects, project => Assert.Contains(entries, entry => entry.Path == "/proyectos/" + project.Id));
        var journal = Assert.Single(entries, entry => entry.Path == "/diario/" + seed.Entries[0].Id);
        Assert.Equal(DateOnly.Parse(seed.Entries[0].Date), journal.LastModified);
    }

    [Fact]
    public async Task Escapes_identifiers_in_paths()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var portfolio = seed with { Projects = [seed.Projects[0] with { Id = "mi proyecto" }], Entries = [] };

        var entries = await new SitemapService(new FixedRepository(portfolio)).GetEntriesAsync(CancellationToken.None);

        Assert.Contains(entries, entry => entry.Path == "/proyectos/mi%20proyecto");
    }

    private sealed class FixedRepository(PortfolioData portfolio) : IPortfolioRepository
    {
        public Task<PortfolioData> GetAsync(CancellationToken cancellationToken) => Task.FromResult(portfolio);

        public Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
