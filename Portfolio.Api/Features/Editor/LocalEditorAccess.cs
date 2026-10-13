using System.Net;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Features.Editor;

public sealed class LocalEditorAccess(IWebHostEnvironment environment, IHttpContextAccessor accessor,
    IOptions<LocalEditorOptions> options) : ILocalEditorAccess
{
    public bool IsAvailable()
    {
        var context = accessor.HttpContext;
        return environment.IsDevelopment() && context?.Connection.RemoteIpAddress is { } address
            && IPAddress.IsLoopback(address)
            && options.Value.AllowedHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase);
    }

    public bool CanAccessProtectedEndpoints()
    {
        if (!IsAvailable()) return false;
        var request = accessor.HttpContext!.Request;
        return request.Headers["X-Portfolio-Editor"] == "local"
            && (!request.Headers.ContainsKey("Origin")
                || options.Value.AllowedOrigins.Contains(request.Headers.Origin.ToString(), StringComparer.Ordinal));
    }
}
