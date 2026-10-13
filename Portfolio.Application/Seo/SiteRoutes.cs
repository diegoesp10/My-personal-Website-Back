using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Seo;

/// <summary>
/// Public addresses of the frontend carry the language as their first segment: /es/proyectos, /en/proyectos. The rest
/// of the route is the same in every language, so switching language only changes the prefix.
/// </summary>
public static class SiteRoutes
{
    /// <summary>"/en/proyectos/x" → ("en", "/proyectos/x"). A route without a language prefix is Spanish.</summary>
    public static (string Language, string Route) Split(string path)
    {
        var segments = path.Split('/', 3);
        return segments.Length > 1 && ContentLanguage.IsSupported(segments[1])
            ? (segments[1], segments.Length > 2 && segments[2].Length > 0 ? "/" + segments[2] : "/")
            : (ContentLanguage.Default, path);
    }

    /// <summary>("en", "/proyectos") → "/en/proyectos"; ("es", "/") → "/es".</summary>
    public static string Localize(string language, string route) => "/" + language + (route == "/" ? "" : route);

    /// <summary>Open Graph locale of a language.</summary>
    public static string Locale(string language) => language == ContentLanguage.English ? "en_US" : "es_ES";

    /// <summary>BCP 47 tag of a language for schema.org inLanguage.</summary>
    public static string Tag(string language) => language == ContentLanguage.English ? "en-US" : "es-ES";
}
