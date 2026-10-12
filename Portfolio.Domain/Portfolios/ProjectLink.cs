namespace Portfolio.Domain.Portfolios;

public sealed record ProjectLink
{
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
}
