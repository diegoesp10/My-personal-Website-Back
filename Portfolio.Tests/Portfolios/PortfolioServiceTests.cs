using Portfolio.Application.Common;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Tests.Portfolios;

public sealed class PortfolioServiceTests
{
    [Fact]
    public async Task Saves_with_a_new_revision_when_the_expected_one_matches()
    {
        var repository = new InMemoryPortfolioRepository(PortfolioValidatorTests.LoadSeed());
        var service = new PortfolioService(repository, new PortfolioValidator());

        var result = await service.SaveAsync(repository.Current, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual("initial", result.Value!.Revision);
        Assert.Equal(result.Value.Revision, repository.Current.Revision);
    }

    [Fact]
    public async Task Reports_a_conflict_when_the_content_changed_meanwhile()
    {
        var repository = new InMemoryPortfolioRepository(PortfolioValidatorTests.LoadSeed());
        var service = new PortfolioService(repository, new PortfolioValidator());
        var stale = repository.Current;
        await service.SaveAsync(stale, CancellationToken.None);

        var result = await service.SaveAsync(stale, CancellationToken.None);

        Assert.Equal(FailureKind.Conflict, result.Failure);
    }

    [Fact]
    public async Task Does_not_store_invalid_content()
    {
        var repository = new InMemoryPortfolioRepository(PortfolioValidatorTests.LoadSeed());
        var service = new PortfolioService(repository, new PortfolioValidator());
        var invalid = repository.Current with { Profile = repository.Current.Profile with { Name = "" } };

        var result = await service.SaveAsync(invalid, CancellationToken.None);

        Assert.Equal(FailureKind.Validation, result.Failure);
        Assert.Equal("initial", repository.Current.Revision);
    }

    private sealed class InMemoryPortfolioRepository(PortfolioData initial) : IPortfolioRepository
    {
        public PortfolioData Current { get; private set; } = initial;

        public Task<PortfolioData> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, CancellationToken cancellationToken)
        {
            if (Current.Revision != expectedRevision) return Task.FromResult(false);
            Current = portfolio;
            return Task.FromResult(true);
        }
    }
}
