using Portfolio.Domain.Portfolios;

namespace Portfolio.Api.Contracts.Requests;

/// <summary>Retains the flat JSON contract and explicitly separates language from persisted content.</summary>
public sealed record PortfolioWriteRequest : LocalizedRequest
{
    public string Revision { get; init; } = "";
    public Profile Profile { get; init; } = new();
    public Project[] Projects { get; init; } = [];
    public Experience[] Experience { get; init; } = [];
    public Education[] Education { get; init; } = [];
    public Entry[] Entries { get; init; } = [];
    public Skill[] Skills { get; init; } = [];
    public Metric[] Metrics { get; init; } = [];

    public PortfolioData ToPortfolio() => new()
    {
        Revision = Revision, Profile = Profile, Projects = Projects, Experience = Experience,
        Education = Education, Entries = Entries, Skills = Skills, Metrics = Metrics
    };
}
