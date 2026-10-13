# Errores, idioma y diagnóstico privado

## Capas y responsabilidades

- `Portfolio.Errors` contiene el catálogo de códigos y mensajes es/en, el contrato `ApiProblem` y los contratos de registro y correlación. No depende de ASP.NET Core, almacenamiento ni Domain.
- `Portfolio.Application/Errors` coordina el registro mediante `IErrorReporter`. Un fallo del historial se comunica al logger sin sustituir el error original. Los validadores utilizan el catálogo compartido; la integración de GitHub registra sus fallos controlados y conserva su respuesta degradada.
- `Portfolio.Infrastructure/Errors` implementa `IErrorRegistry` en JSON dentro de `Portfolio:DataPath`. Serializa las escrituras concurrentes con un semáforo y sustituye el archivo de forma atómica.
- `Portfolio.Api/Features/Errors` adapta los resultados a HTTP, negocia el idioma, expone la correlación y protege la consulta. Startup compone las dependencias y el middleware; Program sigue reservado al arranque.

Los errores HTTP de `/api` y `/uploads` incluyen validación/model binding, permisos, recursos inexistentes, conflictos, límites y excepciones no controladas. Los fallos detectados después de comenzar una respuesta se registran y se aborta la conexión. Una desconexión normal del cliente no se convierte en un error de servidor. El historial recoge fallos de la aplicación; los errores emitidos por Cloudflare/Nginx o por el arranque antes de atender peticiones pertenecen a los logs de esos componentes.

## Contrato de idioma

Valores admitidos: `es` y `en`. Omitir `language` mantiene `es`. La cuota se comprueba antes de leer o validar cuerpos: un idioma inválido tampoco evita el limitador. Cuando se agota, 429 tiene prioridad sobre la validación y una petición cuyo cuerpo no se lee usa español; GET puede conservar un idioma válido de la query.

Un valor inválido, null explícito o una clave repetida devuelve 400 con código `request.language`; una petición que no permite leer el idioma usa el español predeterminado.

| Petición | Idioma |
| --- | --- |
| GET/HEAD | `?language=en` |
| PUT `/api/portfolio` | propiedad `language` del objeto JSON existente; no se envuelve el portfolio |
| POST `/api/visits` | `{ "path": "/", "referrer": "", "language": "en" }` |
| POST `/api/uploads` | campo `language` del formulario multipart junto a `file` |

Ejemplo de edición: `{ "language": "en", "revision": "...", "profile": { ... }, "projects": [ ... ], ... }`.

`PortfolioWriteRequest` y `VisitBeacon` declaran el campo de idioma en sus contratos HTTP; el mapeo al dominio excluye ese metadato.

Las respuestas JSON de objeto de los controladores añaden `language` y `message` sin cambiar los campos existentes. Por ejemplo, guardar responde con `message: "Content saved successfully."`; subir devuelve `url` y `message: "Image uploaded successfully."`. Las respuestas 204/304, archivos, sitemap, fragmentos HTML SEO y la descripción OpenAPI mantienen su formato. `Content-Language` indica el idioma del mensaje de operación y `X-Correlation-ID` identifica la petición en las rutas API. El ETag del portfolio distingue la variante inglesa (`-en`) de la española.

`language` es metadato de transporte: no se persiste en `portfolio.json`. No traduce automáticamente el perfil, proyectos, diario, metadatos SEO ni textos originales de GitHub. Tampoco equivale a `profile.languages`, que describe los idiomas profesionales del CV.

El cliente React añade este parámetro según el método, conserva el FormData original y acepta tanto Problem Details como el campo anterior `error`. Los errores de guardado o subida muestran su referencia para localizarla en el historial.

## Respuesta de error

Se devuelve `application/problem+json` siguiendo la estructura de [Problem Details (RFC 9457)](https://www.rfc-editor.org/rfc/rfc9457.html), con extensiones `code`, `traceId` y `language`. `error` conserva compatibilidad con el frontend anterior.

```json
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "The content has changed. Reload the editor before saving.",
  "code": "portfolio.conflict",
  "traceId": "identificador-generado-por-el-servidor",
  "language": "en",
  "error": "The content has changed. Reload the editor before saving."
}
```

Los consumidores deben interpretar `status` y `code`; los textos pueden evolucionar. Las excepciones inesperadas responden con un mensaje genérico. La respuesta de GitHub mantiene HTTP 200 cuando sirve datos en caché o el resultado degradado documentado; un fallo real de actualización registra `github.unavailable` con el mismo identificador de petición.

## Historial y acceso

Archivo privado: `<Portfolio:DataPath>/errors.json`; en Docker, `/app/data/errors.json`, dentro del volumen persistente `diego-portfolio-data`. El archivo no entra en la imagen ni se versiona. Las publicaciones de CI/CD conservan el volumen.

Cada entrada contiene id, fecha UTC, código, origen, estado cuando es HTTP, correlación, método, plantilla del endpoint o componente interno y tipo de excepción cuando procede. Los fallos de guardado de visitas y la recuperación de estadísticas dañadas también se registran; las tareas internas tienen estado HTTP null. Se guarda la plantilla, nunca la URL suministrada por el cliente. No se almacenan query strings, cuerpos, archivos subidos, cabeceras, IP, contraseñas, tokens, mensajes ni stack traces de excepciones. Los mensajes visibles se obtienen del catálogo al consultar el historial en es/en.

`GET /api/editor/errors?language=en&limit=50` exige API en Development, IP loopback, host permitido, `X-Portfolio-Editor: local` y Origin permitido si se envía. **Production rechaza esta consulta, incluso con ese header.** Se usa la misma política local en las operaciones del editor. No existe un endpoint público para volcar el historial ni aceptar errores arbitrarios.

Desde el frontend local (`Start-Api.ps1` y `npm run dev`), abrir `/editor` y la pestaña **Errores**. Se puede actualizar la consulta y elegir el idioma del catálogo. Para investigar producción, descargar primero una copia del volumen mediante el procedimiento de backup por SSH y revisarla privadamente; no activar el editor en la VM pública.

Valores por defecto, configurables mediante `ErrorRegistry` o sus variables de entorno:

```json
"ErrorRegistry": {
  "MaximumEntries": 1000,
  "RetentionDays": 14
}
```

El registro conserva como máximo esos elementos y descarta los vencidos al escribir. Las consultas filtran también las entradas vencidas y limitan la respuesta a un máximo de 200. Si el historial existente está corrupto, se conserva para diagnóstico y no se sobrescribe silenciosamente. El fallo de escritura no reemplaza la respuesta original. Esta persistencia está diseñada para una instancia; para varias, implementar `IErrorRegistry` en almacenamiento compartido.

## Comprobaciones

```powershell
dotnet build DiegoPortfolio.slnx
dotnet test DiegoPortfolio.slnx
node scripts/test-api.mjs
node scripts/test-errors.mjs
```

Las pruebas usan datos aislados. Cubren concurrencia y retención, reinicio, catálogo seguro, es/en en JSON/GET/multipart, respuestas de éxito, ETag por idioma, permisos, conflictos, límites, excepciones, privacidad, consulta bloqueada en Production y un historial inaccesible/corrupto. `test-errors.mjs` forma parte del workflow del backend.
