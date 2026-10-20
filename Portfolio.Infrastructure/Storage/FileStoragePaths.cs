using Microsoft.Extensions.Options;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Infrastructure.Storage;

public sealed class FileStoragePaths(IOptions<FileStorageOptions> options)
{
    public string DirectoryPath { get; } = Path.GetFullPath(options.Value.DataPath);
    public string SeedFile { get; } = Path.GetFullPath(options.Value.SeedPath);
    public string UploadsDirectory => Path.Combine(DirectoryPath, "uploads");
    public string VisitorsFile => Path.Combine(DirectoryPath, "visitors.json");
    /// <summary>Default location of the optional IP geolocation database (DB-IP City Lite, MMDB format).</summary>
    public string GeoDatabaseFile => Path.Combine(DirectoryPath, "geo", "dbip-city-lite.mmdb");

    /// <summary>Content of a language: portfolio.json for Spanish, portfolio.{language}.json for the others.</summary>
    public string PortfolioFileFor(string language) => Path.Combine(DirectoryPath, LocalizedName("portfolio", language));

    /// <summary>Initial content of a language, next to the Spanish seed: seed.json, seed.{language}.json.</summary>
    public string SeedFileFor(string language)
        => Path.Combine(Path.GetDirectoryName(SeedFile)!, LocalizedName(Path.GetFileNameWithoutExtension(SeedFile), language));

    /// <summary>CV of a language, published with the seeds: cv/CV_Diego_Espina_ES.pdf, cv/CV_Diego_Espina_EN.pdf.</summary>
    public string CvFileFor(string language)
    {
        if (!ContentLanguage.IsSupported(language))
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported content language.");
        return Path.Combine(Path.GetDirectoryName(SeedFile)!, "cv", $"CV_Diego_Espina_{language.ToUpperInvariant()}.pdf");
    }

    private static string LocalizedName(string name, string language)
    {
        if (!ContentLanguage.IsSupported(language))
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported content language.");
        return language == ContentLanguage.Default ? $"{name}.json" : $"{name}.{language}.json";
    }
}
