using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Features.Errors;
using Portfolio.Application.Cv;

namespace Portfolio.Api.Features.Cv;

[ApiController]
public sealed class CvController(ICvService service, RequestLanguage language) : ControllerBase
{
    /// <summary>The CV as a PDF download in the requested language (?language=, Spanish by default).</summary>
    [HttpGet("api/cv")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var cv = await service.GetAsync(language.Value, cancellationToken);
        if (cv is null) return NotFound();
        return File(cv.Content, "application/pdf", cv.FileName, enableRangeProcessing: true);
    }
}
