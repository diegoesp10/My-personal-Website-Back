using Portfolio.Application.Common;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

public interface IPortfolioService
{
    Task<PortfolioData> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult<PortfolioData>> SaveAsync(PortfolioData? portfolio, CancellationToken cancellationToken);
}
