using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Application.Cv;
using Portfolio.Application.GitHub;
using Portfolio.Application.Errors;
using Portfolio.Errors.Registry;
using Portfolio.Application.Images;
using Portfolio.Application.Portfolios;
using Portfolio.Application.Seo;
using Portfolio.Application.Sitemaps;
using Portfolio.Application.Visitors;

namespace Portfolio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<ICvService, CvService>();
        services.AddScoped<ISitemapService, SitemapService>();
        services.AddScoped<ISeoService, SeoService>();
        services.AddSingleton<IPortfolioValidator, PortfolioValidator>();
        services.AddSingleton<IImageValidator, ImageSignatureValidator>();

        services.AddOptions<GitHubOptions>()
            .Bind(configuration.GetSection(GitHubOptions.SectionName))
            .Validate(options => !options.Enabled || IsValidUser(options.User),
                "GitHub:User debe ser un usuario válido de GitHub.")
            .Validate(options => options.CacheDuration > TimeSpan.Zero && options.StaleDuration >= options.CacheDuration
                    && options.RetryDelay > TimeSpan.Zero,
                "Revisa las duraciones de caché de GitHub.")
            .Validate(options => options.MaxRepositories is > 0 and <= 100 && options.LanguageRepositories is >= 0 and <= 20
                    && options.CommitRepositories is >= 0 and <= 10 && options.CommitsPerRepository is > 0 and <= 20
                    && options.MaxCommits is > 0 and <= 50,
                "Revisa los límites de consulta de GitHub.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IErrorReporter, ErrorReporter>();
        // Singleton: keeps the cached summary and coordinates a single refresh at a time.
        services.AddSingleton<IGitHubActivityService, GitHubActivityService>();

        services.AddOptions<VisitorOptions>()
            .Bind(configuration.GetSection(VisitorOptions.SectionName))
            .Validate(options => options.KeepDays is > 0 and <= 3650 && options.RecentVisits is >= 0 and <= 200
                    && options.MaxPages is > 0 and <= 5000 && options.MaxLocations is > 0 and <= 50_000
                    && options.OnlineWindow > TimeSpan.Zero && options.FlushInterval >= TimeSpan.FromSeconds(1),
                "Revisa los límites de las estadísticas de visitas.")
            .ValidateOnStart();
        // Singleton: keeps today's counters in memory and saves them periodically.
        services.AddSingleton<IVisitTracker, VisitTracker>();
        return services;
    }

    private static bool IsValidUser(string user) => user.Length is > 0 and <= 39
        && user.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
