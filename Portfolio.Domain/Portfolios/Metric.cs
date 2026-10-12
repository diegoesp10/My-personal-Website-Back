namespace Portfolio.Domain.Portfolios;

public sealed record Metric
{
    public string Id { get; init; } = "";
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
}
