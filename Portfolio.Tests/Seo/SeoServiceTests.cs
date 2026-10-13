using System.Text.Json.Nodes;
using Portfolio.Application.Portfolios;
using Portfolio.Application.Seo;
using Portfolio.Domain.Portfolios;
using Portfolio.Tests.Portfolios;

namespace Portfolio.Tests.Seo;

public sealed class SeoServiceTests
{
    private const string Root = "https://example.com";
    private static readonly SiteAddress Site = new(Root + "/", "/og-image.png");

    [Fact]
    public async Task Home_describes_the_person_for_name_searches()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var page = await Get(seed, "/");

        Assert.StartsWith(seed.Profile.Name + " — ", page.Title);
        Assert.Equal(Root + "/", page.Url);
        Assert.True(page.Indexable);
        Assert.Equal(Root + "/og-image.png", page.Image);
        Assert.DoesNotContain("*", page.Description);
        Assert.InRange(page.Description.Length, 1, 160);

        var person = Node(page, "Person");
        Assert.Equal(seed.Profile.Name, (string?)person["name"]);
        Assert.Equal(string.Join(' ', seed.Profile.Name.Split(' ')[..2]), (string?)person["alternateName"]);
        var sameAs = person["sameAs"]!.AsArray().Select(value => (string?)value).ToArray();
        Assert.Contains(seed.Profile.Github, sameAs);
        Assert.Contains(seed.Profile.Linkedin, sameAs);
        Assert.Equal(Root + "/#person", (string?)Node(page, "ProfilePage")["mainEntity"]!["@id"]);
    }

    [Fact]
    public async Task Sections_ignore_query_and_trailing_slash()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var page = await Get(seed, "/proyectos/?utm_source=linkedin");

        Assert.Equal("Proyectos · " + seed.Profile.Name, page.Title);
        Assert.Equal(Root + "/proyectos", page.Url);
        var trail = Node(page, "BreadcrumbList")["itemListElement"]!.AsArray();
        Assert.Equal(["Inicio", "Proyectos"], trail.Select(item => (string?)item!["name"]));
    }

    [Fact]
    public async Task Project_page_uses_its_content_and_uploaded_image()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var project = seed.Projects[0] with { Id = "mi proyecto", Image = "/uploads/0a1b2c.webp", Tags = ["C#", "SQL"] };
        var page = await Get(seed with { Projects = [project] }, "/proyectos/mi proyecto");

        Assert.Equal(project.Title + " · " + seed.Profile.Name, page.Title);
        Assert.Equal(Root + "/proyectos/mi%20proyecto", page.Url);
        Assert.Equal("article", page.Type);
        Assert.Equal(Root + "/uploads/0a1b2c.webp", page.Image);
        Assert.Equal(project.Title, page.ImageAlt);
        var work = Node(page, "CreativeWork");
        Assert.Equal("C#, SQL", (string?)work["keywords"]);
        Assert.Equal(Root + "/#person", (string?)work["author"]!["@id"]);
    }

    [Fact]
    public async Task Ignores_images_that_were_not_uploaded()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var project = seed.Projects[0] with { Image = "https://elsewhere.example/x.png" };

        var page = await Get(seed with { Projects = [project] }, "/proyectos/" + project.Id);

        Assert.Equal(Root + "/og-image.png", page.Image);
    }

    [Fact]
    public async Task Journal_entry_is_an_article_with_its_date()
    {
        var seed = PortfolioValidatorTests.LoadSeed();
        var entry = seed.Entries[0];

        var page = await Get(seed, "/diario/" + entry.Id);

        Assert.Equal(DateOnly.Parse(entry.Date), page.Published);
        var article = Node(page, "BlogPosting");
        Assert.Equal(entry.Title, (string?)article["headline"]);
        Assert.Equal(entry.Date, (string?)article["datePublished"]);
    }

    [Theory]
    [InlineData("/no-existe", "No encontrado")]
    [InlineData("/proyectos/no-existe", "No encontrado")]
    [InlineData("/editor", "Editor")]
    public async Task Unknown_routes_and_the_editor_are_not_indexed(string path, string name)
    {
        var seed = PortfolioValidatorTests.LoadSeed();

        var page = await Get(seed, path);

        Assert.False(page.Indexable);
        Assert.Null(page.Url);
        Assert.Null(page.StructuredData);
        Assert.Equal(name + " · " + seed.Profile.Name, page.Title);
    }

    [Fact]
    public void Summaries_are_plain_and_cut_at_a_word()
    {
        var text = "Diseño *APIs* que\nresponden. " + string.Join(' ', Enumerable.Repeat("palabra", 40));

        var summary = SeoService.Summarize(text);

        Assert.StartsWith("Diseño APIs que responden. palabra", summary);
        Assert.EndsWith("palabra…", summary);
        Assert.True(summary.Length <= 160);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("proyectos", "/proyectos")]
    [InlineData("/diario/x/?a=1#b", "/diario/x")]
    public void Normalizes_paths(string? path, string expected)
        => Assert.Equal(expected, SeoService.NormalizePath(path));

    private static Task<PageMetadata> Get(PortfolioData portfolio, string path)
        => new SeoService(new FixedRepository(portfolio)).GetPageAsync(path, Site, CancellationToken.None);

    private static JsonObject Node(PageMetadata page, string type)
        => page.StructuredData!["@graph"]!.AsArray().OfType<JsonObject>().Single(node => (string?)node["@type"] == type);

    private sealed class FixedRepository(PortfolioData portfolio) : IPortfolioRepository
    {
        public Task<PortfolioData> GetAsync(CancellationToken cancellationToken) => Task.FromResult(portfolio);

        public Task<bool> TrySaveAsync(PortfolioData portfolio, string expectedRevision, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
