using Microsoft.Extensions.Logging;
using Portfolio.Errors.Registry;

namespace Portfolio.Application.Errors;

public sealed class ErrorReporter(IErrorRegistry registry, ILogger<ErrorReporter> logger) : IErrorReporter
{
    public async Task ReportAsync(ErrorRecord entry)
    {
        try { await registry.AppendAsync(entry, CancellationToken.None); }
        catch (Exception exception)
        {
            // Do not expose filesystem paths, exception messages or client data in fallback logs.
            logger.LogError("Error history unavailable: {ExceptionType}; correlation {TraceId}",
                exception.GetType().FullName, entry.TraceId);
        }
    }
}
