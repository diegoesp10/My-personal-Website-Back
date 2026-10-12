namespace Portfolio.Application.Images;

public interface IImageValidator
{
    ImageFormat? DetectFormat(ReadOnlySpan<byte> content);
}
