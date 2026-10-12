using System.Text.RegularExpressions;
using Portfolio.Application.Images;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Images;

public sealed partial class FileImageRepository(FileStoragePaths paths) : IImageRepository
{
    public async Task<string> SaveAsync(byte[] content, ImageFormat format, CancellationToken cancellationToken)
    {
        var fileName = Guid.NewGuid().ToString("N") + format.Extension;
        if (!ValidName().IsMatch(fileName)) throw new ArgumentException("Formato de imagen no permitido.", nameof(format));

        await File.WriteAllBytesAsync(Path.Combine(paths.UploadsDirectory, fileName), content, cancellationToken);
        return fileName;
    }

    public Task<StoredImage?> OpenReadAsync(string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidName().IsMatch(fileName)) return Task.FromResult<StoredImage?>(null);

        try
        {
            var content = new FileStream(Path.Combine(paths.UploadsDirectory, fileName),
                FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var contentType = Path.GetExtension(fileName) switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                _ => "image/webp"
            };
            return Task.FromResult<StoredImage?>(new StoredImage(content, contentType));
        }
        catch (FileNotFoundException) { return Task.FromResult<StoredImage?>(null); }
    }

    [GeneratedRegex("^[a-f0-9]{32}\\.(png|jpg|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidName();
}
