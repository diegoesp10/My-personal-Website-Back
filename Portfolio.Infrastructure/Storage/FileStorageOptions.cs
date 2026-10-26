namespace Portfolio.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Portfolio";
    public string DataPath { get; set; } = "";
    public string SeedPath { get; set; } = "";
    /// <summary>
    /// Folder of the content: portfolio.json per language and uploads/. Empty means the data folder. The local profile
    /// points it to the versioned content/ folder, so the editor saves what CI/CD publishes.
    /// </summary>
    public string ContentPath { get; set; } = "";
}
