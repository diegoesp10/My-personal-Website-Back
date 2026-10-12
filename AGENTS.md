# Convenciones del backend

- Este repositorio contiene solo la API. El frontal React vive en el repositorio My-Personal-WebSite (carpeta hermana `Mi Web Front`). `Portfolio.Api/wwwroot` es una salida generada que solo escribe el script de vista local integrada del frontal; no se versiona ni se edita a mano.
- En .NET, `Program.cs` contiene únicamente el arranque del host. La composición de servicios y del pipeline se realiza en `Startup.cs`, mediante extensiones de registro explícitas.
- Mantener proyectos separados para Domain, Application, Infrastructure y API. Domain no depende de otras capas; Application depende de Domain; Infrastructure implementa los contratos de Application; API compone las dependencias.
- Organizar cada capa por funcionalidad. No colocar rutas, validación, lógica de negocio, acceso a archivos o reglas de acceso en Program o Startup.
- Usar inyección de dependencias por constructor y contratos explícitos. No resolver servicios manualmente con un service locator ni capturar dependencias en variables globales.
- Los controladores adaptan HTTP a los casos de uso; los servicios coordinan reglas y repositorios; los repositorios gestionan persistencia. Las llamadas a servicios externos (GitHub) se hacen en Infrastructure detrás de un contrato de Application. Centralizar errores y cabeceras en middleware.
- Preservar contratos HTTP y datos existentes en las refactorizaciones. Los campos nuevos del contenido deben ser aditivos y tener valor por defecto. Si cambia el modelo, actualizar `seed.json`, los tipos de `src/lib/types.ts` del frontal y las rutas públicas de `SitemapService` cuando cambie el router del frontal.
- Ejecutar `dotnet test`, `node scripts/test-api.mjs` y reconstruir los contenedores afectados (`Start-Docker.ps1`) antes de entregar.
