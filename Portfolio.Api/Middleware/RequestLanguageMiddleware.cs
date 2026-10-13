using System.Text.Json;
using Portfolio.Api.Features.Errors;
using Portfolio.Domain.Portfolios;
using Portfolio.Errors.Catalog;

namespace Portfolio.Api.Middleware;

public sealed class RequestLanguageMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestLanguage language, ApiErrorFactory errors)
    {
        if (!context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/uploads")) { await next(context); return; }
        var request = context.Request;
        string? selected = null;
        var invalid = false;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        {
            var values = request.Query["language"];
            invalid = values.Count > 1;
            selected = values.Count == 1 ? values[0] : null;
        }
        else if (request.HasJsonContentType() && request.ContentLength != 0)
        {
            request.EnableBuffering();
            try
            {
                using var json = await JsonDocument.ParseAsync(request.Body, cancellationToken: context.RequestAborted);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var values = json.RootElement.EnumerateObject().Where(property => property.NameEquals("language")).ToArray();
                    invalid = values.Length > 1 || values.Length == 1 && values[0].Value.ValueKind != JsonValueKind.String;
                    selected = values.Length == 1 && !invalid ? values[0].Value.GetString() : null;
                }
            }
            catch (JsonException) { /* MVC owns malformed JSON responses. */ }
            finally { request.Body.Position = 0; }
        }
        else if (request.HasFormContentType)
        {
            var values = (await request.ReadFormAsync(context.RequestAborted))["language"];
            invalid = values.Count > 1;
            selected = values.Count == 1 ? values[0] : null;
        }
        if (invalid || selected is not null && !ContentLanguage.IsSupported(selected))
        {
            await errors.WriteAsync(context, 400, ErrorCatalog.Get("request.language"));
            return;
        }
        language.Value = selected ?? ContentLanguage.Default;
        await next(context);
    }
}
