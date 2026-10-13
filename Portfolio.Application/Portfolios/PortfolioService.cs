using Portfolio.Errors.Catalog;
using Portfolio.Application.Common;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

/// <summary>Reads and saves the content of one language. Every language is validated and versioned on its own.</summary>
public sealed class PortfolioService(IPortfolioRepository repository, IPortfolioValidator validator) : IPortfolioService
{
    public Task<PortfolioData> GetAsync(string language, CancellationToken cancellationToken)
        => repository.GetAsync(Supported(language), cancellationToken);

    public async Task<OperationResult<PortfolioData>> SaveAsync(PortfolioData? portfolio, string language, CancellationToken cancellationToken)
    {
        var error = validator.Validate(portfolio);
        if (error is not null) return OperationResult<PortfolioData>.Invalid(error);

        var updated = portfolio! with { Revision = Guid.NewGuid().ToString("N") };
        var saved = await repository.TrySaveAsync(updated, portfolio!.Revision, Supported(language), cancellationToken);

        return saved
            ? OperationResult<PortfolioData>.Success(updated)
            : OperationResult<PortfolioData>.Conflict(ErrorCatalog.Get("portfolio.conflict").Message());
    }

    // The API validates the language of every request; anything else here is a programming error.
    private static string Supported(string language) => ContentLanguage.IsSupported(language)
        ? language
        : throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported content language.");
}
