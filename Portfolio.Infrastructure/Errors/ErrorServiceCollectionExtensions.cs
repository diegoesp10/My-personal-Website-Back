using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Errors.Registry;

namespace Portfolio.Infrastructure.Errors;

public static class ErrorServiceCollectionExtensions
{
    public static IServiceCollection AddErrorPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ErrorRegistryOptions>().Bind(configuration.GetSection(ErrorRegistryOptions.SectionName))
            .Validate(options => options.MaximumEntries is >= 1 and <= 10000 && options.RetentionDays is >= 1 and <= 365,
                "Configure a bounded error history.").ValidateOnStart();
        services.AddSingleton<IErrorRegistry, JsonErrorRegistry>();
        return services;
    }
}
