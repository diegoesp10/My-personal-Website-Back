using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Seo;

/// <summary>
/// Title, description, canonical address, language alternates, sharing image and schema.org data of each frontend
/// route, built from the content of the route's language (see <see cref="SiteRoutes"/>). Nginx inserts them in the HTML
/// of every page (SSI), so crawlers that do not run JavaScript read them, and the frontend applies them when navigating
/// between pages.
/// </summary>
public sealed partial class SeoService(IPortfolioRepository repository) : ISeoService
{
    private const int DescriptionLength = 160;

    // Static sections of the React frontend. Keep in sync with its router and with SitemapService.
    private static readonly Dictionary<string, SiteSection> Sections = new(StringComparer.Ordinal)
    {
        ["/proyectos"] = new(new("Proyectos", "Projects"), "CollectionPage", profile => new(
            $"Proyectos de {profile.Name}: backends .NET, aplicaciones full stack y webs en producción, con enlaces al código.",
            $"Projects by {profile.Name}: .NET backends, full stack applications and live websites, with links to the code.")),
        ["/experiencia"] = new(new("Experiencia y CV", "Experience and CV"), "WebPage", profile => new(
            $"CV online de {profile.Name}: {Plain(profile.Role)}. Experiencia, competencias técnicas y formación.",
            $"Online CV of {profile.Name}: {Plain(profile.Role)}. Experience, technical skills and education.")),
        ["/github"] = new(new("GitHub", "GitHub"), "WebPage", profile => new(
            $"Repositorios públicos, lenguajes y commits recientes de {profile.Name} en GitHub.",
            $"Public repositories, languages and recent commits by {profile.Name} on GitHub.")),
        ["/visitas"] = new(new("Visitas", "Visits"), "WebPage", profile => new(
            $"Mapa en directo de las visitas a la web de {profile.Name}: países y ciudades, sin cookies ni direcciones IP.",
            $"Live map of visits to the website of {profile.Name}: countries and cities, with no cookies or IP addresses.")),
        ["/sobre-mi"] = new(new("Sobre mí", "About me"), "AboutPage", profile => Localized.Same(FirstParagraph(profile.Bio))),
        ["/diario"] = new(new("Diario", "Journal"), "CollectionPage", profile => new(
            $"Notas de {profile.Name} sobre lo que construye, lo que aprende y lo que vive fuera del código.",
            $"Notes by {profile.Name} on building software, learning and life outside code.")),
        ["/contacto"] = new(new("Contacto", "Contact"), "ContactPage", profile => new(
            $"Contacto profesional de {profile.Name}, {Plain(profile.Role)}. Correo, teléfono y LinkedIn.",
            $"Professional contact for {profile.Name}, {Plain(profile.Role)}. Email, phone and LinkedIn.")),
        // Legal texts. They live in the frontend (src/legal/documents.tsx), so their descriptions are fixed here.
        ["/legal/aviso-legal"] = new(new("Aviso legal", "Legal notice"), "WebPage", _ => new(
            "Quién está detrás de esta web y en qué condiciones puedes usar lo que publica.",
            "Who is behind this website and the terms under which you may use what it publishes.")),
        ["/legal/privacidad"] = new(new("Política de privacidad", "Privacy policy"), "WebPage", _ => new(
            "Qué datos llegan a esta web, para qué se usan y cómo ejercer tus derechos según el RGPD.",
            "What data reaches this website, what it is used for and how to exercise your rights under the GDPR.")),
        ["/legal/cookies"] = new(new("Política de cookies", "Cookie policy"), "WebPage", _ => new(
            "Esta web no usa cookies de seguimiento ni de publicidad. Esto es todo lo que guarda en tu navegador.",
            "This website uses no tracking or advertising cookies. This is everything it stores in your browser.")),
        ["/legal/terminos"] = new(new("Términos de uso", "Terms of use"), "WebPage", _ => new(
            "Las condiciones para usar esta web: qué puedes hacer y qué no, y qué puedes esperar de ella.",
            "The terms for using this website: what you can and cannot do, and what you can expect from it.")),
    };

    // Routes that the frontend redirects: they share the metadata and canonical address of their target.
    private static readonly Dictionary<string, string> Redirects = new(StringComparer.Ordinal)
    {
        ["/legal"] = "/legal/aviso-legal",
    };

    // Utility pages of the frontend that must never be indexed.
    private static readonly Dictionary<string, Localized> Utilities = new(StringComparer.Ordinal)
    {
        ["/editor"] = Localized.Same("Editor"),
        ["/error"] = Localized.Same("Error"),
    };

    private static readonly Localized NotFound = new("No encontrado", "Not found");
    private static readonly Localized HomeName = new("Inicio", "Home");

    public async Task<PageMetadata> GetPageAsync(string path, SiteAddress site, CancellationToken cancellationToken)
    {
        var (language, route) = SiteRoutes.Split(NormalizePath(path));
        if (Redirects.TryGetValue(route, out var target)) route = target;

        // Every language is read: alternates only point to languages where the page exists.
        var contents = new Dictionary<string, PortfolioData>(StringComparer.Ordinal);
        foreach (var code in ContentLanguage.All) contents[code] = await repository.GetAsync(code, cancellationToken);
        var pages = new PageBuilder(contents, language, site);

        if (route == "/") return pages.Home();
        if (Sections.TryGetValue(route, out var section)) return pages.Section(route, section);

        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is ["proyectos", var projectId]
            && contents[language].Projects.FirstOrDefault(project => project.Id == projectId) is { } found)
            return pages.Project(found);
        if (segments is ["diario", var entryId]
            && contents[language].Entries.FirstOrDefault(entry => entry.Id == entryId) is { } entry)
            return pages.Entry(entry);

