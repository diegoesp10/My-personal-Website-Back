using Microsoft.AspNetCore.Mvc;

namespace Portfolio.Api.Features.Health;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", runtime = ".NET 10" });
}
