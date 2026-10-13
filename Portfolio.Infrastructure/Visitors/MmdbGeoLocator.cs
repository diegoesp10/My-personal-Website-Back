using System.Globalization;
using System.Net;
using MaxMind.Db;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Application.Visitors;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Visitors;

/// <summary>Resolves addresses with a local MMDB database, so no address is sent to a third-party service.</summary>
public sealed class MmdbGeoLocator : IGeoLocator, IDisposable
{
    private readonly Lazy<Reader?> _reader;
    private readonly ILogger<MmdbGeoLocator> _logger;

    public MmdbGeoLocator(IOptions<GeoDatabaseOptions> options, FileStoragePaths paths, ILogger<MmdbGeoLocator> logger)
    {
        _logger = logger;
        var file = string.IsNullOrWhiteSpace(options.Value.GeoDatabasePath)
            ? paths.GeoDatabaseFile
            : Path.GetFullPath(options.Value.GeoDatabasePath);
        _reader = new Lazy<Reader?>(() => Open(file), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public GeoLocation? Locate(IPAddress address)
    {
        var reader = _reader.Value;
        if (reader is null) return null;
        try
        {
            var record = reader.Find<Dictionary<string, object>>(address);
            if (record is null || Read(record, "country", "iso_code") is not string country) return null;
            var city = Read(record, "city", "names", "es") ?? Read(record, "city", "names", "en");
            return new GeoLocation(country, city as string ?? "",
                Number(Read(record, "location", "latitude")), Number(Read(record, "location", "longitude")));
        }
        catch (InvalidDatabaseException exception)
        {
            _logger.LogWarning(exception, "No se pudo leer la base de geolocalización.");
            return null;
        }
    }

    private Reader? Open(string file)
    {
        if (!File.Exists(file))
        {
            _logger.LogInformation("Sin base de geolocalización en {File}: las visitas se cuentan sin ubicación.", file);
            return null;
        }
        _logger.LogInformation("Base de geolocalización cargada: {File}", Path.GetFileName(file));
        return new Reader(file);
    }

    private static object? Read(Dictionary<string, object> node, params string[] path)
    {
        object? current = node;
        foreach (var key in path)
            current = current is Dictionary<string, object> map && map.TryGetValue(key, out var next) ? next : null;
        return current;
    }

    private static double? Number(object? value) => value is IConvertible convertible
        ? convertible.ToDouble(CultureInfo.InvariantCulture)
        : null;

    public void Dispose()
    {
        if (_reader.IsValueCreated) _reader.Value?.Dispose();
    }
}
