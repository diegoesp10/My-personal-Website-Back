using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portfolio.Api.Contracts;
using Portfolio.Errors.Catalog;
using Portfolio.Errors.Contracts;

namespace Portfolio.Api.Features.Errors;

/// <summary>Normalises model binding, access checks, and controller failures without changing success data.</summary>
public sealed class ApiResponseResultFilter(ApiErrorFactory errors, RequestLanguage language, IOptions<JsonOptions> json) : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var status = context.Result switch
        {
            ObjectResult result => result.StatusCode ?? 200,
            StatusCodeResult result => result.StatusCode,
            _ => 200
        };
        if (status >= 400 && context.Result is not ObjectResult { Value: ApiProblem })
        {
            var definition = context.Result is ObjectResult { Value: ApiError legacy }
                ? ErrorCatalog.FromMessage(legacy.Error, status) : ErrorCatalog.ForStatus(status);
            var result = new ObjectResult(errors.Create(context.HttpContext, status, definition)) { StatusCode = status };
            result.ContentTypes.Add("application/problem+json");
            context.Result = result;
        }
        if (status < 400 && context.Result is ObjectResult { Value: not null } success
            && JsonSerializer.SerializeToNode(success.Value, json.Value.JsonSerializerOptions) is JsonObject body)
        {
            body["language"] = language.Value;
            body["message"] = ResponseMessages.Success(context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value ?? "", language.Value);
            success.Value = body;
            success.DeclaredType = typeof(JsonObject);
        }
        await next();
    }
}
