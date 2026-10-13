using Portfolio.Errors.Registry;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Portfolio.Application.Visitors;
using Portfolio.Domain.Visitors;

namespace Portfolio.Tests.Visitors;

public sealed class VisitTrackerTests
{
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0) Chrome/140.0";
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly IPAddress Madrid = IPAddress.Parse("81.40.1.1");
    private static readonly IPAddress Lisbon = IPAddress.Parse("85.240.1.1");

    [Fact]
    public async Task Counts_a_visitor_once_per_day_and_every_page_view()
    {
        var (tracker, clock, _) = Create();

        await tracker.RecordAsync(Visit(Madrid, "/"), CancellationToken.None);
        await tracker.RecordAsync(Visit(Madrid, "/proyectos"), CancellationToken.None);
        await tracker.RecordAsync(Visit(Lisbon, "/"), CancellationToken.None);
        clock.Now = Now.AddDays(1);
        await tracker.RecordAsync(Visit(Madrid, "/"), CancellationToken.None);
        var summary = await tracker.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(4, summary.Visits);
        Assert.Equal(3, summary.Visitors);
        Assert.Equal(new DailyVisits(new DateOnly(2026, 10, 12), 3, 2), summary.Days[^2]);
        Assert.Equal(new DailyVisits(new DateOnly(2026, 10, 13), 1, 1), summary.Today);
    }

    [Fact]
    public async Task Groups_visits_by_city_with_rounded_coordinates()
    {
        var (tracker, _, _) = Create();

        await tracker.RecordAsync(Visit(Madrid, "/"), CancellationToken.None);
        await tracker.RecordAsync(Visit(Madrid, "/diario"), CancellationToken.None);
        await tracker.RecordAsync(Visit(Lisbon, "/"), CancellationToken.None);
        var summary = await tracker.GetSummaryAsync(CancellationToken.None);

        var madrid = Assert.Single(summary.Locations, place => place.City == "Madrid");
        Assert.Equal((40.4, -3.7, 2L, 1L), (madrid.Latitude!.Value, madrid.Longitude!.Value, madrid.Visits, madrid.Visitors));
        Assert.Equal(["ES", "PT"], summary.Countries.Select(country => country.Name));
    }

    [Theory]
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)", "/", false)]
    [InlineData("Mozilla/5.0 HeadlessChrome/140.0", "/", false)]
    [InlineData("", "/", false)]
    [InlineData(Browser, "/editor", false)]
    [InlineData(Browser, "https://evil.example/", false)]
    [InlineData(Browser, "/proyectos?x=<script>", false)]
    [InlineData(Browser, "/proyectos/murmur", true)]
    public async Task Ignores_automated_clients_and_non_public_pages(string userAgent, string path, bool counted)
    {
        var (tracker, _, _) = Create();

        await tracker.RecordAsync(new VisitRequest(Madrid, path, null, userAgent, false, null), CancellationToken.None);

        Assert.Equal(counted ? 1 : 0, (await tracker.GetSummaryAsync(CancellationToken.None)).Visits);
    }

    [Fact]
    public async Task Respects_do_not_track_and_global_privacy_control()
    {
        var (tracker, _, _) = Create();

        await tracker.RecordAsync(Visit(Madrid, "/") with { OptedOut = true }, CancellationToken.None);

        Assert.Equal(0, (await tracker.GetSummaryAsync(CancellationToken.None)).Visits);
    }

    [Fact]
    public async Task Counts_private_addresses_without_a_place()
    {
        var (tracker, _, locator) = Create();

        await tracker.RecordAsync(Visit(IPAddress.Parse("192.168.1.20"), "/"), CancellationToken.None);
        var summary = await tracker.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(1, summary.Visits);
        Assert.Empty(summary.Locations);
        Assert.Equal(0, locator.Lookups);
    }

    [Fact]
    public async Task Uses_the_proxy_location_only_when_enabled()
    {
        var proxied = new GeoLocation("JP", "Tokio", 35.6895, 139.6917);
        var (ignoring, _, _) = Create();
        var (trusting, _, _) = Create(options => options.UseProxyLocation = true);

        await ignoring.RecordAsync(Visit(Madrid, "/") with { ProxyLocation = proxied }, CancellationToken.None);
        await trusting.RecordAsync(Visit(Madrid, "/") with { ProxyLocation = proxied }, CancellationToken.None);

        Assert.Equal("Madrid", Assert.Single((await ignoring.GetSummaryAsync(CancellationToken.None)).Locations).City);
        var tokyo = Assert.Single((await trusting.GetSummaryAsync(CancellationToken.None)).Locations);
        Assert.Equal(("Tokio", 35.7, 139.7), (tokyo.City, tokyo.Latitude!.Value, tokyo.Longitude!.Value));
    }

    [Fact]
    public async Task Keeps_referring_sites_by_host_and_recent_visits_newest_first()
    {
        var (tracker, clock, _) = Create(options => options.RecentVisits = 2);

        await tracker.RecordAsync(Visit(Madrid, "/") with { Referrer = "https://www.linkedin.com/in/someone?x=1" }, CancellationToken.None);
        clock.Now = Now.AddMinutes(1);
        await tracker.RecordAsync(Visit(Lisbon, "/github") with { Referrer = "javascript:alert(1)" }, CancellationToken.None);
        clock.Now = Now.AddMinutes(2);
        await tracker.RecordAsync(Visit(Madrid, "/contacto"), CancellationToken.None);
        var summary = await tracker.GetSummaryAsync(CancellationToken.None);

        Assert.Equal([new NamedCount("linkedin.com", 1)], summary.Referrers);
        Assert.Equal(["/contacto", "/github"], summary.Recent.Select(visit => visit.Path));
    }

    [Fact]
    public async Task Counts_people_online_within_the_window()
    {
        var (tracker, clock, _) = Create();
        await tracker.RecordAsync(Visit(Madrid, "/"), CancellationToken.None);
        clock.Now = Now.AddMinutes(4);
        await tracker.RecordAsync(Visit(Lisbon, "/"), CancellationToken.None);

        Assert.Equal(2, (await tracker.GetSummaryAsync(CancellationToken.None)).Online);
        clock.Now = Now.AddMinutes(6);
        Assert.Equal(1, (await tracker.GetSummaryAsync(CancellationToken.None)).Online);
    }

    [Fact]
    public async Task Saves_only_aggregates_and_restores_them()
    {
        var store = new MemoryStore();
        var (tracker, _, _) = Create(store: store);
        await tracker.RecordAsync(Visit(Madrid, "/"), CancellationToken.None);
        await tracker.FlushAsync(CancellationToken.None);
        await tracker.FlushAsync(CancellationToken.None);

        var (restored, _, _) = Create(store: store);
        var summary = await restored.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(1, store.Saves);
        Assert.Equal(1, summary.Visits);
        Assert.DoesNotContain(Madrid.ToString(), System.Text.Json.JsonSerializer.Serialize(store.Stats));
    }

    [Fact]
    public async Task Failed_flush_is_registered_and_dirty_counts_are_retried()
    {
        var store = new MemoryStore { Fail = true };
        var reporter = new RecordingReporter();
        var (tracker, _, _) = Create(store: store, reporter: reporter);
        await tracker.RecordAsync(Visit(Madrid, "/"), CancellationToken.None);
        await tracker.FlushAsync(CancellationToken.None);
        var failure = Assert.Single(reporter.Entries);
        Assert.Equal("visitors.persistence", failure.Code);
        Assert.Null(failure.Status);
        Assert.Equal("background", failure.Source);
        Assert.Equal("System.IO.IOException", failure.ExceptionType);
        Assert.DoesNotContain("do-not-record", System.Text.Json.JsonSerializer.Serialize(failure));
        store.Fail = false;
        await tracker.FlushAsync(CancellationToken.None);
        Assert.Equal(1, store.Saves);
        Assert.Equal(1, store.Stats!.Visits);
    }

    private sealed class RecordingReporter : IErrorReporter
    {
        public List<ErrorRecord> Entries { get; } = [];
        public Task ReportAsync(ErrorRecord entry) { Entries.Add(entry); return Task.CompletedTask; }
    }

    private static VisitRequest Visit(IPAddress address, string path) => new(address, path, null, Browser, false, null);

    private static (VisitTracker Tracker, ManualClock Clock, FakeLocator Locator) Create(
        Action<VisitorOptions>? configure = null, MemoryStore? store = null, RecordingReporter? reporter = null)
    {
        var options = new VisitorOptions();
        configure?.Invoke(options);
        var clock = new ManualClock(Now);
        var locator = new FakeLocator();
        var tracker = new VisitTracker(store ?? new MemoryStore(), locator, Options.Create(options), clock,
            NullLogger<VisitTracker>.Instance, reporter ?? new RecordingReporter());
        return (tracker, clock, locator);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeLocator : IGeoLocator
    {
        public int Lookups { get; private set; }

        public GeoLocation? Locate(IPAddress address)
        {
            Lookups++;
            return address.Equals(Madrid) ? new GeoLocation("es", "Madrid", 40.4168, -3.7038)
                : address.Equals(Lisbon) ? new GeoLocation("PT", "Lisboa", 38.7223, -9.1393)
                : null;
        }
    }

    private sealed class MemoryStore : IVisitStore
    {
        public VisitorStats? Stats { get; private set; }
        public int Saves { get; private set; }
        public bool Fail { get; set; }

        public Task<VisitorStats?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Stats);

        public Task SaveAsync(VisitorStats stats, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("do-not-record");
            Stats = stats;
            Saves++;
            return Task.CompletedTask;
        }
    }
}
