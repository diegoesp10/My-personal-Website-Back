using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Portfolio.Application.Seo;

namespace Portfolio.Api.Features.Seo;

/// <summary>
/// Renders <see cref="PageMetadata"/> as &lt;head&gt; tags. Every tag except the title carries data-seo, so the
/// frontend can replace the set when navigating. The frontend builds the same tags in src/lib/seo.ts.
/// </summary>
public static class SeoHeadRenderer
{
    // Keeps accents readable; markup characters are still escaped, including "<" inside the JSON-LD script.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public static string Render(PageMetadata page)
    {
        var html = new StringBuilder();
        html.Append("<title>").Append(Html.Encode(page.Title)).Append("</title>\n");
        Meta(html, "name", "description", page.Description);
        if (page.Indexable && page.Url is { } url)
            html.Append("<link rel=\"canonical\" href=\"").Append(Html.Encode(url)).Append("\" data-seo>\n");
        else
            Meta(html, "name", "robots", "noindex");
        foreach (var alternate in page.Alternates)
            html.Append("<link rel=\"alternate\" hreflang=\"").Append(Html.Encode(alternate.Language))
                .Append("\" href=\"").Append(Html.Encode(alternate.Url)).Append("\" data-seo>\n");
        Meta(html, "property", "og:type", page.Type);
        Meta(html, "property", "og:site_name", page.SiteName);
        Meta(html, "property", "og:locale", page.Locale);
        foreach (var other in page.Alternates.Where(item => item.Language != page.Language && item.Language != "x-default"))
            Meta(html, "property", "og:locale:alternate", SiteRoutes.Locale(other.Language));
        Meta(html, "property", "og:title", page.Title);
        Meta(html, "property", "og:description", page.Description);
        if (page.Url is { } address) Meta(html, "property", "og:url", address);
        Meta(html, "property", "og:image", page.Image);
        Meta(html, "property", "og:image:alt", page.ImageAlt);
        if (page.Published is { } date)
            Meta(html, "property", "article:published_time", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Meta(html, "name", "twitter:card", "summary_large_image");
        Meta(html, "name", "twitter:title", page.Title);
        Meta(html, "name", "twitter:description", page.Description);
        Meta(html, "name", "twitter:image", page.Image);
        if (page.StructuredData is { } data)
            html.Append("<script type=\"application/ld+json\" data-seo>")
                .Append(data.ToJsonString(Json))
                .Append("</script>\n");
        return html.ToString();
    }

    private static void Meta(StringBuilder html, string attribute, string name, string content)
    {
        if (content.Length == 0) return;
        html.Append("<meta ").Append(attribute).Append("=\"").Append(name)
            .Append("\" content=\"").Append(Html.Encode(content)).Append("\" data-seo>\n");
    }
}
