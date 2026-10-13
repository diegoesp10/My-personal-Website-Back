using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Portfolio.Api.Contracts;
using Portfolio.Api.Features.Editor;

namespace Portfolio.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = 9 * 1024 * 1024);
        services.AddControllers().ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new ApiError("La solicitud no es válida.")));
        services.AddHttpContextAccessor();
        services.AddOptions<LocalEditorOptions>().Bind(configuration.GetSection(LocalEditorOptions.SectionName));
        services.AddScoped<ILocalEditorAccess, LocalEditorAccess>();
        services.AddScoped<LocalEditorWriteFilter>();

        services.AddOptions<SiteOptions>()
            .Bind(configuration.GetSection(SiteOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
                "Site:BaseUrl debe ser una dirección absoluta.")
            .Validate(options => options.DefaultImage.StartsWith('/') && !options.DefaultImage.StartsWith("//"),
                "Site:DefaultImage debe ser una ruta relativa a la web, como /og-image.png.")
            .ValidateOnStart();
        services.AddApiRateLimiting(configuration);
        services.AddReverseProxySupport(configuration);
        services.AddOpenApi();
        return services;
    }
}
