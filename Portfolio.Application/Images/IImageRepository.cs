namespace Portfolio.Application.Images;

public interface IImageRepository
{
    Task<string> SaveAsync(byte[] content, ImageFormat format, CancellationToken cancellationToken);
    Task<StoredImage?> OpenReadAsync(string fileName, CancellationToken cancellationToken);
}
