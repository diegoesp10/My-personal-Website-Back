namespace Portfolio.Api.Features.Editor;

public sealed class LocalEditorOptions
{
    public const string SectionName = "LocalEditor";
    public string[] AllowedHosts { get; set; } = ["localhost", "127.0.0.1", "::1"];
    public string[] AllowedOrigins { get; set; } = [
        "http://127.0.0.1:5173", "http://localhost:5173",
        "http://127.0.0.1:5080", "http://localhost:5080"
    ];
}
