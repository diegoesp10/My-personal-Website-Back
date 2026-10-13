using System.Text.Json;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Portfolios;

/// <summary>One JSON file per language (see <see cref="FileStoragePaths.PortfolioFileFor"/>), each with its own revision.</summary>
public sealed class JsonPortfolioRepository(FileStoragePaths paths) : IPortfolioRepository, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<PortfolioData> GetAsync(string language, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(paths.PortfolioFileFor(language), cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, string language,
        CancellationToken cancellationToken)
    {
        var file = paths.PortfolioFileFor(language);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadAsync(file, cancellationToken);
            if (current.Revision != expectedRevision) return false;

            var temporaryFile = file + ".tmp";
            await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(portfolio, PortfolioJson.Options), cancellationToken);
            File.Move(temporaryFile, file, true);
            return true;
        }
        finally { _gate.Release(); }
    }

    private static async Task<PortfolioData> ReadAsync(string file, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(file, cancellationToken);
        return JsonSerializer.Deserialize<PortfolioData>(content, PortfolioJson.Options)
            ?? throw new InvalidDataException("No se pudo leer el contenido del portfolio.");
    }

    public void Dispose() => _gate.Dispose();
}
