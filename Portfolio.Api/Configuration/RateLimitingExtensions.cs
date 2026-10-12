using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Portfolio.Api.Contracts;

namespace Portfolio.Api.Configuration;

public sealed class ApiRateLimitOptions
{
    public const string SectionName = "RateLimiting";
    public int PermitLimit { get; set; } = 300;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public static class RateLimitingExtensions
{
    /// <summary>Fixed window per client IP for every request that reaches the endpoints. Static files are served before it.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiRateLimitOptions>()
            .Bind(configuration.GetSection(ApiRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit > 0 && options.Window > TimeSpan.Zero, "Revisa los límites de peticiones.")
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ApiRateLimitOptions>>((limiter, settings) =>
        {
            var limits = settings.Value;
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = limits.Window,
                        QueueLimit = 0
                    }));
            limiter.OnRejected = (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                response.Headers.CacheControl = "no-store";
                return new ValueTask(response.WriteAsJsonAsync(
                    new ApiError("Demasiadas solicitudes. Inténtalo de nuevo en un momento."), cancellationToken));
            };
        });
        return services;
    }
}
