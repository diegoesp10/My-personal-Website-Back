using Portfolio.Application.Common;

namespace Portfolio.Application.Images;

public interface IImageService
{
    Task<OperationResult<UploadedImage>> UploadAsync(Stream content, long length, CancellationToken cancellationToken);
    Task<StoredImage?> GetAsync(string fileName, CancellationToken cancellationToken);
}
