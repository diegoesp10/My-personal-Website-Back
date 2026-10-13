namespace Portfolio.Domain.Visitors;

/// <summary>
/// Aggregated, anonymous visit statistics. No IP address, identifier or cookie is stored: only counters per day,
/// per approximate location (city level, coordinates rounded), per page and per referring site.
/// </summary>
public sealed record VisitorStats
{
    public DateOnly Since { get; init; }
    public long Visits { get; init; }
    /// <summary>Sum of daily unique visitors: a person counts once per day.</summary>
    public long Visitors { get; init; }
    public DailyVisits[] Days { get; init; } = [];
    public VisitLocation[] Locations { get; init; } = [];
    public NamedCount[] Pages { get; init; } = [];
    public NamedCount[] Referrers { get; init; } = [];
    public RecentVisit[] Recent { get; init; } = [];
}

public sealed record DailyVisits(DateOnly Date, long Visits, long Visitors);

/// <param name="Country">ISO 3166-1 alpha-2 code.</param>
/// <param name="Latitude">Rounded to one decimal; null when only the country is known.</param>
public sealed record VisitLocation(string Country, string City, double? Latitude, double? Longitude,
    long Visits, long Visitors, DateTimeOffset LastSeen);

public sealed record NamedCount(string Name, long Count);

public sealed record RecentVisit(string Country, string City, string Path, DateTimeOffset At);
