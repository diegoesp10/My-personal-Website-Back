namespace Portfolio.Domain.Portfolios;

public sealed record PortfolioData
{
    public string Revision { get; init; } = "";
    public Profile Profile { get; init; } = new();
    public Project[] Projects { get; init; } = [];
    public Experience[] Experience { get; init; } = [];
    public Education[] Education { get; init; } = [];
    public Entry[] Entries { get; init; } = [];
    public Skill[] Skills { get; init; } = [];
    public Metric[] Metrics { get; init; } = [];
}
