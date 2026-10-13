namespace Portfolio.Errors.Registry;

/// <summary>Records failures without letting a broken history replace the original outcome.</summary>
public interface IErrorReporter
{
    Task ReportAsync(ErrorRecord entry);
}

public interface IErrorCorrelation
{
    string? CurrentTraceId { get; }
}
