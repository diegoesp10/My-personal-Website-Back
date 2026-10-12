using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Storage;

public sealed class FileStoragePaths(IOptions<FileStorageOptions> options)
{
    public string DirectoryPath { get; } = Path.GetFullPath(options.Value.DataPath);
    public string SeedFile { get; } = Path.GetFullPath(options.Value.SeedPath);
    public string PortfolioFile => Path.Combine(DirectoryPath, "portfolio.json");
    public string UploadsDirectory => Path.Combine(DirectoryPath, "uploads");
}
