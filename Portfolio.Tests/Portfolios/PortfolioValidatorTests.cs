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
        var portfolio = seed with { Experience = [seed.Experience[0] with { End = "" }, .. seed.Experience[1..]] };

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
    public void Rejects_projects_linked_to_an_unknown_position()
    {
        var seed = LoadSeed();
        var project = seed.Projects[0] with { ExperienceId = "empresa-inexistente" };

        Assert.Equal("Un proyecto está asociado a una experiencia que no existe.",
            _validator.Validate(seed with { Projects = [project] }));
    }

    [Fact]
    public void Seed_links_professional_projects_to_existing_positions()
    {
        var seed = LoadSeed();
        var positions = seed.Experience.Select(item => item.Id).ToHashSet();

        var professional = seed.Projects.Where(project => project.ExperienceId.Length > 0).ToArray();

        Assert.NotEmpty(professional);
        Assert.All(professional, project => Assert.Contains(project.ExperienceId, positions));
    }

    [Theory]
    [InlineData("")]
    [InlineData("+34 691 649 298")]
    [InlineData("+34 (691) 64-92-98")]
    public void Accepts_empty_or_well_formed_phones(string phone)
    {
        var seed = LoadSeed();

        Assert.Null(_validator.Validate(seed with { Profile = seed.Profile with { Phone = phone } }));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("691 649 298 ext. 2")]
    [InlineData("34+691649298")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_malformed_phones(string phone)
    {
        var seed = LoadSeed();

        Assert.Equal("Introduce un teléfono válido, por ejemplo +34 600 000 000.",
            _validator.Validate(seed with { Profile = seed.Profile with { Phone = phone } }));
    }

    [Fact]
    public void Rejects_missing_collections()
    {
        var seed = LoadSeed();

        Assert.Equal("El contenido está incompleto.", _validator.Validate(seed with { Education = null! }));
    }
}
