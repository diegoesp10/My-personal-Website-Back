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

        var result = await service.SaveAsync(repository.Current, ContentLanguage.Spanish, CancellationToken.None);

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
        await service.SaveAsync(stale, ContentLanguage.Spanish, CancellationToken.None);

        var result = await service.SaveAsync(stale, ContentLanguage.Spanish, CancellationToken.None);

        Assert.Equal(FailureKind.Conflict, result.Failure);
    }

    [Fact]
    public async Task Does_not_store_invalid_content()
    {
        var repository = new InMemoryPortfolioRepository(PortfolioValidatorTests.LoadSeed());
        var service = new PortfolioService(repository, new PortfolioValidator());
        var invalid = repository.Current with { Profile = repository.Current.Profile with { Name = "" } };

        var result = await service.SaveAsync(invalid, ContentLanguage.Spanish, CancellationToken.None);

        Assert.Equal(FailureKind.Validation, result.Failure);
        Assert.Equal("initial", repository.Current.Revision);
    }

    [Fact]
    public async Task Each_language_is_saved_and_versioned_on_its_own()
    {
        var repository = new InMemoryPortfolioRepository(PortfolioValidatorTests.LoadSeed());
        var service = new PortfolioService(repository, new PortfolioValidator());
        var english = PortfolioValidatorTests.LoadSeed(ContentLanguage.English);

        var result = await service.SaveAsync(english, ContentLanguage.English, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await service.GetAsync(ContentLanguage.English, CancellationToken.None);
        Assert.Equal(english.Profile.Intro, saved.Profile.Intro);
        Assert.Equal("initial", repository.Current.Revision);
    }

    [Fact]
    public async Task Rejects_an_unsupported_language()
    {
        var repository = new InMemoryPortfolioRepository(PortfolioValidatorTests.LoadSeed());
        var service = new PortfolioService(repository, new PortfolioValidator());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync("fr", CancellationToken.None));
    }

    private sealed class InMemoryPortfolioRepository(PortfolioData spanish) : IPortfolioRepository
    {
        private readonly Dictionary<string, PortfolioData> _contents = new()
        {
            [ContentLanguage.Spanish] = spanish,
            [ContentLanguage.English] = spanish,
        };

        public PortfolioData Current => _contents[ContentLanguage.Spanish];

        public Task<PortfolioData> GetAsync(string language, CancellationToken cancellationToken)
            => Task.FromResult(_contents[language]);

        public Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, string language,
            CancellationToken cancellationToken)
        {
            if (_contents[language].Revision != expectedRevision) return Task.FromResult(false);
            _contents[language] = portfolio;
            return Task.FromResult(true);
        }
    }
}
