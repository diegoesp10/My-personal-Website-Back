using Portfolio.Errors.Registry;

namespace Portfolio.Api.Features.Errors;

public static class ErrorHandlingServiceCollectionExtensions
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddScoped<RequestLanguage>();
        services.AddScoped<ApiErrorFactory>();
        services.AddScoped<ApiResponseResultFilter>();
        services.AddSingleton<IErrorCorrelation, HttpErrorCorrelation>();
        return services;
    }
}
