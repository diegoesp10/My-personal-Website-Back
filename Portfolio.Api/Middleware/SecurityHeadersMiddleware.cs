namespace Portfolio.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // Same policy as the Nginx configuration of the frontend, for the integrated local preview.
    private const string PagePolicy = "default-src 'self'; img-src 'self' data: https://avatars.githubusercontent.com; "
        + "style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self'; connect-src 'self'; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        var isApi = context.Request.Path.StartsWithSegments("/api");
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers.ContentSecurityPolicy = isApi ? ApiPolicy : PagePolicy;
        if (isApi) headers.CacheControl = "no-store";
        return next(context);
    }
}
