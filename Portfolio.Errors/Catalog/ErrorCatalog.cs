namespace Portfolio.Errors.Catalog;

/// <summary>Stable machine codes and safe public messages, independent of HTTP and storage.</summary>
public sealed record ErrorDefinition(string Code, string Spanish, string English)
{
    public string Message(string language = "es") => language == "en" ? English : Spanish;
}

public static class ErrorCatalog
{
    private static readonly ErrorDefinition[] Definitions =
    [
        new("request.invalid", "La solicitud no es válida.", "The request is invalid."),
        new("request.language", "El idioma debe ser es o en.", "Language must be es or en."),
        new("request.forbidden", "No tienes acceso a esta operación.", "You do not have access to this operation."),
        new("request.not_found", "No se encontró el recurso solicitado.", "The requested resource was not found."),
        new("request.method", "Este método no está permitido.", "This method is not allowed."),
        new("request.too_large", "La solicitud supera el tamaño permitido.", "The request exceeds the size limit."),
        new("request.media_type", "El tipo de contenido no está permitido.", "The content type is not supported."),
        new("request.rate_limit", "Demasiadas solicitudes. Inténtalo de nuevo en un momento.", "Too many requests. Please try again shortly."),
        new("server.unexpected", "No se pudo completar la operación.", "The operation could not be completed."),
        new("visitors.persistence", "No se pudieron guardar las estadísticas de visitas.", "Visit statistics could not be saved."),
        new("visitors.corrupt_store", "El historial de visitas dañado se ha aislado para su recuperación.", "Damaged visit statistics were isolated for recovery."),
        new("github.unavailable", "No se pudo actualizar la actividad de GitHub.", "GitHub activity could not be refreshed."),
        new("server.unavailable", "El servicio no está disponible temporalmente.", "The service is temporarily unavailable."),
        new("portfolio.incomplete", "El contenido está incompleto.", "The content is incomplete."),
        new("portfolio.profile", "Nombre y especialidad son obligatorios.", "Name and specialisation are required."),
        new("portfolio.items", "Hay demasiados elementos.", "There are too many items."),
        new("portfolio.size", "El contenido supera el tamaño permitido.", "The content exceeds the size limit."),
        new("portfolio.text", "Los campos no pueden ser nulos ni superar 20.000 caracteres.", "Fields cannot be null or exceed 20,000 characters."),
        new("portfolio.identifiers", "Cada elemento debe tener un identificador único.", "Each item must have a unique identifier."),
        new("portfolio.fields", "Revisa los títulos, fechas, etiquetas, enlaces y métricas.", "Check titles, dates, tags, links and metrics."),
        new("portfolio.experience", "Cada experiencia necesita puesto y empresa.", "Each position needs a role and a company."),
        new("portfolio.experience_items", "Hay demasiados logros o tecnologías en una experiencia.", "There are too many achievements or technologies in a position."),
        new("portfolio.experience_dates", "Las fechas de la experiencia deben tener el formato AAAA-MM.", "Position dates must use the YYYY-MM format."),
        new("portfolio.experience_order", "La fecha de fin de una experiencia no puede ser anterior al inicio.", "A position end date cannot precede its start date."),
        new("portfolio.experience_reference", "Un proyecto está asociado a una experiencia que no existe.", "A project is associated with a position that does not exist."),
        new("portfolio.links", "Los enlaces deben empezar por https:// o http://.", "Links must start with https:// or http://."),
        new("portfolio.email", "Introduce un correo válido.", "Enter a valid email address."),
        new("portfolio.phone", "Introduce un teléfono válido, por ejemplo +34 600 000 000.", "Enter a valid phone number, for example +34 600 000 000."),
        new("portfolio.images", "Utiliza las imágenes subidas desde el editor.", "Use images uploaded through the editor."),
        new("portfolio.conflict", "El contenido ha cambiado. Recarga el editor antes de guardar.", "The content has changed. Reload the editor before saving."),
        new("image.required", "Selecciona una imagen.", "Select an image."),
        new("image.size", "La imagen debe ocupar entre 1 byte y 8 MB.", "The image must be between 1 byte and 8 MB."),
        new("image.format", "Formato no permitido. Utiliza PNG, JPEG o WebP.", "Unsupported format. Use PNG, JPEG or WebP."),
        new("portfolio.sections", "Revisa los títulos y textos de las secciones del proyecto.", "Check the titles and text of the project sections."),
    ];
    private static readonly IReadOnlyDictionary<string, ErrorDefinition> ByCode =
        Definitions.ToDictionary(error => error.Code, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, ErrorDefinition> BySpanish =
        Definitions.ToDictionary(error => error.Spanish, StringComparer.Ordinal);

    public static ErrorDefinition Get(string code) => ByCode.TryGetValue(code, out var error)
        ? error : ByCode["server.unexpected"];

    // Compatibility with existing Application validation contracts; never publish arbitrary exception text.
    public static ErrorDefinition FromMessage(string message, int status) =>
        BySpanish.TryGetValue(message, out var error) ? error : ForStatus(status);

    public static ErrorDefinition ForStatus(int status) => Get(status switch
    {
        400 => "request.invalid", 401 or 403 => "request.forbidden", 404 => "request.not_found",
        405 => "request.method", 409 => "portfolio.conflict", 413 => "request.too_large",
        415 => "request.media_type", 429 => "request.rate_limit", 503 => "server.unavailable",
        _ => "server.unexpected"
    });

    public static string Title(int status, string language) => (status, language == "en") switch
    {
        (400, true) => "Bad Request", (400, false) => "Solicitud no válida",
        (401, true) => "Unauthorized", (403, true) => "Forbidden", (401 or 403, false) => "Acceso denegado",
        (404, true) => "Not Found", (404, false) => "No encontrado",
        (405, true) => "Method Not Allowed", (405, false) => "Método no permitido",
        (409, true) => "Conflict", (409, false) => "Conflicto",
        (413, true) => "Content Too Large", (413, false) => "Solicitud demasiado grande",
        (415, true) => "Unsupported Media Type", (415, false) => "Tipo de contenido no permitido",
        (429, true) => "Too Many Requests", (429, false) => "Demasiadas solicitudes",
        (503, true) => "Service Unavailable", (_, true) => "Internal Server Error", (_, false) => "Error del servidor"
    };
}
