namespace Portfolio.Domain.Portfolios;

public sealed record Education
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Center { get; init; } = "";
    public string Location { get; init; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
}
