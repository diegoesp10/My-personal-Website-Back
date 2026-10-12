using System.Text.Json;
using Portfolio.Application.Portfolios;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Tests.Portfolios;

public sealed class PortfolioValidatorTests
{
    private readonly PortfolioValidator _validator = new();

    internal static PortfolioData LoadSeed()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seed.json"));
        return JsonSerializer.Deserialize<PortfolioData>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public void Seed_content_is_valid()
    {
        var seed = LoadSeed();

        Assert.Null(_validator.Validate(seed));
        Assert.NotEmpty(seed.Experience);
        Assert.NotEmpty(seed.Education);
        Assert.Contains(seed.Projects, project => project.Featured);
    }

    [Theory]
    [InlineData("2025/10", "")]
    [InlineData("2025-13", "")]
    [InlineData("2025-10", "mayo")]
    public void Rejects_experience_dates_outside_year_month_format(string start, string end)
    {
        var seed = LoadSeed();
        var portfolio = seed with { Experience = [seed.Experience[0] with { Start = start, End = end }] };

        Assert.Equal("Las fechas de la experiencia deben tener el formato AAAA-MM.", _validator.Validate(portfolio));
    }

    [Fact]
    public void Rejects_experience_that_ends_before_it_starts()
    {
        var seed = LoadSeed();
        var portfolio = seed with { Experience = [seed.Experience[0] with { Start = "2025-10", End = "2025-09" }] };

        Assert.Equal("La fecha de fin de una experiencia no puede ser anterior al inicio.", _validator.Validate(portfolio));
    }

    [Fact]
    public void Accepts_current_experience_without_end()
    {
        var seed = LoadSeed();
        var portfolio = seed with { Experience = [seed.Experience[0] with { End = "" }] };

        Assert.Null(_validator.Validate(portfolio));
    }

    [Fact]
    public void Rejects_project_links_with_unsafe_scheme()
    {
        var seed = LoadSeed();
        var project = seed.Projects[0] with { Links = [new ProjectLink { Label = "Demo", Url = "javascript:alert(1)" }] };

        Assert.Equal("Los enlaces deben empezar por https:// o http://.", _validator.Validate(seed with { Projects = [project] }));
    }

    [Fact]
    public void Rejects_project_links_without_label()
    {
        var seed = LoadSeed();
        var project = seed.Projects[0] with { Links = [new ProjectLink { Label = " ", Url = "https://example.com" }] };

        Assert.Equal("Revisa los títulos, fechas, etiquetas, enlaces y métricas.", _validator.Validate(seed with { Projects = [project] }));
    }

    [Fact]
    public void Rejects_duplicated_experience_identifiers()
    {
        var seed = LoadSeed();
        var portfolio = seed with { Experience = [seed.Experience[0], seed.Experience[0]] };

        Assert.Equal("Cada elemento debe tener un identificador único.", _validator.Validate(portfolio));
    }

    [Fact]
    public void Rejects_missing_collections()
    {
        var seed = LoadSeed();

        Assert.Equal("El contenido está incompleto.", _validator.Validate(seed with { Education = null! }));
    }
}
