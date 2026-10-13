using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Domain.Visitors;

namespace Portfolio.Application.Visitors;

/// <summary>
/// Counts page views in memory and saves the aggregates periodically. A visitor is recognised during the day by an
/// HMAC of address and browser whose key is random, changes every day and is never stored: visits cannot be linked
/// across days or traced back to an address. Locations are kept at city level with coordinates rounded to ~10 km.
/// </summary>
public sealed partial class VisitTracker(IVisitStore store, IGeoLocator locator, IOptions<VisitorOptions> options,
    TimeProvider clock, ILogger<VisitTracker> logger) : IVisitTracker, IDisposable
{
    private const int SummaryDays = 30;
    private const int SummaryLocations = 300;
    private const int SummaryRecent = 12;
    private const int TopItems = 8;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _seenToday = [];
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = [];
    private Totals? _totals;
    private DateOnly _day;
    private byte[] _key = [];
    private bool _dirty;

    public async Task RecordAsync(VisitRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || request.OptedOut || IsAutomated(request.UserAgent)) return;
        var path = NormalizePath(request.Path);
        if (path is null) return;

        var location = Clean(settings.UseProxyLocation && request.ProxyLocation is not null
            ? request.ProxyLocation
            : request.Address is { } address && IsPublic(address) ? locator.Locate(address) : null);
        var referrer = ReferrerHost(request.Referrer);
        var now = clock.GetUtcNow();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var totals = await LoadAsync(cancellationToken);
            StartDay(DateOnly.FromDateTime(now.UtcDateTime));
            var visitor = Fingerprint(request.Address, request.UserAgent);
            var firstToday = _seenToday.Add("v|" + visitor);

            totals.Visits++;
            if (firstToday) totals.Visitors++;
            var day = GetOrAdd(totals.Days, _day);
            day.Visits++;
            if (firstToday) day.Visitors++;

            if (location is not null)
            {
                var key = LocationKey(location);
                if (!totals.Locations.TryGetValue(key, out var place) && totals.Locations.Count < settings.MaxLocations)
                    totals.Locations[key] = place = new PlaceCount(location);
                if (place is not null)
                {
                    place.Visits++;
                    if (_seenToday.Add("l|" + visitor + "|" + key)) place.Visitors++;
                    place.LastSeen = now;
                }
            }

            Increment(totals.Pages, path, settings.MaxPages);
            if (referrer is not null) Increment(totals.Referrers, referrer, settings.MaxPages);
            totals.Recent.AddFirst(new RecentVisit(location?.Country ?? "", location?.City ?? "", path, now));
            while (totals.Recent.Count > settings.RecentVisits) totals.Recent.RemoveLast();
            var oldest = _day.AddDays(-settings.KeepDays);
            foreach (var expired in totals.Days.Keys.Where(date => date < oldest).ToArray()) totals.Days.Remove(expired);

            _lastSeen[visitor] = now;
            _dirty = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<VisitorSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var totals = await LoadAsync(cancellationToken);
            var now = clock.GetUtcNow();
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            foreach (var visitor in _lastSeen.Where(entry => now - entry.Value > settings.OnlineWindow)
                         .Select(entry => entry.Key).ToArray())
                _lastSeen.Remove(visitor);

            var days = Enumerable.Range(1 - SummaryDays, SummaryDays)
                .Select(offset => today.AddDays(offset))
                .Select(date => totals.Days.TryGetValue(date, out var count)
                    ? new DailyVisits(date, count.Visits, count.Visitors)
                    : new DailyVisits(date, 0, 0))
                .ToArray();

            return new VisitorSummary
            {
                Enabled = settings.Enabled,
                Since = totals.Visits > 0 ? totals.Since : null,
                Visits = totals.Visits,
                Visitors = totals.Visitors,
                Online = _lastSeen.Count,
                Today = days[^1],
                Days = days,
                Locations = totals.Locations.Values
                    .Where(place => place.Location.Latitude is not null && place.Location.Longitude is not null)
                    .OrderByDescending(place => place.Visitors).ThenByDescending(place => place.Visits)
                    .Take(SummaryLocations).Select(place => place.ToRecord()).ToArray(),
                Countries = totals.Locations.Values
                    .GroupBy(place => place.Location.Country)
                    .Select(group => new NamedCount(group.Key, group.Sum(place => place.Visitors)))
                    .OrderByDescending(country => country.Count).ToArray(),
                Pages = Top(totals.Pages),
                Referrers = Top(totals.Referrers),
                Recent = totals.Recent.Take(SummaryRecent).ToArray()
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_dirty || _totals is null) return;
            await store.SaveAsync(_totals.ToStats(), cancellationToken);
            _dirty = false;
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "No se pudieron guardar las estadísticas de visitas.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Totals> LoadAsync(CancellationToken cancellationToken)
    {
        if (_totals is not null) return _totals;
        var stored = await store.LoadAsync(cancellationToken);
        return _totals = stored is null
            ? new Totals { Since = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) }
            : Totals.From(stored);
    }

    private void StartDay(DateOnly today)
    {
        if (today == _day && _key.Length > 0) return;
        _day = today;
        _key = RandomNumberGenerator.GetBytes(32);
        _seenToday.Clear();
    }

    private string Fingerprint(IPAddress? address, string? userAgent)
    {
        var data = Encoding.UTF8.GetBytes($"{address}\n{userAgent}");
        return Convert.ToHexString(HMACSHA256.HashData(_key, data), 0, 12);
    }

    private static DayCount GetOrAdd(SortedDictionary<DateOnly, DayCount> days, DateOnly date)
    {
        if (!days.TryGetValue(date, out var day)) days[date] = day = new DayCount();
        return day;
    }

    private static void Increment(Dictionary<string, long> counts, string name, int maximum)
    {
        if (counts.TryGetValue(name, out var count)) counts[name] = count + 1;
        else if (counts.Count < maximum) counts[name] = 1;
    }

    private static NamedCount[] Top(Dictionary<string, long> counts) => counts
        .OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal)
        .Take(TopItems).Select(entry => new NamedCount(entry.Key, entry.Value)).ToArray();

    private static string LocationKey(GeoLocation location) => string.Create(CultureInfo.InvariantCulture,
        $"{location.Country}|{location.City}|{location.Latitude:0.0}|{location.Longitude:0.0}");

    internal static bool IsAutomated(string? userAgent)
        => string.IsNullOrWhiteSpace(userAgent) || AutomatedAgent().IsMatch(userAgent);

    /// <summary>Path of a public page, without query or fragment; null for anything else.</summary>
    internal static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim();
        if (path.Length > 1) path = path.TrimEnd('/');
        if (!PagePath().IsMatch(path) || path.Contains("..", StringComparison.Ordinal)) return null;
        return path == "/editor" || path.StartsWith("/editor/", StringComparison.Ordinal) ? null : path;
    }

    internal static string? ReferrerHost(string? referrer)
    {
        if (!Uri.TryCreate(referrer, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return null;
        var host = uri.IdnHost.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        return host.Length is > 0 and <= 100 ? host : null;
    }

    /// <summary>Only public addresses can be located; local and private networks are counted without a place.</summary>
    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] is 0 or 10 or 127 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168
                || b[0] == 169 && b[1] == 254 || b[0] == 100 && b[1] is >= 64 and <= 127);
        }
        return address.AddressFamily == AddressFamily.InterNetworkV6
            && !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal
                || address.Equals(IPAddress.IPv6None));
    }

    /// <summary>Validated location with coordinates rounded to one decimal (~10 km).</summary>
    internal static GeoLocation? Clean(GeoLocation? location)
    {
        if (location is null) return null;
        var country = location.Country.Trim().ToUpperInvariant();
        if (!CountryCode().IsMatch(country) || country is "XX" or "T1") return null;
        var city = location.City.Trim();
        if (city.Length > 80) city = city[..80];
        var valid = location.Latitude is >= -90 and <= 90 && location.Longitude is >= -180 and <= 180
            && !(location.Latitude == 0 && location.Longitude == 0);
        return new GeoLocation(country, city,
            valid ? Math.Round(location.Latitude!.Value, 1) : null,
            valid ? Math.Round(location.Longitude!.Value, 1) : null);
    }

    [GeneratedRegex(@"^/[A-Za-z0-9._~%/-]{0,199}$", RegexOptions.CultureInvariant)]
    private static partial Regex PagePath();

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCode();

    [GeneratedRegex(@"bot|crawl|spider|slurp|headless|lighthouse|monitor|preview|curl|wget|python|java/|okhttp|go-http|httpclient|facebookexternalhit|embedly|whatsapp|telegram|discord|slack",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutomatedAgent();

    public void Dispose() => _gate.Dispose();

    private sealed class DayCount
    {
        public long Visits;
        public long Visitors;
    }

    private sealed class PlaceCount(GeoLocation location)
    {
        public GeoLocation Location { get; } = location;
        public long Visits;
        public long Visitors;
        public DateTimeOffset LastSeen;

        public VisitLocation ToRecord() => new(Location.Country, Location.City, Location.Latitude, Location.Longitude,
            Visits, Visitors, LastSeen);
    }

    private sealed class Totals
    {
        public DateOnly Since;
        public long Visits;
        public long Visitors;
        public readonly SortedDictionary<DateOnly, DayCount> Days = new();
        public readonly Dictionary<string, PlaceCount> Locations = new(StringComparer.Ordinal);
        public readonly Dictionary<string, long> Pages = new(StringComparer.Ordinal);
        public readonly Dictionary<string, long> Referrers = new(StringComparer.Ordinal);
        public readonly LinkedList<RecentVisit> Recent = new();

        public static Totals From(VisitorStats stats)
        {
            var totals = new Totals { Since = stats.Since, Visits = stats.Visits, Visitors = stats.Visitors };
            foreach (var day in stats.Days) totals.Days[day.Date] = new DayCount { Visits = day.Visits, Visitors = day.Visitors };
            foreach (var place in stats.Locations)
            {
                var location = new GeoLocation(place.Country, place.City, place.Latitude, place.Longitude);
                totals.Locations[LocationKey(location)] = new PlaceCount(location)
                {
                    Visits = place.Visits, Visitors = place.Visitors, LastSeen = place.LastSeen
                };
            }
            foreach (var page in stats.Pages) totals.Pages[page.Name] = page.Count;
            foreach (var referrer in stats.Referrers) totals.Referrers[referrer.Name] = referrer.Count;
            foreach (var visit in stats.Recent) totals.Recent.AddLast(visit);
            return totals;
        }

        public VisitorStats ToStats() => new()
        {
            Since = Since,
            Visits = Visits,
            Visitors = Visitors,
            Days = Days.Select(day => new DailyVisits(day.Key, day.Value.Visits, day.Value.Visitors)).ToArray(),
            Locations = Locations.Values.Select(place => place.ToRecord()).ToArray(),
            Pages = Pages.Select(page => new NamedCount(page.Key, page.Value)).ToArray(),
            Referrers = Referrers.Select(referrer => new NamedCount(referrer.Key, referrer.Value)).ToArray(),
            Recent = Recent.ToArray()
        };
    }
}
