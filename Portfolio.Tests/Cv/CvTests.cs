using Microsoft.Extensions.Options;
using Portfolio.Application.Cv;
using Portfolio.Domain.Portfolios;
using Portfolio.Infrastructure.Cv;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Tests.Cv;

/// <summary>One CV per language, published next to the seeds; other languages get none.</summary>
public sealed class CvTests
{
    private readonly FileStoragePaths _paths = new(Options.Create(new FileStorageOptions
    {
        DataPath = Path.Combine(Path.GetTempPath(), "portfolio-tests-" + Guid.NewGuid().ToString("N")),
        SeedPath = Path.Combine(AppContext.BaseDirectory, "seed.json"),
    }));

    [Theory]
    [InlineData(ContentLanguage.Spanish, "CV_Diego_Espina_ES.pdf")]
    [InlineData(ContentLanguage.English, "CV_Diego_Espina_EN.pdf")]
    public async Task Serves_the_pdf_of_each_language(string language, string fileName)
    {
        var service = new CvService(new FileCvRepository(_paths));

        var cv = await service.GetAsync(language, CancellationToken.None);

        Assert.NotNull(cv);
        await using var content = cv.Content;
        Assert.Equal(fileName, cv.FileName);
        var header = new byte[5];
        await content.ReadExactlyAsync(header);
        Assert.Equal("%PDF-"u8.ToArray(), header);
    }

    [Fact]
    public async Task Languages_differ_and_unsupported_ones_have_none()
    {
        var service = new CvService(new FileCvRepository(_paths));

        Assert.NotEqual(await File.ReadAllBytesAsync(_paths.CvFileFor(ContentLanguage.Spanish)),
            await File.ReadAllBytesAsync(_paths.CvFileFor(ContentLanguage.English)));
        Assert.Null(await service.GetAsync("../fr", CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => _paths.CvFileFor("fr"));
    }

    [Fact]
    public async Task A_missing_file_is_not_found()
    {
        var paths = new FileStoragePaths(Options.Create(new FileStorageOptions
        {
            DataPath = Path.GetTempPath(),
            SeedPath = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"), "seed.json"),
        }));

        Assert.Null(await new FileCvRepository(paths).OpenReadAsync(ContentLanguage.Spanish, CancellationToken.None));
    }
}
