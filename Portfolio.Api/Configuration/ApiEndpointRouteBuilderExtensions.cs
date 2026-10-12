namespace Portfolio.Api.Configuration;

public static class ApiEndpointRouteBuilderExtensions
{
    public static void MapApiRoutes(this IEndpointRouteBuilder endpoints, IWebHostEnvironment environment)
    {
        endpoints.MapControllers();
        // The API description is a development aid; Production does not publish it.
        if (environment.IsDevelopment()) endpoints.MapOpenApi("/api/openapi/{documentName}.json");
        endpoints.MapFallback("/api/{**path}", NotFound);
        endpoints.MapFallback("/uploads/{**path}", NotFound);
        endpoints.MapFallbackToFile("index.html");
    }

    private static Task NotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
