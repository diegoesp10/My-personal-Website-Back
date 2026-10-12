namespace Portfolio.Domain.Portfolios;

public sealed record Experience
{
    public string Id { get; init; } = "";
    public string Role { get; init; } = "";
    public string Company { get; init; } = "";
    public string CompanyUrl { get; init; } = "";
    /// <summary>Month in yyyy-MM format.</summary>
    public string Start { get; init; } = "";
    /// <summary>Month in yyyy-MM format; empty while the position is current.</summary>
    public string End { get; init; } = "";
    public string Location { get; init; } = "";
    public string Summary { get; init; } = "";
    public string[] Highlights { get; init; } = [];
    public string[] Stack { get; init; } = [];
}
