namespace Portfolio.Infrastructure.Visitors;

public sealed class GeoDatabaseOptions
{
    public const string SectionName = "Visitors";

    /// <summary>
    /// MMDB file with city-level IP geolocation (for example DB-IP City Lite, CC BY 4.0). Empty uses
    /// geo/dbip-city-lite.mmdb in the data folder. Without the file, visits are counted without a place unless the
    /// proxy supplies it.
    /// </summary>
    public string GeoDatabasePath { get; set; } = "";
}
