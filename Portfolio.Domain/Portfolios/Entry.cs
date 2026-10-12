namespace Portfolio.Domain.Portfolios;

public sealed record Entry
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Category { get; init; } = "";
    public string Text { get; init; } = "";
}
