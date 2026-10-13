using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Seo;

/// <summary>
/// Title, description, canonical address, sharing image and schema.org data of each frontend route, built from the
/// content. Nginx inserts them in the HTML of every page (SSI), so crawlers that do not run JavaScript read them, and
/// the frontend applies them when navigating between pages.
/// </summary>
public sealed partial class SeoService(IPortfolioRepository repository) : ISeoService
{
    private const int DescriptionLength = 160;

    // Static sections of the React frontend. Keep in sync with its router and with SitemapService.
    private static readonly Dictionary<string, SiteSection> Sections = new(StringComparer.Ordinal)
    {
        ["/proyectos"] = new("Proyectos", "CollectionPage", profile =>
            $"Proyectos de {profile.Name}: backends .NET, aplicaciones full stack y webs en producción, con enlaces al código."),
        ["/experiencia"] = new("Experiencia y CV", "WebPage", profile =>
            $"CV online de {profile.Name}: {Plain(profile.Role)}. Experiencia, competencias técnicas y formación."),
        ["/github"] = new("GitHub", "WebPage", profile =>
            $"Repositorios públicos, lenguajes y commits recientes de {profile.Name} en GitHub."),
        ["/visitas"] = new("Visitas", "WebPage", profile =>
            $"Mapa en directo de las visitas a la web de {profile.Name}: países y ciudades, sin cookies ni direcciones IP."),
        ["/sobre-mi"] = new("Sobre mí", "AboutPage", profile => FirstParagraph(profile.Bio)),
        ["/diario"] = new("Diario", "CollectionPage", profile =>
            $"Notas de {profile.Name} sobre lo que construye, lo que aprende y lo que vive fuera del código."),
        ["/contacto"] = new("Contacto", "ContactPage", profile =>
            $"Contacto profesional de {profile.Name}, {Plain(profile.Role)}. Correo, teléfono y LinkedIn."),
    };

    public async Task<PageMetadata> GetPageAsync(string path, SiteAddress site, CancellationToken cancellationToken)
    {
        var portfolio = await repository.GetAsync(cancellationToken);
        var pages = new PageBuilder(portfolio, site);
        var route = NormalizePath(path);

        if (route == "/") return pages.Home();
        if (Sections.TryGetValue(route, out var section)) return pages.Section(route, section);

        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is ["proyectos", var projectId]
            && portfolio.Projects.FirstOrDefault(project => project.Id == projectId) is { } found)
            return pages.Project(found);
        if (segments is ["diario", var entryId]
            && portfolio.Entries.FirstOrDefault(entry => entry.Id == entryId) is { } entry)
            return pages.Entry(entry);

