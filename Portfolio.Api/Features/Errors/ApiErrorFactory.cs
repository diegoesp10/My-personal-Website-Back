using Portfolio.Errors.Catalog;
using Portfolio.Errors.Contracts;

namespace Portfolio.Api.Features.Errors;

public sealed class ApiErrorFactory(RequestLanguage language)
{
    public ApiProblem Create(HttpContext context, int status, ErrorDefinition? definition = null)
    {
        var error = definition ?? ErrorCatalog.ForStatus(status);
        var problem = new ApiProblem("about:blank", ErrorCatalog.Title(status, language.Value), status,
            error.Message(language.Value), error.Code, context.TraceIdentifier, language.Value);
        context.Items[typeof(ApiProblem)] = problem;
        context.Response.Headers.CacheControl = "no-store";
        return problem;
    }

    public Task WriteAsync(HttpContext context, int status, ErrorDefinition? definition = null)
    {
        var problem = Create(context, status, definition);
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
