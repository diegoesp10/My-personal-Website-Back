using System.Text.Json;
using Microsoft.Extensions.Options;
using Portfolio.Errors.Catalog;
using Portfolio.Errors.Registry;
using Portfolio.Infrastructure.Errors;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Tests.Errors;

public sealed class ErrorRegistryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "portfolio-error-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(DateTimeOffset.Parse("2026-10-13T09:00:00Z"));
    private JsonErrorRegistry Create(int maximum = 1000) => new(
        new FileStoragePaths(Options.Create(new FileStorageOptions { DataPath = _directory, SeedPath = "unused" })),
        Options.Create(new ErrorRegistryOptions { MaximumEntries = maximum, RetentionDays = 14 }), _clock);
    private ErrorRecord Entry(int index) => new(index.ToString(), _clock.GetUtcNow().AddSeconds(index),
        "request.invalid", 400, "trace-" + index, "PUT", "api/portfolio", null);

    [Fact]
    public async Task Concurrent_writes_are_bounded_and_survive_a_new_store()
    {
        using (var registry = Create(12))
            await Task.WhenAll(Enumerable.Range(0, 40).Select(index => registry.AppendAsync(Entry(index), CancellationToken.None)));
        using var restarted = Create(12);
        var entries = await restarted.ReadAsync(50, CancellationToken.None);
        Assert.Equal(12, entries.Count);
        Assert.Equal(Enumerable.Range(28, 12).Reverse().Select(index => index.ToString()), entries.Select(entry => entry.Id));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Expired_entries_are_removed_on_the_next_write()
    {
        using var registry = Create();
        await registry.AppendAsync(Entry(0), CancellationToken.None);
        _clock.Now = _clock.Now.AddDays(15);
        await registry.AppendAsync(Entry(1), CancellationToken.None);
        Assert.Single(await registry.ReadAsync(50, CancellationToken.None));
        Assert.DoesNotContain("trace-0", await File.ReadAllTextAsync(Path.Combine(_directory, "errors.json")));
    }

    [Fact]
    public async Task Recent_errors_with_equal_timestamps_replace_older_entries_at_the_limit()
    {
        using var registry = Create(1);
        await registry.AppendAsync(Entry(0), CancellationToken.None);
        await registry.AppendAsync(Entry(0) with { Id = "new" }, CancellationToken.None);
        Assert.Equal("new", Assert.Single(await registry.ReadAsync(50, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task Corrupt_history_is_preserved_instead_of_silently_overwritten()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "errors.json");
        await File.WriteAllTextAsync(file, "not-json");
        using var registry = Create();
        await Assert.ThrowsAsync<JsonException>(() => registry.AppendAsync(Entry(0), CancellationToken.None));
        Assert.Equal("not-json", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public void Unknown_messages_cannot_leak_through_the_catalogue()
    {
        var error = ErrorCatalog.FromMessage("password=private", 500);
        Assert.Equal("server.unexpected", error.Code);
        Assert.Equal("The operation could not be completed.", error.Message("en"));
        Assert.DoesNotContain("private", error.Message());
    }

    [Fact]
    public async Task Corrupt_visit_store_is_preserved_and_registered_without_its_content()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "visitors.json");
        await File.WriteAllTextAsync(file, "do-not-record");
        var reporter = new RecordingReporter();
        var paths = new FileStoragePaths(Options.Create(new FileStorageOptions { DataPath = _directory, SeedPath = "unused" }));
        var store = new Portfolio.Infrastructure.Visitors.JsonVisitStore(paths, _clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Portfolio.Infrastructure.Visitors.JsonVisitStore>.Instance, reporter);
        Assert.Null(await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(file));
        var backup = Assert.Single(Directory.GetFiles(_directory, "visitors.json.damaged-*"));
        Assert.Equal("do-not-record", await File.ReadAllTextAsync(backup));
        var entry = Assert.Single(reporter.Entries);
        Assert.Equal("visitors.corrupt_store", entry.Code);
        Assert.Null(entry.Status);
        Assert.Equal("storage", entry.Source);
        Assert.DoesNotContain("do-not-record", JsonSerializer.Serialize(entry));
    }

    private sealed class RecordingReporter : IErrorReporter
    {
        public List<ErrorRecord> Entries { get; } = [];
        public Task ReportAsync(ErrorRecord entry) { Entries.Add(entry); return Task.CompletedTask; }
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