        return pages.NotIndexed(route == "/editor" ? "Editor" : "No encontrado");
    }

    /// <summary>Path without query, fragment or trailing slash, always starting with "/".</summary>
    internal static string NormalizePath(string? path)
    {
        var value = (path ?? "").Split('?', '#')[0].Trim().TrimEnd('/');
        return value.StartsWith('/') ? value : "/" + value;
    }

    /// <summary>Text without the emphasis marks of the content (*text*) and with collapsed whitespace.</summary>
    internal static string Plain(string text) => Whitespace().Replace(text.Replace("*", ""), " ").Trim();

    /// <summary>Plain text cut at a word boundary to fit search result snippets.</summary>
    internal static string Summarize(string text)
    {
        var plain = Plain(text);
        if (plain.Length <= DescriptionLength) return plain;
        var cut = plain[..(DescriptionLength - 1)];
        var space = cut.LastIndexOf(' ');
        if (space > DescriptionLength / 2) cut = cut[..space];
        return cut.TrimEnd(' ', ',', ';', ':', '.', '·', '—', '-') + "…";
    }

    private static string FirstParagraph(string text)
        => text.Split('\n').Select(Plain).FirstOrDefault(paragraph => paragraph.Length > 0) ?? "";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("^/uploads/[a-f0-9]+\\.(png|jpg|webp)$")]
    private static partial Regex UploadedImage();

    private sealed record SiteSection(string Title, string Type, Func<Profile, string> Describe);

    private sealed class PageBuilder(PortfolioData portfolio, SiteAddress site)
    {
        private readonly Profile profile = portfolio.Profile;
        private readonly string root = site.BaseUrl.TrimEnd('/');

        private string HomeUrl => root + "/";
        private string PersonId => root + "/#person";
        private string WebSiteId => root + "/#website";
        private string DefaultImage => root + site.DefaultImage;

        public PageMetadata Home()
        {
            var title = profile.Role.Length > 0 ? $"{profile.Name} — {Plain(profile.Role)}" : profile.Name;
            var description = Summarize(profile.Intro);
            var page = WebPage("ProfilePage", HomeUrl, title, description);
            page["mainEntity"] = Reference(PersonId);
            return Create(title, description, HomeUrl, "website", Graph(page));
        }

        public PageMetadata Section(string route, SiteSection section)
        {
            var url = root + route;
            var title = $"{section.Title} · {profile.Name}";
            var description = Summarize(section.Describe(profile));
            var page = WebPage(section.Type, url, title, description);
            if (section.Type is "AboutPage" or "ContactPage") page["mainEntity"] = Reference(PersonId);
            return Create(title, description, url, "website", Graph(page, Breadcrumbs((section.Title, url))));
        }

        public PageMetadata Project(Project project)
        {
            var url = root + "/proyectos/" + Uri.EscapeDataString(project.Id);
            var title = $"{project.Title} · {profile.Name}";
            var description = Summarize(project.Description);
            var image = ImageUrl(project.Image);
            var work = new JsonObject
            {
                ["@type"] = "CreativeWork",
                ["@id"] = url + "#work",
                ["name"] = project.Title,
                ["description"] = description,
                ["url"] = url,
                ["author"] = Reference(PersonId),
                ["image"] = image ?? DefaultImage,
                ["inLanguage"] = "es-ES",
            };
            if (project.Tags.Length > 0) work["keywords"] = string.Join(", ", project.Tags);
            if (project.Year.Length == 4 && project.Year.All(char.IsAsciiDigit)) work["dateCreated"] = project.Year;
            var page = WebPage("WebPage", url, title, description);
            page["mainEntity"] = Reference(url + "#work");
            var metadata = Create(title, description, url, "article",
                Graph(page, work, Breadcrumbs(("Proyectos", root + "/proyectos"), (project.Title, url))));
            return image is null ? metadata : metadata with { Image = image, ImageAlt = project.Title };
        }

        public PageMetadata Entry(Entry entry)
        {
            var url = root + "/diario/" + Uri.EscapeDataString(entry.Id);
            var title = $"{entry.Title} · {profile.Name}";
            var description = Summarize(FirstParagraph(entry.Text));
            DateOnly? published = DateOnly.TryParseExact(entry.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? date : null;
            var article = new JsonObject
            {
                ["@type"] = "BlogPosting",
                ["@id"] = url + "#article",
                ["headline"] = entry.Title,
                ["description"] = description,
                ["url"] = url,
                ["author"] = Reference(PersonId),
                ["publisher"] = Reference(PersonId),
                ["mainEntityOfPage"] = Reference(url + "#page"),
                ["image"] = DefaultImage,
                ["inLanguage"] = "es-ES",
            };
            if (published is { } day) article["datePublished"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (entry.Category.Length > 0) article["articleSection"] = entry.Category;
            var metadata = Create(title, description, url, "article",
                Graph(WebPage("WebPage", url, title, description), article,
                    Breadcrumbs(("Diario", root + "/diario"), (entry.Title, url))));
            return metadata with { Published = published };
        }

        public PageMetadata NotIndexed(string name)
            => Create($"{name} · {profile.Name}", Summarize(profile.Intro), null, "website", null) with { Indexable = false };

        private PageMetadata Create(string title, string description, string? url, string type, JsonObject? data)
            => new(title, description, url, DefaultImage, $"{profile.Name}, {Plain(profile.Role)}", type, true,
                profile.Name, null, data);

        private JsonObject Graph(params JsonObject[] nodes)
            => new()
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = new JsonArray([Person(), WebSite(), .. nodes]),
            };

        private JsonObject Person()
        {
            var person = new JsonObject
            {
                ["@type"] = "Person",
                ["@id"] = PersonId,
                ["name"] = profile.Name,
                ["url"] = HomeUrl,
                ["image"] = ImageUrl(profile.Portrait) ?? DefaultImage,
            };
            // "Diego Espina" for "Diego Espina Rodríguez": the short form people search for.
            var words = profile.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 3) person["alternateName"] = string.Join(' ', words[..2]);
            if (profile.Role.Length > 0) person["jobTitle"] = Plain(profile.Role);
            if (profile.Intro.Length > 0) person["description"] = Summarize(profile.Intro);
            if (profile.Location.Length > 0)
            {
                var parts = profile.Location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var address = new JsonObject { ["@type"] = "PostalAddress", ["addressLocality"] = parts[0] };
                if (parts.Length > 1) address["addressRegion"] = parts[1];
                person["address"] = address;
            }
            var profiles = new[] { profile.Github, profile.Linkedin }.Where(IsHttpUrl).ToArray();
            if (profiles.Length > 0) person["sameAs"] = Strings(profiles);
            var topics = portfolio.Skills.SelectMany(skill => skill.Tags).Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToArray();
            if (topics.Length > 0) person["knowsAbout"] = Strings(topics);
            return person;
        }

        private JsonObject WebSite() => new()
        {
            ["@type"] = "WebSite",
            ["@id"] = WebSiteId,
            ["url"] = HomeUrl,
            ["name"] = profile.Name,
            ["inLanguage"] = "es-ES",
            ["publisher"] = Reference(PersonId),
        };

        private JsonObject WebPage(string type, string url, string title, string description) => new()
        {
            ["@type"] = type,
            ["@id"] = url + "#page",
            ["url"] = url,
            ["name"] = title,
            ["description"] = description,
            ["inLanguage"] = "es-ES",
            ["isPartOf"] = Reference(WebSiteId),
        };

        private JsonObject Breadcrumbs(params (string Name, string Url)[] trail)
        {
            var items = new JsonArray();
            foreach (var (name, url) in trail.Prepend(("Inicio", HomeUrl)))
                items.Add(new JsonObject
                {
                    ["@type"] = "ListItem",
                    ["position"] = items.Count + 1,
                    ["name"] = name,
                    ["item"] = url,
                });
            return new JsonObject { ["@type"] = "BreadcrumbList", ["itemListElement"] = items };
        }

        private string? ImageUrl(string value) => UploadedImage().IsMatch(value) ? root + value : null;

        private static JsonObject Reference(string id) => new() { ["@id"] = id };

        private static JsonArray Strings(IEnumerable<string> values)
            => new([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);

        private static bool IsHttpUrl(string value)
            => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }
}
