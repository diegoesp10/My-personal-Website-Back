namespace Portfolio.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Portfolio";
    public string DataPath { get; set; } = "";
    public string SeedPath { get; set; } = "";
}
