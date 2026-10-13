namespace Portfolio.Errors.Catalog;

public static class ResponseMessages
{
    public static string Success(string method, string endpoint, string language)
        => (method, endpoint, language == "en") switch
        {
            ("PUT", "/api/portfolio", true) => "Content saved successfully.",
            ("PUT", "/api/portfolio", false) => "Contenido guardado correctamente.",
            ("POST", "/api/uploads", true) => "Image uploaded successfully.",
            ("POST", "/api/uploads", false) => "Imagen subida correctamente.",
            (_, _, true) => "Request completed successfully.",
            _ => "Solicitud completada correctamente."
        };
}
