using System.Text.Json;
using Microsoft.Extensions.Options;
using Portfolio.Errors.Registry;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure.Errors;

/// <summary>Single-instance, bounded history in the persistent data volume. Writes are atomic.</summary>
public sealed class JsonErrorRegistry(FileStoragePaths paths, IOptions<ErrorRegistryOptions> options,
    TimeProvider clock) : IErrorRegistry, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string FilePath => Path.Combine(paths.DirectoryPath, "errors.json");

    public async Task AppendAsync(ErrorRecord entry, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadAsync(cancellationToken);
            var retained = Retain(new[] { entry }.Concat(entries));
            Directory.CreateDirectory(paths.DirectoryPath);
            var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(retained, Json), cancellationToken);
                File.Move(temporary, FilePath, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<ErrorRecord>> ReadAsync(int limit, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return Retain(await LoadAsync(cancellationToken)).Take(Math.Clamp(limit, 1, 200)).ToArray(); }
        finally { _gate.Release(); }
    }

    private async Task<ErrorRecord[]> LoadAsync(CancellationToken cancellationToken)
        => File.Exists(FilePath)
            ? JsonSerializer.Deserialize<ErrorRecord[]>(await File.ReadAllTextAsync(FilePath, cancellationToken), Json)
                ?? throw new InvalidDataException("The error history is invalid.")
            : [];

    private ErrorRecord[] Retain(IEnumerable<ErrorRecord> entries)
        => entries.Where(entry => entry.OccurredAt >= clock.GetUtcNow().AddDays(-options.Value.RetentionDays))
            .OrderByDescending(entry => entry.OccurredAt).Take(options.Value.MaximumEntries).ToArray();

    public void Dispose() => _gate.Dispose();
}
