using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Portfolio.Application.Visitors;
using Portfolio.Api.Contracts.Requests;

namespace Portfolio.Api.Features.Visitors;

[ApiController]
public sealed class VisitsController(IVisitTracker tracker) : ControllerBase
{
    /// <summary>Records a page view. Always 204: ignored views (bots, opt-out, invalid paths) give no signal.</summary>
    [HttpPost("api/visits")]
    public async Task<IActionResult> Record(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VisitBeacon? beacon, CancellationToken cancellationToken)
    {
        if (beacon?.Path is { } path)
        {
            var headers = Request.Headers;
            var optedOut = headers["DNT"] == "1" || headers["Sec-GPC"] == "1";
            await tracker.RecordAsync(new VisitRequest(HttpContext.Connection.RemoteIpAddress, path, beacon.Referrer,
                headers.UserAgent.ToString(), optedOut, CloudflareLocation(headers)), cancellationToken);
        }
        return NoContent();
    }

    /// <summary>Anonymous aggregated statistics for the visits page.</summary>
    [HttpGet("api/visitors")]
    public async Task<ActionResult<VisitorSummary>> Get(CancellationToken cancellationToken)
        => Ok(await tracker.GetSummaryAsync(cancellationToken));

    // Cloudflare visitor location headers. They are only used when Visitors:UseProxyLocation is enabled.
    private static GeoLocation? CloudflareLocation(IHeaderDictionary headers)
    {
        var country = headers["CF-IPCountry"].ToString();
        if (country.Length != 2) return null;
        var city = headers["cf-ipcity"].ToString();
        if (city.Contains('%')) city = Uri.UnescapeDataString(city);
        return new GeoLocation(country, city, Coordinate(headers["cf-iplatitude"]), Coordinate(headers["cf-iplongitude"]));
    }

    private static double? Coordinate(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
