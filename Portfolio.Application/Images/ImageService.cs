using Portfolio.Application.Common;

namespace Portfolio.Application.Images;

public sealed class ImageService(IImageRepository repository, IImageValidator validator) : IImageService
{
    private const int MaximumBytes = 8 * 1024 * 1024;
    private const string SizeError = "La imagen debe ocupar entre 1 byte y 8 MB.";

    public Task<StoredImage?> GetAsync(string fileName, CancellationToken cancellationToken)
        => repository.OpenReadAsync(fileName, cancellationToken);

    public async Task<OperationResult<UploadedImage>> UploadAsync(Stream content, long length, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaximumBytes) return OperationResult<UploadedImage>.Invalid(SizeError);

        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + count > MaximumBytes) return OperationResult<UploadedImage>.Invalid(SizeError);
            await memory.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        var bytes = memory.ToArray();
        if (bytes.Length == 0) return OperationResult<UploadedImage>.Invalid(SizeError);
        var format = validator.DetectFormat(bytes);
        if (format is null) return OperationResult<UploadedImage>.Invalid("Formato no permitido. Utiliza PNG, JPEG o WebP.");

        var fileName = await repository.SaveAsync(bytes, format, cancellationToken);
        return OperationResult<UploadedImage>.Success(new UploadedImage(fileName));
    }
}
