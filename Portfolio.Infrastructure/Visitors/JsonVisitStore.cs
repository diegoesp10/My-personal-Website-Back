using System.Text.Json;
using Microsoft.Extensions.Logging;
using Portfolio.Application.Visitors;
using Portfolio.Domain.Visitors;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Visitors;

/// <summary>Aggregated visit statistics in visitors.json, next to the portfolio content.</summary>
public sealed class JsonVisitStore(FileStoragePaths paths, TimeProvider clock, ILogger<JsonVisitStore> logger) : IVisitStore
{
    public async Task<VisitorStats?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.VisitorsFile)) return null;
        try
        {
            await using var stream = File.OpenRead(paths.VisitorsFile);
            return await JsonSerializer.DeserializeAsync<VisitorStats>(stream, PortfolioJson.Options, cancellationToken);
        }
        catch (JsonException exception)
        {
            // Keep the damaged file for inspection and start again instead of overwriting it.
            var aside = $"{paths.VisitorsFile}.damaged-{clock.GetUtcNow():yyyyMMddHHmmss}";
            File.Move(paths.VisitorsFile, aside);
            logger.LogError(exception, "visitors.json no es válido; se ha apartado como {File}.", Path.GetFileName(aside));
            return null;
        }
    }

    public async Task SaveAsync(VisitorStats stats, CancellationToken cancellationToken)
    {
        var temporary = paths.VisitorsFile + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(stats, PortfolioJson.Options), cancellationToken);
        File.Move(temporary, paths.VisitorsFile, true);
    }
}