        return pages.NotIndexed(Utilities.GetValueOrDefault(route, NotFound).In(language));
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

    /// <summary>A fixed text of the site in each language.</summary>
    private sealed record Localized(string Spanish, string English)
    {
        public string In(string language) => language == ContentLanguage.English ? English : Spanish;
        public static Localized Same(string text) => new(text, text);
    }

    private sealed record SiteSection(Localized Title, string Type, Func<Profile, Localized> Describe);

    private sealed class PageBuilder(IReadOnlyDictionary<string, PortfolioData> contents, string language, SiteAddress site)
    {
        private readonly PortfolioData portfolio = contents[language];
        private readonly Profile profile = contents[language].Profile;
        private readonly string root = site.BaseUrl.TrimEnd('/');

        private string HomeUrl => Url("/");
        private string PersonId => root + "/#person";
        private string WebSiteId => root + "/#website";
        private string DefaultImage => root + site.DefaultImage;

        public PageMetadata Home()
        {
            var title = profile.Role.Length > 0 ? $"{profile.Name} — {Plain(profile.Role)}" : profile.Name;
            var description = Summarize(profile.Intro);
            var page = WebPage("ProfilePage", HomeUrl, title, description);
            page["mainEntity"] = Reference(PersonId);
            return Create(title, description, "/", "website", Graph(page), ContentLanguage.All);
        }

        public PageMetadata Section(string route, SiteSection section)
        {
            var url = Url(route);
            var name = section.Title.In(language);
            var title = $"{name} · {profile.Name}";
            var description = Summarize(section.Describe(profile).In(language));
            var page = WebPage(section.Type, url, title, description);
            if (section.Type is "AboutPage" or "ContactPage") page["mainEntity"] = Reference(PersonId);
            return Create(title, description, route, "website", Graph(page, Breadcrumbs((name, url))), ContentLanguage.All);
        }

        public PageMetadata Project(Project project)
        {
            var route = "/proyectos/" + Uri.EscapeDataString(project.Id);
            var url = Url(route);
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
                ["inLanguage"] = SiteRoutes.Tag(language),
            };
            if (project.Tags.Length > 0) work["keywords"] = string.Join(", ", project.Tags);
            if (project.Year.Length == 4 && project.Year.All(char.IsAsciiDigit)) work["dateCreated"] = project.Year;
            var page = WebPage("WebPage", url, title, description);
            page["mainEntity"] = Reference(url + "#work");
            var projects = Sections["/proyectos"].Title.In(language);
            var languages = Having(content => content.Projects.Any(item => item.Id == project.Id));
            var metadata = Create(title, description, route, "article",
                Graph(page, work, Breadcrumbs((projects, Url("/proyectos")), (project.Title, url))), languages);
            return image is null ? metadata : metadata with { Image = image, ImageAlt = project.Title };
        }

        public PageMetadata Entry(Entry entry)
        {
            var route = "/diario/" + Uri.EscapeDataString(entry.Id);
            var url = Url(route);
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
                ["inLanguage"] = SiteRoutes.Tag(language),
            };
            if (published is { } day) article["datePublished"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (entry.Category.Length > 0) article["articleSection"] = entry.Category;
            var journal = Sections["/diario"].Title.In(language);
            var languages = Having(content => content.Entries.Any(item => item.Id == entry.Id));
            var metadata = Create(title, description, route, "article",
                Graph(WebPage("WebPage", url, title, description), article,
                    Breadcrumbs((journal, Url("/diario")), (entry.Title, url))), languages);
            return metadata with { Published = published };
        }

        public PageMetadata NotIndexed(string name)
            => Create($"{name} · {profile.Name}", Summarize(profile.Intro), null, "website", null, []) with
            {
                Indexable = false,
            };

        private PageMetadata Create(string title, string description, string? route, string type, JsonObject? data,
            IReadOnlyList<string> languages)
            => new(title, description, route is null ? null : Url(route), DefaultImage,
                $"{profile.Name}, {Plain(profile.Role)}", type, true, profile.Name, null, data,
                language, SiteRoutes.Locale(language), route is null ? [] : Alternates(route, languages));

        private string Url(string route) => root + SiteRoutes.Localize(language, route);

        private string[] Having(Func<PortfolioData, bool> exists)
            => [.. ContentLanguage.All.Where(code => exists(contents[code]))];

        // The page in each language it exists in; x-default is the Spanish one, or the first available.
        private List<PageAlternate> Alternates(string route, IReadOnlyList<string> languages)
        {
            var alternates = languages.Select(code => new PageAlternate(code, root + SiteRoutes.Localize(code, route))).ToList();
            var fallback = languages.Contains(ContentLanguage.Default) ? ContentLanguage.Default : languages[0];
            alternates.Add(new PageAlternate("x-default", root + SiteRoutes.Localize(fallback, route)));
            return alternates;
        }

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
            ["url"] = root + "/",
            ["name"] = profile.Name,
            ["inLanguage"] = Strings(ContentLanguage.All.Select(SiteRoutes.Tag)),
            ["publisher"] = Reference(PersonId),
        };

        private JsonObject WebPage(string type, string url, string title, string description) => new()
        {
            ["@type"] = type,
            ["@id"] = url + "#page",
            ["url"] = url,
            ["name"] = title,
            ["description"] = description,
            ["inLanguage"] = SiteRoutes.Tag(language),
            ["isPartOf"] = Reference(WebSiteId),
        };

        private JsonObject Breadcrumbs(params (string Name, string Url)[] trail)
        {
            var items = new JsonArray();
            foreach (var (name, url) in trail.Prepend((HomeName.In(language), HomeUrl)))
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
