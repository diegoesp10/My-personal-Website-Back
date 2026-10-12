using Portfolio.Api.Configuration;
using Portfolio.Api.Middleware;
using Portfolio.Application;
using Portfolio.Infrastructure;

namespace Portfolio.Api;

public sealed class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration,
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "App_Data")),
            Path.Combine(AppContext.BaseDirectory, "seed.json"));
        services.AddApi(configuration);
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseReverseProxyHeaders();
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseEndpoints(endpoints => endpoints.MapApiRoutes(environment));
    }
}
