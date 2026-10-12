namespace Portfolio.Domain.Portfolios;

public sealed record Project
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string Details { get; init; } = "";
    public string Year { get; init; } = "";
    public string Status { get; init; } = "";
    public string Url { get; init; } = "";
    public string Image { get; init; } = "";
    public string[] Tags { get; init; } = [];
    public string Art { get; init; } = "orange";
    public bool Featured { get; init; }
    public string[] Highlights { get; init; } = [];
    public ProjectLink[] Links { get; init; } = [];
}
