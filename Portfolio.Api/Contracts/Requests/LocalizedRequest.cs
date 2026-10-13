namespace Portfolio.Api.Contracts.Requests;

/// <summary>Transport metadata validated centrally before model binding. Not a domain property.</summary>
public abstract record LocalizedRequest
{
    public string Language { get; init; } = "es";
}
