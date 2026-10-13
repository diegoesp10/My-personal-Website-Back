using Portfolio.Errors.Registry;

namespace Portfolio.Api.Features.Errors;

public sealed class HttpErrorCorrelation(IHttpContextAccessor accessor) : IErrorCorrelation
{
    public string? CurrentTraceId => accessor.HttpContext?.TraceIdentifier;
}
