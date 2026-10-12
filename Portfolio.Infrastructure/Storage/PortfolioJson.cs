using System.Text.Json;

namespace Portfolio.Infrastructure.Storage;

internal static class PortfolioJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
