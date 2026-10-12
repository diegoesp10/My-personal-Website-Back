using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

public interface IPortfolioValidator
{
    string? Validate(PortfolioData? portfolio);
}
