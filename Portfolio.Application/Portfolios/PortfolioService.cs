using Portfolio.Application.Common;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

public sealed class PortfolioService(IPortfolioRepository repository, IPortfolioValidator validator) : IPortfolioService
{
    public Task<PortfolioData> GetAsync(CancellationToken cancellationToken) => repository.GetAsync(cancellationToken);

    public async Task<OperationResult<PortfolioData>> SaveAsync(PortfolioData? portfolio, CancellationToken cancellationToken)
    {
        var error = validator.Validate(portfolio);
        if (error is not null) return OperationResult<PortfolioData>.Invalid(error);

        var updated = portfolio! with { Revision = Guid.NewGuid().ToString("N") };
        var saved = await repository.TrySaveAsync(updated, portfolio!.Revision, cancellationToken);

        return saved
            ? OperationResult<PortfolioData>.Success(updated)
            : OperationResult<PortfolioData>.Conflict("El contenido ha cambiado. Recarga el editor antes de guardar.");
    }
}
