using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Portfolio.Api.Configuration;
using Portfolio.Application.Sitemaps;

namespace Portfolio.Api.Features.Sitemap;

[ApiController]
public sealed class SitemapController(ISitemapService service, IOptions<SiteOptions> options) : ControllerBase
{
    private static readonly XNamespace Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var entries = await service.GetEntriesAsync(cancellationToken);
        var document = new XElement(Namespace + "urlset", entries.Select(entry => new XElement(Namespace + "url",
            new XElement(Namespace + "loc", baseUrl + entry.Path),
            entry.LastModified is { } date
                ? new XElement(Namespace + "lastmod", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                : null)));

        Response.Headers.CacheControl = "public, max-age=3600";
        return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + document, "application/xml; charset=utf-8");
    }
}
