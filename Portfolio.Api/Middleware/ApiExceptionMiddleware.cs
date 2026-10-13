using Microsoft.AspNetCore.Mvc.Controllers;
using Portfolio.Api.Features.Errors;
using Portfolio.Errors.Contracts;
using Portfolio.Errors.Registry;

namespace Portfolio.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ApiErrorFactory errors, IErrorReporter reporter)
    {
        string? exceptionType = null;
        var disconnected = false;
        try
        {
            await next(context);
            // Includes unknown API routes and framework responses (for example 405).
            if (context.Response.StatusCode >= 400 && !context.Response.HasStarted && IsApi(context))
                await errors.WriteAsync(context, context.Response.StatusCode);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            disconnected = true;
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            exceptionType = exception.GetType().FullName;
            await errors.WriteAsync(context, exception.StatusCode);
        }
        catch (InvalidDataException exception) when (context.Request.HasFormContentType && !context.Response.HasStarted)
        {
            exceptionType = exception.GetType().FullName;
            await errors.WriteAsync(context, 400);
        }
        catch (Exception exception)
        {
            exceptionType = exception.GetType().FullName;
            // Do not put exception messages, bodies or headers in the persistent history or public response.
            logger.LogError("Request failed: {ExceptionType}; correlation {TraceId}", exceptionType, context.TraceIdentifier);
            if (!context.Response.HasStarted) await errors.WriteAsync(context, 500);
            else context.Abort();
        }
        finally
        {
            if (!disconnected && IsApi(context) && (context.Response.StatusCode >= 400 || exceptionType is not null))
            {
                var problem = context.Items[typeof(ApiProblem)] as ApiProblem;
                var status = exceptionType is not null && context.Response.StatusCode < 400 ? 500 : context.Response.StatusCode;
                var route = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.AttributeRouteInfo?.Template;
                var entry = new ErrorRecord(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow,
                    problem?.Code ?? Portfolio.Errors.Catalog.ErrorCatalog.ForStatus(status).Code, status,
                    context.TraceIdentifier, KnownMethod(context.Request.Method), route ?? "api/*", exceptionType);
                await reporter.ReportAsync(entry);
            }
        }
    }

    private static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments("/api")
        || context.Request.Path.StartsWithSegments("/uploads");
    private static string KnownMethod(string method) => method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE" or "OPTIONS"
        ? method : "OTHER";
}
