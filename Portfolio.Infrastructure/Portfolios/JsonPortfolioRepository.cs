using System.Text.Json;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Portfolios;

public sealed class JsonPortfolioRepository(FileStoragePaths paths) : IPortfolioRepository, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<PortfolioData> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadAsync(cancellationToken);
            if (current.Revision != expectedRevision) return false;

            var temporaryFile = paths.PortfolioFile + ".tmp";
            await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(portfolio, PortfolioJson.Options), cancellationToken);
            File.Move(temporaryFile, paths.PortfolioFile, true);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task<PortfolioData> ReadAsync(CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(paths.PortfolioFile, cancellationToken);
        return JsonSerializer.Deserialize<PortfolioData>(content, PortfolioJson.Options)
            ?? throw new InvalidDataException("No se pudo leer el contenido del portfolio.");
    }

    public void Dispose() => _gate.Dispose();
}
