namespace Portfolio.Infrastructure.Errors;

public sealed class ErrorRegistryOptions
{
    public const string SectionName = "ErrorRegistry";
    public int MaximumEntries { get; set; } = 1000;
    public int RetentionDays { get; set; } = 14;
}
