using Portfolio.Application.Common;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

public interface IPortfolioService
{
    Task<PortfolioData> GetAsync(string language, CancellationToken cancellationToken);
    Task<OperationResult<PortfolioData>> SaveAsync(PortfolioData? portfolio, string language, CancellationToken cancellationToken);
}
