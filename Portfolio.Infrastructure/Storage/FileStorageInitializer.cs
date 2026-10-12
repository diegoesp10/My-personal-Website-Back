using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Infrastructure.Storage;

public sealed class FileStorageInitializer(FileStoragePaths paths) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.DirectoryPath);
        Directory.CreateDirectory(paths.UploadsDirectory);

        if (File.Exists(paths.PortfolioFile)) return;

        var seed = await File.ReadAllTextAsync(paths.SeedFile, cancellationToken);
        if (JsonSerializer.Deserialize<PortfolioData>(seed, PortfolioJson.Options) is null)
            throw new InvalidDataException("El contenido inicial del portfolio es inválido.");

        var temporaryFile = paths.PortfolioFile + ".initializing";
        await File.WriteAllTextAsync(temporaryFile, seed, cancellationToken);
        File.Move(temporaryFile, paths.PortfolioFile);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
