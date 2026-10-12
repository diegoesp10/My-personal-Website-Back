using Microsoft.AspNetCore.Mvc;
using Portfolio.Application.GitHub;
using Portfolio.Domain.GitHub;

namespace Portfolio.Api.Features.GitHub;

[ApiController]
[Route("api/github")]
public sealed class GitHubController(IGitHubActivityService service) : ControllerBase
{
    /// <summary>Public repositories, languages and recent commits. Always 200: <c>available</c> is false when GitHub cannot be read.</summary>
    [HttpGet]
    public async Task<ActionResult<GitHubActivity>> Get(CancellationToken cancellationToken)
    {
        var activity = await service.GetAsync(cancellationToken);
        Response.Headers.CacheControl = activity.Available ? "public, max-age=300" : "no-store";
        return Ok(activity);
    }
}
