using System.Text;

namespace Portfolio.Application.Images;

public sealed class ImageSignatureValidator : IImageValidator
{
    public ImageFormat? DetectFormat(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 8 && content[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return new ImageFormat(".png", "image/png");
        if (content.Length >= 3 && content[0] == 255 && content[1] == 216 && content[2] == 255)
            return new ImageFormat(".jpg", "image/jpeg");
        if (content.Length >= 12 && Encoding.ASCII.GetString(content[..4]) == "RIFF"
            && Encoding.ASCII.GetString(content.Slice(8, 4)) == "WEBP")
            return new ImageFormat(".webp", "image/webp");

        return null;
    }
}
