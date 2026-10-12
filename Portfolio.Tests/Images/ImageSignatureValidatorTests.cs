using System.Text;
using Portfolio.Application.Images;

namespace Portfolio.Tests.Images;

public sealed class ImageSignatureValidatorTests
{
    private readonly ImageSignatureValidator _validator = new();

    [Fact]
    public void Detects_png() => Assert.Equal(".png",
        _validator.DetectFormat([137, 80, 78, 71, 13, 10, 26, 10, 0, 0])?.Extension);

    [Fact]
    public void Detects_jpeg() => Assert.Equal(".jpg", _validator.DetectFormat([255, 216, 255, 224])?.Extension);

    [Fact]
    public void Detects_webp() => Assert.Equal(".webp",
        _validator.DetectFormat(Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 "))?.Extension);

    [Fact]
    public void Rejects_svg_and_other_text() => Assert.Null(_validator.DetectFormat(Encoding.UTF8.GetBytes("<svg></svg>")));
}
