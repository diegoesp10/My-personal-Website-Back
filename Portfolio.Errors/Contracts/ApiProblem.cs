namespace Portfolio.Errors.Contracts;

/// <summary>RFC 9457 problem details with additive application metadata and the legacy error field.</summary>
public sealed record ApiProblem(string Type, string Title, int Status, string Detail,
    string Code, string TraceId, string Language)
{
    public string Error => Detail;
}
