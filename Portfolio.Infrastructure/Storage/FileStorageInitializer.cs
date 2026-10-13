using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Infrastructure.Storage;

/// <summary>
/// Creates the data folders and, for every language without content yet, copies its seed. Existing content is never
/// replaced, so a deployment that adds a language only creates that language's file.
/// </summary>
public sealed class FileStorageInitializer(FileStoragePaths paths) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.DirectoryPath);
        Directory.CreateDirectory(paths.UploadsDirectory);

        foreach (var language in ContentLanguage.All)
        {
            var file = paths.PortfolioFileFor(language);
            if (File.Exists(file)) continue;

            var seed = await File.ReadAllTextAsync(paths.SeedFileFor(language), cancellationToken);
            if (JsonSerializer.Deserialize<PortfolioData>(seed, PortfolioJson.Options) is null)
                throw new InvalidDataException($"El contenido inicial del portfolio ({language}) es inválido.");

            var temporaryFile = file + ".initializing";
            await File.WriteAllTextAsync(temporaryFile, seed, cancellationToken);
            File.Move(temporaryFile, file);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
