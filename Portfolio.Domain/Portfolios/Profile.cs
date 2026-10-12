namespace Portfolio.Domain.Portfolios;

public sealed record Profile
{
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string Headline { get; init; } = "";
    public string Intro { get; init; } = "";
    public string Bio { get; init; } = "";
    public string Personal { get; init; } = "";
    public string Location { get; init; } = "";
    public string Availability { get; init; } = "";
    public string Languages { get; init; } = "";
    public string Email { get; init; } = "";
    public string Github { get; init; } = "";
    public string Linkedin { get; init; } = "";
    public string Portrait { get; init; } = "";
}
