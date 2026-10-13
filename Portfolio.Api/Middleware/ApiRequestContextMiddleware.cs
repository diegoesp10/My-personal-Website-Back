using Portfolio.Api.Features.Errors;

namespace Portfolio.Api.Middleware;

/// <summary>Prepares error/correlation metadata without reading the body, before rate limiting.</summary>
public sealed class ApiRequestContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, RequestLanguage language, ApiErrorFactory errors)
    {
        // The global limiter also covers sitemap and fallback endpoints.
        context.Features.Set(new ApiErrorFeature(errors));
        if ((context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/uploads"))
            && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            var selected = context.Request.Query["language"];
            if (selected.Count == 1 && selected[0] is "es" or "en") language.Value = selected[0]!;
        }
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.ContentLanguage = language.Value;
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
