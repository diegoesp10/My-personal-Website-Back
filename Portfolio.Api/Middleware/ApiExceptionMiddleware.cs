using Portfolio.Api.Contracts;

namespace Portfolio.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; no response can be delivered.
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            var message = exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "La solicitud supera el tamaño permitido."
                : "La solicitud no es válida.";
            await WriteErrorAsync(context, exception.StatusCode, message);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "Error al procesar {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "No se pudo completar la operación.");
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return context.Response.WriteAsJsonAsync(new ApiError(message));
    }
}
