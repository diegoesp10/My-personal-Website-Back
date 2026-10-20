using Portfolio.Application.Cv;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Cv;

/// <summary>Reads the CV PDFs published with the application (cv/ next to the seeds).</summary>
public sealed class FileCvRepository(FileStoragePaths paths) : ICvRepository
{
    public Task<StoredCv?> OpenReadAsync(string language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = paths.CvFileFor(language);
        try
        {
            var content = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            return Task.FromResult<StoredCv?>(new StoredCv(content, Path.GetFileName(file)));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<StoredCv?>(null);
        }
    }
}
