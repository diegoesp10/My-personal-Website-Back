using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

public interface IPortfolioRepository
{
    Task<PortfolioData> GetAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, CancellationToken cancellationToken);
}
