using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Portfolio.Api.Configuration;
using Portfolio.Application.Seo;

namespace Portfolio.Api.Features.Seo;

[ApiController]
[Route("api/seo")]
public sealed class SeoController(ISeoService service, IOptions<SiteOptions> options) : ControllerBase
{
    /// <summary>Metadata of a frontend route, applied by the frontend when navigating between pages.</summary>
    [HttpGet("page")]
    public async Task<PageMetadata> GetPage([FromQuery] string? path, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache";
        return await service.GetPageAsync(path ?? "/", Site, cancellationToken);
    }

    /// <summary>
    /// The same metadata as HTML for the &lt;head&gt; of index.html. Nginx includes it (SSI) in every page it serves,
    /// so search engines and link previews read it without running JavaScript.
    /// </summary>
    [HttpGet("head")]
    public async Task<ContentResult> GetHead([FromQuery] string? path, CancellationToken cancellationToken)
    {
        var page = await service.GetPageAsync(path ?? "/", Site, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Content(SeoHeadRenderer.Render(page), "text/html; charset=utf-8");
    }

    private SiteAddress Site => new(options.Value.BaseUrl, options.Value.DefaultImage);
}
