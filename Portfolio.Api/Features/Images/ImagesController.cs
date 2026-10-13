using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Contracts;
using Portfolio.Api.Features.Editor;
using Portfolio.Application.Images;

namespace Portfolio.Api.Features.Images;

[ApiController]
public sealed class ImagesController(IImageService service) : ControllerBase
{
    [HttpGet("uploads/{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        var image = await service.GetAsync(name, cancellationToken);
        if (image is null) return NotFound();
        // Uploaded names are random and never reused, so the content can be cached indefinitely.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(image.Content, image.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("api/uploads")]
    [LocalEditorOnly]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken, [FromForm] string? language = null)
    {
        if (!Request.HasFormContentType) return BadRequest(new ApiError("Selecciona una imagen."));
        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null) return BadRequest(new ApiError("Selecciona una imagen."));

        await using var content = file.OpenReadStream();
        var result = await service.UploadAsync(content, file.Length, cancellationToken);
        return result.IsSuccess
            ? Ok(new { url = "/uploads/" + result.Value!.FileName })
            : BadRequest(new ApiError(result.Error!));
    }
}
