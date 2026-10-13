using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Portfolio.Domain.Portfolios;

namespace Portfolio.Application.Portfolios;

public sealed class PortfolioValidator : IPortfolioValidator
{
    private const int MaximumListItems = 20;
    private const int MaximumProjectLinks = 6;

    public string? Validate(PortfolioData? portfolio)
    {
        if (portfolio?.Profile is null || portfolio.Projects is null || portfolio.Skills is null
            || portfolio.Entries is null || portfolio.Metrics is null
            || portfolio.Experience is null || portfolio.Education is null)
            return "El contenido está incompleto.";

        if (string.IsNullOrWhiteSpace(portfolio.Profile.Name) || string.IsNullOrWhiteSpace(portfolio.Profile.Role))
            return "Nombre y especialidad son obligatorios.";

        if (portfolio.Projects.Length > 100 || portfolio.Entries.Length > 500
            || portfolio.Skills.Length > 30 || portfolio.Metrics.Length > 12
            || portfolio.Experience.Length > 50 || portfolio.Education.Length > 30)
            return "Hay demasiados elementos.";

        var serialized = JsonSerializer.Serialize(portfolio);
        if (serialized.Length > 1_000_000) return "El contenido supera el tamaño permitido.";

        using var document = JsonDocument.Parse(serialized);
        if (HasInvalidText(document.RootElement))
            return "Los campos no pueden ser nulos ni superar 20.000 caracteres.";

        return ValidateIdentifiers(portfolio)
            ?? ValidateItems(portfolio)
            ?? ValidateExperience(portfolio)
            ?? ValidateLinks(portfolio)
            ?? ValidateEmail(portfolio.Profile.Email)
            ?? ValidatePhone(portfolio.Profile.Phone)
            ?? ValidateImages(portfolio);
    }

    private static string? ValidateIdentifiers(PortfolioData portfolio)
    {
        var collections = new[] {
            portfolio.Projects.Select(item => item.Id), portfolio.Entries.Select(item => item.Id),
            portfolio.Skills.Select(item => item.Id), portfolio.Metrics.Select(item => item.Id),
            portfolio.Experience.Select(item => item.Id), portfolio.Education.Select(item => item.Id)
        };

        return collections.Any(ids => ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Count())
            ? "Cada elemento debe tener un identificador único." : null;
    }

    private static string? ValidateItems(PortfolioData portfolio)
    {
        var invalid = portfolio.Projects.Any(item => string.IsNullOrWhiteSpace(item.Title)
                || item.Tags.Length > MaximumListItems || item.Highlights.Length > MaximumListItems
                || item.Links.Length > MaximumProjectLinks
                || item.Links.Any(link => string.IsNullOrWhiteSpace(link.Label) || string.IsNullOrWhiteSpace(link.Url)))
            || portfolio.Skills.Any(item => string.IsNullOrWhiteSpace(item.Title) || item.Tags.Length > MaximumListItems)
            || portfolio.Entries.Any(item => string.IsNullOrWhiteSpace(item.Title)
                || !DateOnly.TryParseExact(item.Date, "yyyy-MM-dd", out _))
            || portfolio.Metrics.Any(item => string.IsNullOrWhiteSpace(item.Value) || string.IsNullOrWhiteSpace(item.Label))
            || portfolio.Education.Any(item => string.IsNullOrWhiteSpace(item.Title));

        return invalid ? "Revisa los títulos, fechas, etiquetas, enlaces y métricas." : null;
    }

    private static string? ValidateExperience(PortfolioData portfolio)
    {
        foreach (var item in portfolio.Experience)
        {
            if (string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.Company))
                return "Cada experiencia necesita puesto y empresa.";
            if (item.Highlights.Length > MaximumListItems || item.Stack.Length > MaximumListItems * 2)
                return "Hay demasiados logros o tecnologías en una experiencia.";
            if (!TryParseMonth(item.Start, out var start) || item.End.Length > 0 && !TryParseMonth(item.End, out _))
                return "Las fechas de la experiencia deben tener el formato AAAA-MM.";
            if (item.End.Length > 0 && TryParseMonth(item.End, out var end) && end < start)
                return "La fecha de fin de una experiencia no puede ser anterior al inicio.";
        }

        var positions = portfolio.Experience.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (portfolio.Projects.Any(project => project.ExperienceId.Length > 0 && !positions.Contains(project.ExperienceId)))
            return "Un proyecto está asociado a una experiencia que no existe.";

        return null;
    }

    private static string? ValidateLinks(PortfolioData portfolio)
    {
        var links = new[] { portfolio.Profile.Github, portfolio.Profile.Linkedin }
            .Concat(portfolio.Projects.Select(item => item.Url))
            .Concat(portfolio.Projects.SelectMany(item => item.Links.Select(link => link.Url)))
            .Concat(portfolio.Experience.Select(item => item.CompanyUrl));

        return links.Any(link => link.Length > 2048 || link.Length > 0
            && (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            ? "Los enlaces deben empezar por https:// o http://." : null;
    }

    private static string? ValidateEmail(string email) => email.Length > 254
        || email.Any(character => character is '\r' or '\n')
        || email.Length > 0 && !MailAddress.TryCreate(email, out _)
            ? "Introduce un correo válido." : null;

    // Used in tel: links: digits with an optional leading +, and spaces, dots, dashes or brackets to group them.
    private static string? ValidatePhone(string phone) => phone.Length > 0
        && (phone.Length > 32 || phone.Count(char.IsAsciiDigit) is < 6 or > 15
            || phone.Where((character, index) => !(char.IsAsciiDigit(character) || character is ' ' or '.' or '-' or '(' or ')'
                || character == '+' && index == 0)).Any())
            ? "Introduce un teléfono válido, por ejemplo +34 600 000 000." : null;

    private static string? ValidateImages(PortfolioData portfolio)
    {
        var images = new[] { portfolio.Profile.Portrait }.Concat(portfolio.Projects.Select(item => item.Image));
        return images.Any(image => image.Length > 0 && (!image.StartsWith("/uploads/", StringComparison.Ordinal)
            || image.Contains("..") || image.IndexOfAny(['?', '#', '\\']) >= 0))
                ? "Utiliza las imágenes subidas desde el editor." : null;
    }

    private static bool TryParseMonth(string value, out DateOnly month)
        => DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);

    private static bool HasInvalidText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => true,
        JsonValueKind.String => element.GetString()!.Length > 20_000,
        JsonValueKind.Array => element.EnumerateArray().Any(HasInvalidText),
        JsonValueKind.Object => element.EnumerateObject().Any(property => HasInvalidText(property.Value)),
        _ => false
    };
}
