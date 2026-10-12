using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Trust X-Forwarded-For/Proto from the immediate proxy. Enable it only when the API is reachable exclusively
    /// through that proxy (the Docker setup publishes it on loopback and to the Nginx container).
    /// </summary>
    public bool Enabled { get; set; }
}

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReverseProxyOptions>().Bind(configuration.GetSection(ReverseProxyOptions.SectionName));
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            // The proxy address changes with every container; the API is not published outside the proxy.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return services;
    }

    public static IApplicationBuilder UseReverseProxyHeaders(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
        return options.Enabled ? app.UseForwardedHeaders() : app;
    }
}
