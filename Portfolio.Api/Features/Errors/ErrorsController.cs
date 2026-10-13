using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Features.Editor;
using Portfolio.Errors.Catalog;
using Portfolio.Errors.Registry;

namespace Portfolio.Api.Features.Errors;

[ApiController]
[Route("api/editor/errors")]
public sealed class ErrorsController(IErrorRegistry registry, RequestLanguage language) : ControllerBase
{
    [HttpGet]
    [LocalEditorOnly] // Private reads require the same local access checks and editor header as writes.
    public async Task<IActionResult> Get([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var entries = await registry.ReadAsync(limit, cancellationToken);
        return Ok(new
        {
            language = language.Value,
            entries = entries.Select(entry => new
            {
                entry.Id, entry.OccurredAt, entry.Code, entry.Status, entry.TraceId,
                entry.Method, entry.Endpoint, entry.ExceptionType, entry.Source,
                message = ErrorCatalog.Get(entry.Code).Message(language.Value)
            })
        });
    }
}
