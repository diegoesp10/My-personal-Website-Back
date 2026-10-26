using Microsoft.Extensions.Options;
using Portfolio.Domain.Portfolios;
using Portfolio.Infrastructure.Portfolios;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Tests.Portfolios;

/// <summary>One content file per language: created from its seed, never overwritten, saved independently.</summary>
public sealed class LocalizedStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "portfolio-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileStoragePaths _paths;

    public LocalizedStorageTests()
    {
        _paths = new FileStoragePaths(Options.Create(new FileStorageOptions
        {
            DataPath = _directory,
            SeedPath = Path.Combine(AppContext.BaseDirectory, "seed.json"),
        }));
    }

    [Fact]
    public void Each_language_has_its_own_file_and_seed()
    {
        Assert.Equal(Path.Combine(_paths.DirectoryPath, "portfolio.json"), _paths.PortfolioFileFor(ContentLanguage.Spanish));
        Assert.Equal(Path.Combine(_paths.DirectoryPath, "portfolio.en.json"), _paths.PortfolioFileFor(ContentLanguage.English));
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "seed.en.json"), _paths.SeedFileFor(ContentLanguage.English));
        Assert.Throws<ArgumentOutOfRangeException>(() => _paths.PortfolioFileFor("../fr"));
    }

    [Fact]
    public async Task Creates_the_missing_languages_without_touching_existing_content()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_paths.PortfolioFileFor(ContentLanguage.Spanish),
            await File.ReadAllTextAsync(_paths.SeedFile));
        var spanishBefore = await File.ReadAllTextAsync(_paths.PortfolioFileFor(ContentLanguage.Spanish));

        await new FileStorageInitializer(_paths).StartAsync(CancellationToken.None);

        Assert.Equal(spanishBefore, await File.ReadAllTextAsync(_paths.PortfolioFileFor(ContentLanguage.Spanish)));
        Assert.Equal(await File.ReadAllTextAsync(_paths.SeedFileFor(ContentLanguage.English)),
            await File.ReadAllTextAsync(_paths.PortfolioFileFor(ContentLanguage.English)));
    }

    [Fact]
    public async Task Saves_one_language_without_changing_the_other()
    {
        await new FileStorageInitializer(_paths).StartAsync(CancellationToken.None);
        using var repository = new JsonPortfolioRepository(_paths);
        var english = await repository.GetAsync(ContentLanguage.English, CancellationToken.None);
        var updated = english with
        {
            Revision = "next",
            Profile = english.Profile with { Intro = "Updated English introduction." },
        };

        Assert.True(await repository.TrySaveAsync(updated, english.Revision, ContentLanguage.English, CancellationToken.None));

        Assert.Equal("Updated English introduction.",
            (await repository.GetAsync(ContentLanguage.English, CancellationToken.None)).Profile.Intro);
        var spanish = await repository.GetAsync(ContentLanguage.Spanish, CancellationToken.None);
        Assert.Equal(PortfolioValidatorTests.LoadSeed().Profile.Intro, spanish.Profile.Intro);
        Assert.False(await repository.TrySaveAsync(updated, english.Revision, ContentLanguage.English, CancellationToken.None));
    }

    [Fact]
    public async Task A_content_folder_keeps_content_and_images_apart_from_the_local_data()
    {
        var content = Path.Combine(_directory, "content");
        var paths = new FileStoragePaths(Options.Create(new FileStorageOptions
        {
            DataPath = Path.Combine(_directory, "data"),
            SeedPath = Path.Combine(AppContext.BaseDirectory, "seed.json"),
            ContentPath = content,
        }));

        await new FileStorageInitializer(paths).StartAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(content, "portfolio.json"), paths.PortfolioFileFor(ContentLanguage.Spanish));
        Assert.Equal(Path.Combine(content, "uploads"), paths.UploadsDirectory);
        Assert.Equal(Path.Combine(_directory, "data", "visitors.json"), paths.VisitorsFile);
        Assert.True(File.Exists(Path.Combine(content, "portfolio.en.json")));
        Assert.True(Directory.Exists(paths.UploadsDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(paths.DirectoryPath));
    }

    [Fact]
    public void Without_a_content_folder_everything_stays_in_the_data_folder()
    {
        Assert.Equal(_paths.DirectoryPath, _paths.ContentDirectory);
        Assert.Equal(Path.Combine(_paths.DirectoryPath, "uploads"), _paths.UploadsDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
