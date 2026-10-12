using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Portfolio.Api.Contracts;
using Portfolio.Api.Features.Editor;
using Portfolio.Application.Common;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Api.Features.Portfolios;

[ApiController]
[Route("api/portfolio")]
public sealed class PortfolioController(IPortfolioService service) : ControllerBase
{
    /// <summary>Public content. The revision is published as ETag so repeated visits can answer 304 Not Modified.</summary>
    [HttpGet]
    public async Task<ActionResult<PortfolioData>> Get(CancellationToken cancellationToken)
    {
        var portfolio = await service.GetAsync(cancellationToken);
        if (!EntityTagHeaderValue.TryParse($"\"{portfolio.Revision}\"", out var entityTag)) return Ok(portfolio);

        Response.Headers.CacheControl = "no-cache";
        Response.GetTypedHeaders().ETag = entityTag;
        var requested = Request.GetTypedHeaders().IfNoneMatch;
        return requested.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(entityTag, useStrongComparison: false))
            ? StatusCode(StatusCodes.Status304NotModified)
            : Ok(portfolio);
    }

    [HttpPut]
    [LocalEditorWrite]
    public async Task<ActionResult<PortfolioData>> Save([FromBody] PortfolioData? portfolio, CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(portfolio, cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        var error = new ApiError(result.Error!);
        return result.Failure == FailureKind.Conflict ? Conflict(error) : BadRequest(error);
    }
}
