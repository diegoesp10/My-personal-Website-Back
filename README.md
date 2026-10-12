# Diego Espina · API del portfolio

API en **ASP.NET Core 10** de [diegoespinarodriguez.com](https://diegoespinarodriguez.com). Sirve el contenido de la web, las imágenes, la actividad pública de GitHub y el sitemap. El frontal React vive en su propio repositorio: [My-Personal-WebSite](https://github.com/diegoesp10/My-Personal-WebSite).

## Arquitectura

Cuatro proyectos con la dirección de dependencias fijada por sus referencias:

```text
Portfolio.Domain         → sin dependencias del proyecto (modelos)
Portfolio.Application    → Domain (casos de uso, validación, contratos)
Portfolio.Infrastructure → Application (JSON en disco, imágenes, cliente de GitHub)
Portfolio.Api            → Application + Infrastructure (HTTP, middleware, composición)
Portfolio.Tests          → Application (xUnit)
```

`Program.cs` solo arranca el host; `Startup.cs` compone los módulos con `AddApplication`, `AddInfrastructure` y `AddApi`. Los controladores adaptan HTTP, los servicios coordinan reglas y repositorios, y los middleware centralizan errores y cabeceras. Las convenciones están en [AGENTS.md](AGENTS.md).

## Endpoints

| Método y ruta | Uso |
| --- | --- |
| `GET /api/portfolio` | Contenido público. Publica la revisión como `ETag` y responde `304` si no ha cambiado. |
| `PUT /api/portfolio` | Guarda el contenido. Solo editor local (ver abajo); `409` si otra pestaña guardó antes. |
| `POST /api/uploads` | Sube PNG, JPEG o WebP de hasta 8 MB, validando la firma del archivo. Solo editor local. |
| `GET /uploads/{nombre}` | Imágenes subidas, con caché inmutable (los nombres son aleatorios). |
| `GET /api/github` | Repositorios públicos, lenguajes y commits recientes de GitHub. Caché de 30 min; si GitHub falla sirve el último resultado (`stale: true`) y espera antes de reintentar. Siempre `200`: `available: false` si no hay datos. |
| `GET /sitemap.xml` | Sitemap con las páginas, proyectos y entradas del diario. |
| `GET /api/editor` | Indica si el editor local está disponible. |
| `GET /api/health` | Comprobación de salud para Docker. |
| `GET /api/openapi/v1.json` | Descripción OpenAPI. Solo en Development. |

Todas las respuestas llevan `nosniff`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` y CSP. Hay un límite de peticiones por IP (300/min por defecto) que responde `429` en JSON.

**Editor local.** Las escrituras solo se aceptan en Development, desde loopback, con Host local, cabecera `X-Portfolio-Editor: local` y Origin permitido. No es un panel de administración para Internet: en Production el editor y todas las escrituras están desactivados.

## Arrancar en local

Requisitos: SDK .NET 10.

```powershell
.\Start-Api.ps1
```

La API queda en <http://127.0.0.1:5080>. Para ver la web, arranca el frontal con `npm run dev` en su repositorio (redirige `/api`, `/uploads` y `/sitemap.xml` a este puerto) y abre <http://127.0.0.1:5173>; el editor está en `/editor`. Alternativamente, `scripts/Start-Portfolio.ps1` del frontal compila React, lo copia a `Portfolio.Api/wwwroot` y arranca esta API sirviendo todo desde el puerto 5080.

## Configuración

| Clave (variable de entorno) | Por defecto | Uso |
| --- | --- | --- |
| `Portfolio:DataPath` (`Portfolio__DataPath`) | `App_Data/` | Carpeta del contenido y las imágenes. |
| `Site:BaseUrl` | `https://diegoespinarodriguez.com` | Origen público para el sitemap. |
| `GitHub:User` | `diegoesp10` | Usuario cuya actividad pública se muestra. |
| `GitHub:Enabled` | `true` | `false` desactiva las llamadas a GitHub (lo usan las pruebas). |
| `GitHub:Token` (`GitHub__Token`) | vacío | Token opcional de solo lectura para ampliar el límite de GitHub. No hace falta con la caché. |
| `RateLimiting:PermitLimit` / `Window` | `300` / `00:01:00` | Peticiones por IP y ventana. |
| `ReverseProxy:Enabled` | `false` | Confía en `X-Forwarded-For/Proto` del proxy. Docker lo activa, porque la API solo es accesible a través de Nginx y de loopback. |

## Docker

Con Docker Desktop en modo Linux:

```powershell
.\Start-Docker.ps1
```

Construye la imagen, crea si faltan la red `diego-portfolio-net` y el volumen `diego-portfolio-data`, importa `App_Data` al volumen **solo si está vacío** y espera a que el contenedor esté sano. La API queda en <http://127.0.0.1:8081/api/health> (`$env:PORTFOLIO_API_PORT` cambia el puerto). Opciones: `-SkipBuild` reutiliza la imagen; `-ReplaceData` sustituye el contenido del volumen por el de `App_Data` y guarda el anterior en `backups/` dentro del volumen.

| Imagen | Contenedor | Red | Volumen |
| --- | --- | --- | --- |
| `diego-portfolio-api:local` | `diego-portfolio-api` (alias `api`) | `diego-portfolio-net` | `diego-portfolio-data` → `/app/data` |

El contenedor se ejecuta en Production, sin root, con sistema de archivos de solo lectura y sin capacidades. El contenido de `App_Data` nunca entra en la imagen. El contenedor web del frontal se une a la misma red y llega a esta API como `api`.

```powershell
docker compose ps
docker compose logs --tail 100
docker compose down          # conserva el volumen de datos
docker build --target test . # ejecuta las pruebas xUnit dentro de la build
```

## Pruebas

```powershell
dotnet test DiegoPortfolio.slnx
dotnet build DiegoPortfolio.slnx
node scripts/test-api.mjs
```

- `Portfolio.Tests`: validación del contenido (incluido `seed.json`), concurrencia por revisión, firmas de imagen, sitemap y servicio de GitHub (caché, modo degradado, espera entre reintentos, agregación de lenguajes).
- `scripts/test-api.mjs`: arranca la API compilada en el puerto 5082 con datos aislados en `.test-data/` y comprueba ETag/304, sitemap, cabeceras, validaciones, protección del editor, conflictos, imágenes, persistencia tras reinicio y modo Production.

## Datos y copias

Al primer arranque se copia `seed.json` a `App_Data/portfolio.json`; las ediciones posteriores se guardan ahí con sustitución atómica y las imágenes en `App_Data/uploads/`. `App_Data` no está en Git: cópiala entera para hacer backup. En Docker, el volumen `diego-portfolio-data` debe incluirse en las copias. Esta versión está pensada para **una sola instancia**; para varias, sustituir la persistencia JSON por base de datos y almacenamiento de objetos.

## Publicación

El dominio previsto es **diegoespinarodriguez.com**, con DNS en Cloudflare. Esta API necesita un host con .NET 10 o contenedores (por ejemplo, Azure App Service / Container Apps o un VPS con Docker detrás de Cloudflare Tunnel). Antes de publicar: definir `Site:BaseUrl`, mantener `ASPNETCORE_ENVIRONMENT=Production`, persistir `/app/data` y, si el tráfico llega a través de Cloudflare, propagar la IP real del cliente al límite de peticiones.

## Estructura

```text
├── Portfolio.Api/              Controladores por funcionalidad, middleware y composición
├── Portfolio.Application/      Servicios, validación y contratos (Portfolios, Images, GitHub, Sitemaps)
├── Portfolio.Domain/           Modelos: perfil, proyectos, experiencia, formación, diario, GitHub
├── Portfolio.Infrastructure/   Repositorios JSON e imágenes, cliente HTTP de GitHub
├── Portfolio.Tests/            Pruebas xUnit
├── scripts/test-api.mjs        Prueba de integración de la API compilada
├── seed.json                   Contenido inicial
├── Dockerfile · compose.yaml   Imagen y contenedor de la API
└── Start-Api.ps1 · Start-Docker.ps1
```
