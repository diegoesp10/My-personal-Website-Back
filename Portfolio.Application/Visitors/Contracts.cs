using System.Net;
using Portfolio.Domain.Visitors;

namespace Portfolio.Application.Visitors;

/// <param name="Country">ISO 3166-1 alpha-2 code.</param>
public sealed record GeoLocation(string Country, string City, double? Latitude, double? Longitude);

/// <summary>A page view reported by the browser. The address is used only while recording it, never stored.</summary>
/// <param name="OptedOut">The browser asked not to be tracked (DNT or Global Privacy Control).</param>
/// <param name="ProxyLocation">Location supplied by a trusted proxy, used when <see cref="VisitorOptions.UseProxyLocation"/> is on.</param>
public sealed record VisitRequest(IPAddress? Address, string Path, string? Referrer, string? UserAgent,
    bool OptedOut, GeoLocation? ProxyLocation);

/// <summary>Approximate location of a public IP address, resolved locally (no third-party service).</summary>
public interface IGeoLocator
{
    GeoLocation? Locate(IPAddress address);
}

public interface IVisitStore
{
    Task<VisitorStats?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(VisitorStats stats, CancellationToken cancellationToken);
}

public interface IVisitTracker
{
    Task RecordAsync(VisitRequest request, CancellationToken cancellationToken);
    Task<VisitorSummary> GetSummaryAsync(CancellationToken cancellationToken);
    /// <summary>Writes pending changes to the store. Called periodically and on shutdown.</summary>
    Task FlushAsync(CancellationToken cancellationToken);
}

/// <summary>Public summary shown on the visits page.</summary>
public sealed record VisitorSummary
{
    public bool Enabled { get; init; }
    public DateOnly? Since { get; init; }
    public long Visits { get; init; }
    public long Visitors { get; init; }
    public int Online { get; init; }
    public DailyVisits? Today { get; init; }
    /// <summary>Last 30 days, oldest first, including days without visits.</summary>
    public DailyVisits[] Days { get; init; } = [];
    public VisitLocation[] Locations { get; init; } = [];
    /// <summary>Visitors per country (ISO code), most first.</summary>
    public NamedCount[] Countries { get; init; } = [];
    public NamedCount[] Pages { get; init; } = [];
    public NamedCount[] Referrers { get; init; } = [];
    public RecentVisit[] Recent { get; init; } = [];
}
