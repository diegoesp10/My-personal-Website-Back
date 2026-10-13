using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Storage;

public sealed class FileStoragePaths(IOptions<FileStorageOptions> options)
{
    public string DirectoryPath { get; } = Path.GetFullPath(options.Value.DataPath);
    public string SeedFile { get; } = Path.GetFullPath(options.Value.SeedPath);
    public string PortfolioFile => Path.Combine(DirectoryPath, "portfolio.json");
    public string UploadsDirectory => Path.Combine(DirectoryPath, "uploads");
    public string VisitorsFile => Path.Combine(DirectoryPath, "visitors.json");
    /// <summary>Default location of the optional IP geolocation database (DB-IP City Lite, MMDB format).</summary>
    public string GeoDatabaseFile => Path.Combine(DirectoryPath, "geo", "dbip-city-lite.mmdb");
}
