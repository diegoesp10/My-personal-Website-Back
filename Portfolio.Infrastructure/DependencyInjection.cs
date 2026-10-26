using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Portfolio.Application.Cv;
using Portfolio.Application.GitHub;
using Portfolio.Application.Images;
using Portfolio.Application.Portfolios;
using Portfolio.Application.Visitors;
using Portfolio.Infrastructure.Cv;
using Portfolio.Infrastructure.GitHub;
using Portfolio.Infrastructure.Errors;
using Portfolio.Infrastructure.Images;
using Portfolio.Infrastructure.Portfolios;
using Portfolio.Infrastructure.Storage;
using Portfolio.Infrastructure.Visitors;

namespace Portfolio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        IConfiguration configuration, string defaultDataPath, string seedPath, string contentRootPath)
    {
        services.AddOptions<FileStorageOptions>()
            .Configure(options => { options.DataPath = defaultDataPath; options.SeedPath = seedPath; })
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            // Like App_Data, a relative content folder (../content in the local profile) is relative to the project.
            .PostConfigure(options =>
            {
                if (!string.IsNullOrWhiteSpace(options.ContentPath) && !Path.IsPathRooted(options.ContentPath))
                    options.ContentPath = Path.GetFullPath(Path.Combine(contentRootPath, options.ContentPath));
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.DataPath) && !string.IsNullOrWhiteSpace(options.SeedPath),
                "Deben configurarse las rutas del contenido y del archivo inicial.")
            .ValidateOnStart();

        services.AddSingleton<FileStoragePaths>();
        services.AddSingleton<IPortfolioRepository, JsonPortfolioRepository>();
        services.AddSingleton<IImageRepository, FileImageRepository>();
        services.AddSingleton<ICvRepository, FileCvRepository>();
        services.AddHostedService<FileStorageInitializer>();
        services.AddGitHub(configuration);
        services.AddVisitors(configuration);
        services.AddErrorPersistence(configuration);
        return services;
    }

    private static void AddVisitors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeoDatabaseOptions>().Bind(configuration.GetSection(GeoDatabaseOptions.SectionName));
        services.AddSingleton<IVisitStore, JsonVisitStore>();
        services.AddSingleton<IGeoLocator, MmdbGeoLocator>();
        services.AddHostedService<VisitFlushService>();
    }

    private static void AddGitHub(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GitHubClientOptions>()
            .Bind(configuration.GetSection(GitHubClientOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                    && options.Timeout > TimeSpan.Zero,
                "Revisa la dirección y el tiempo máximo del cliente de GitHub.")
            .ValidateOnStart();

        services.AddHttpClient(GitHubApiSource.ClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GitHubClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("diegoespinarodriguez-portfolio", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(options.Token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        });
        services.AddSingleton<IGitHubSource, GitHubApiSource>();
    }
}
