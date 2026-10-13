namespace Portfolio.Application.Visitors;

public sealed class VisitorOptions
{
    public const string SectionName = "Visitors";

    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Use the location sent by the proxy (Cloudflare visitor location headers). Enable it only when every request
    /// arrives through that proxy; otherwise anyone could send fake headers.
    /// </summary>
    public bool UseProxyLocation { get; set; }
    public int KeepDays { get; set; } = 400;
    public int RecentVisits { get; set; } = 30;
    public int MaxPages { get; set; } = 200;
    public int MaxLocations { get; set; } = 5000;
    public TimeSpan OnlineWindow { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(30);
}
