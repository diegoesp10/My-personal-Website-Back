namespace Portfolio.Domain.Portfolios;

public sealed record Skill
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string[] Tags { get; init; } = [];
}
