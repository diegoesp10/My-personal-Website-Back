using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Api.Features.Editor;

[ApiController]
[Route("api/editor")]
public sealed class EditorController(ILocalEditorAccess access) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { enabled = access.IsAvailable() });
}
