using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

/// <summary>Content of the site, one independent copy per <see cref="ContentLanguage"/>.</summary>
public interface IPortfolioRepository
{
    Task<PortfolioData> GetAsync(string language, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, string language, CancellationToken cancellationToken);
}
