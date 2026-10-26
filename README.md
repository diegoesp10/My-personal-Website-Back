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
| `PUT /api/portfolio` | Guarda el contenido. Solo editor local (ver abajo); `409` si otra pestaña guardó antes. Valida enlaces (solo http/https), correo, teléfono (para enlaces `tel:`), fechas de experiencia y límites de tamaño. |
| `POST /api/uploads` | Sube PNG, JPEG o WebP de hasta 8 MB, validando la firma del archivo. Solo editor local. |
| `GET /uploads/{nombre}` | Imágenes subidas, con caché inmutable (los nombres son aleatorios). |
| `GET /api/cv?language=` | CV en PDF del idioma pedido, como descarga (`CV_Diego_Espina_ES.pdf`, `CV_Diego_Espina_EN.pdf`). Los PDF viven en `cv/` y se publican con la imagen; para actualizarlos, sustituye el archivo y vuelve a construir. `404` si falta el del idioma. |
| `GET /api/github` | Repositorios públicos, lenguajes y commits recientes de GitHub. Caché de 30 min; si GitHub falla sirve el último resultado (`stale: true`) y espera antes de reintentar. Siempre `200`: `available: false` si no hay datos. |
| `GET /sitemap.xml` | Sitemap con las páginas, proyectos y entradas del diario. |
| `GET /api/seo/page?path=` | Metadatos de una ruta del frontal: título, descripción, dirección canónica, imagen para compartir y datos schema.org (`Person`, `WebSite`, página, migas de pan, proyecto o artículo). El frontal los aplica al navegar. Las rutas desconocidas y el editor salen como no indexables. |
| `GET /api/seo/head?path=` | Los mismos metadatos como etiquetas HTML. Nginx los incluye (SSI) en el `<head>` de cada página, así que buscadores y vistas previas de enlaces los leen sin ejecutar JavaScript. Solo accesible desde Nginx. |
| `POST /api/visits` | Registra una página vista (`{ path, referrer }`), enviada por el frontal con `sendBeacon`. Siempre `204`. Ver «Estadísticas de visitas». |
| `GET /api/visitors` | Resumen público y anónimo: visitas, visitantes, personas ahora, últimos 30 días, ciudades con coordenadas redondeadas, países, páginas, sitios de origen y últimas visitas. |
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
| `Portfolio:DataPath` (`Portfolio__DataPath`) | `App_Data/` | Datos de ejecución: visitas, historial de errores, base de geolocalización y, si no hay carpeta de contenido, también el contenido. |
| `Portfolio:ContentPath` (`Portfolio__ContentPath`) | vacío (= `DataPath`) | Carpeta del contenido (`portfolio*.json` y `uploads/`). El perfil local la apunta a `../content`, la carpeta versionada; en Docker y producción va vacía y el contenido vive en el volumen. |
| `Site:BaseUrl` | `https://diegoespinarodriguez.com` | Origen público para el sitemap, las direcciones canónicas y los datos schema.org. |
| `Site:DefaultImage` | `/og-image.png` | Imagen para compartir (1200×630) cuando la página no tiene una propia; la sirve el frontal. |
| `GitHub:User` | `diegoesp10` | Usuario cuya actividad pública se muestra. |
| `GitHub:Enabled` | `true` | `false` desactiva las llamadas a GitHub (lo usan las pruebas). |
| `GitHub:Token` (`GitHub__Token`) | vacío | Token opcional de solo lectura para ampliar el límite de GitHub. No hace falta con la caché. |
| `RateLimiting:PermitLimit` / `Window` | `300` / `00:01:00` | Peticiones por IP y ventana. |
| `ReverseProxy:Enabled` | `false` | Confía en `X-Forwarded-For/Proto` del proxy. Docker lo activa, porque la API solo es accesible a través de Nginx y de loopback. |
| `ReverseProxy:ClientIpHeader` | `X-Forwarded-For` | Cabecera con la IP del visitante. Detrás de Cloudflare, `CF-Connecting-IP` (en Docker: `PORTFOLIO_CLIENT_IP_HEADER`). |
| `Visitors:Enabled` | `true` | Activa las estadísticas de visitas. |
| `Visitors:UseProxyLocation` | `false` | Usa las cabeceras de ubicación de Cloudflare (`CF-IPCountry`, `cf-ipcity`, `cf-iplatitude`, `cf-iplongitude`). Solo si todo el tráfico pasa por Cloudflare (en Docker: `PORTFOLIO_PROXY_LOCATION=true`). |
| `Visitors:GeoDatabasePath` | `geo/dbip-city-lite.mmdb` en la carpeta de datos | Base MMDB local para ubicar direcciones sin enviarlas a nadie. Opcional. |

## SEO

Cada página llega al navegador con su propio título, descripción, dirección canónica, imagen para compartir y datos estructurados, generados por `SeoService` a partir del contenido de su idioma:

- Las direcciones llevan el idioma como primer segmento (`/es/proyectos`, `/en/proyectos`); una ruta sin prefijo se trata como español. `SiteRoutes` reúne esa convención.
- Cada página indexable enlaza sus traducciones con `hreflang` (más `x-default`, la versión en español) y declara su `og:locale` (`es_ES` o `en_US`). Un proyecto o una entrada que solo existe en un idioma solo enlaza ese idioma.

- La portada es una `ProfilePage` de la persona (`Person`), con su nombre completo, el nombre corto («Diego Espina»), el puesto, la ubicación, los perfiles de GitHub y LinkedIn (`sameAs`) y las tecnologías de las competencias (`knowsAbout`).
- Los proyectos son `CreativeWork` y las entradas del diario, `BlogPosting` con su fecha; todas las páginas interiores llevan migas de pan.
- Las descripciones salen del texto de cada página, sin marcas de énfasis y recortadas a 160 caracteres.
- La imagen es la del proyecto si tiene una subida; si no, la imagen general del frontal.
- Las rutas que no existen y el editor llevan `noindex` y no tienen dirección canónica.

Las secciones fijas del frontal están en `SeoService`, con sus textos en español e inglés, y deben coincidir con su router, igual que las de `SitemapService`. El sitemap lista cada página en cada idioma con sus alternativas (`xhtml:link`).

## Estadísticas de visitas

La página `/visitas` del frontal muestra un globo con las ciudades desde las que se visita la web. El diseño cumple el RGPD sin necesidad de banner de cookies:

- **No se guarda ninguna dirección IP.** La IP solo se usa al recibir la visita para obtener la ciudad aproximada. Se guardan recuentos por día, por ciudad (coordenadas redondeadas a un decimal, unos 10 km), por página y por sitio de origen (solo el dominio), y las últimas visitas sin identificador. Todo va en `visitors.json`, en la carpeta de datos.
- **Visitantes únicos sin cookies.** Se usa un HMAC de IP y navegador cuya clave es aleatoria, cambia cada día y solo vive en memoria: un visitante cuenta una vez al día y no se puede seguir entre días ni recuperar su IP. «Ahora mismo» son los visitantes de los últimos 5 minutos.
- **Opt-out y filtrado.** Se respetan `DNT: 1` y `Sec-GPC: 1`. Se descartan bots y navegadores automatizados, el editor y las rutas que no son páginas públicas.
- **Ubicación sin terceros.** Detrás de Cloudflare se usan sus cabeceras de ubicación: activa «Add visitor location headers» en *Rules › Transform Rules › Managed Transforms*. En cualquier otro alojamiento se puede usar la base gratuita DB-IP City Lite (CC BY 4.0) con `scripts/Update-GeoDatabase.ps1`, que la descarga en `App_Data/geo`; `Start-Docker.ps1` la copia al volumen. Sin ninguna de las dos, las visitas se cuentan sin ubicación.

Las cifras se acumulan en memoria y se guardan cada 30 segundos y al parar el servidor. Esta versión está pensada para una sola instancia, como el resto de la persistencia en archivos.

## Docker

Con Docker Desktop en modo Linux:

```powershell
.\Start-Docker.ps1
```

Construye la imagen, crea si faltan la red `diego-portfolio-net` y el volumen `diego-portfolio-data`, importa `content/` al volumen **solo si está vacío** (y la base de geolocalización de `App_Data/geo`, si existe) y espera a que el contenedor esté sano. La API queda en <http://127.0.0.1:8081/api/health> (`$env:PORTFOLIO_API_PORT` cambia el puerto). Opciones: `-SkipBuild` reutiliza la imagen; `-ReplaceData` sustituye el contenido del volumen por el de `content/` y guarda el anterior en `backups/` dentro del volumen.

| Imagen | Contenedor | Red | Volumen |
| --- | --- | --- | --- |
| `diego-portfolio-api:local` | `diego-portfolio-api` (alias `api`) | `diego-portfolio-net` | `diego-portfolio-data` → `/app/data` |

El contenedor se ejecuta en Production, sin root, con sistema de archivos de solo lectura y sin capacidades. La imagen solo lleva `content/portfolio*.json` como contenido inicial de un volumen vacío; `App_Data` y las imágenes subidas nunca entran en ella. El contenedor web del frontal se une a la misma red y llega a esta API como `api`.

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
node scripts/test-errors.mjs
```

- `Portfolio.Tests`: validación del contenido (incluido el de `content/`), concurrencia por revisión, firmas de imagen, sitemap, metadatos SEO y servicio de GitHub (caché, modo degradado, espera entre reintentos, agregación de lenguajes).
- `scripts/test-api.mjs`: arranca la API compilada en el puerto 5082 con datos aislados en `.test-data/` y comprueba ETag/304, sitemap, cabeceras, validaciones, protección del editor, conflictos, imágenes, metadatos SEO (fragmento HTML escapado y JSON), visitas anónimas (ubicación por cabeceras, Do Not Track, bots, que la IP no llegue al disco), persistencia tras reinicio y modo Production.

## Datos y copias

El contenido publicado está versionado en `content/`: `portfolio.json` (español), `portfolio.en.json` (inglés), con los mismos identificadores, y `uploads/` con las imágenes que sube el editor. En local, la API lee y guarda ahí directamente (`Portfolio:ContentPath`), así que cada cambio del editor es un cambio de Git: se revisa en la PR y se publica al fusionar en `main`. Cada guardado sustituye el archivo de su idioma de forma atómica.

`.gitignore` solo admite en `content/` los archivos de idioma y las imágenes con el nombre que genera la API (`<32 hex>.png|jpg|webp`); cualquier otro archivo, como el temporal de un guardado, se queda fuera. Las imágenes se publican tal cual: quita los metadatos (ubicación GPS, cámara) antes de subirlas. `App_Data/` sigue fuera de Git y guarda solo datos locales de ejecución: visitas, historial de errores, base de geolocalización y copias. En Docker y en el servidor, contenido y datos viven en el volumen `diego-portfolio-data`, que debe incluirse en las copias.

La imagen lleva `content/portfolio*.json` como semilla (`seed.json`, `seed.en.json`): al arrancar, el idioma que aún no tiene archivo se crea desde ella sin tocar los demás. Esta versión está pensada para **una sola instancia**; para varias, sustituir la persistencia JSON por base de datos y almacenamiento de objetos.

## Publicación

**diegoespinarodriguez.com** se sirve desde una máquina virtual Ubuntu con Docker (Azure, tamaño B2ats_v2) detrás de **Cloudflare Tunnel**. El servidor no tiene ningún puerto abierto salvo SSH: las visitas solo llegan por el túnel, que conecta hacia fuera con Cloudflare. Por eso la API confía en `CF-Connecting-IP` y en las cabeceras de ubicación de Cloudflare. La dirección del servidor no figura en los repositorios: se usa el alias SSH `diego-portfolio` de `~/.ssh/config`.

```powershell
.\scripts\Deploy-Server.ps1                  # construye las dos imágenes en local, las sube y reinicia la web
.\scripts\Deploy-Server.ps1 -SkipBuild       # sube las imágenes ya construidas
.\scripts\Deploy-Server.ps1 -ReplaceData     # sustituye el contenido del servidor por content/ (copia el anterior a backups/)
.\scripts\Deploy-Server.ps1 -SetTunnelToken  # pide el token del túnel, lo guarda en el servidor y arranca el túnel
.\scripts\Deploy-Server.ps1 -Backup          # descarga una copia de los datos del servidor
.\scripts\Deploy-Server.ps1 -ContentOnly     # publica content/ ahora, igual que el CI/CD (copia el anterior a backups/)
```

**Publicar contenido (un post del diario, un proyecto, una imagen).** En producción el editor está desactivado, así que el contenido se edita en local y llega a producción con Git:

1. Arranca la API (`Start-Api.ps1`) y el frontal (`npm run dev`) y edita en <http://127.0.0.1:5173/editor>. Guardar escribe en `content/`.
2. Haz commit de `content/` en `develop` y abre la PR a `main`. El diff muestra el cambio, y «Backend checks» valida el contenido con las pruebas y `pack-content.mjs`, que comprueba además que toda imagen referenciada esté versionada.
3. Al fusionar y aprobar el despliegue en `production`, el job de despliegue publica la API y después el contenido (`deploy-content.sh`). Si algún idioma cambió, guarda la versión anterior en `backups/` del volumen, coloca la nueva con un renombrado atómico sin parar la API y comprueba que la API la sirve; si no la sirve, restaura la anterior y el workflow falla. Si nada cambió, no toca el volumen. Las imágenes nuevas se añaden y las existentes nunca se sobrescriben ni se borran.

La revisión de cada idioma (su ETag) se calcula al publicar a partir del propio contenido, de modo que cualquier cambio llega al navegador aunque se edite el JSON a mano. `-ContentOnly` sigue el mismo camino desde tu equipo, para una corrección urgente entre merges; el siguiente merge vuelve a publicar lo que haya en `main`. Las visitas y el historial de errores del servidor nunca se tocan.

- `deploy/compose.yaml` define el despliegue del servidor: `api`, `web` (publicada solo en el loopback del servidor) y `tunnel` (`cloudflared`, activo cuando `~/portfolio/.env` contiene el token). `deploy/install.sh` se ejecuta allí: carga las imágenes, importa `content/` si el volumen no tiene contenido y arranca el compose.
- El primer despliegue importa `portfolio.json`, `portfolio.en.json` y `uploads`; desde ahí, el contenido lo publica el CI/CD. Las estadísticas de visitas solo existen en el servidor y nunca se sustituyen.
- En Cloudflare, los nombres de host públicos del túnel (`diegoespinarodriguez.com` y `www`) apuntan a `http://web:8080`. Para que el mapa de visitas tenga ubicación, activa «Add visitor location headers» en *Rules › Transform Rules › Managed Transforms*. No crees registros DNS con la IP del servidor.
- Para revisar la web publicada sin pasar por Cloudflare: `ssh -L 8080:127.0.0.1:8080 diego-portfolio` y abre <http://127.0.0.1:8080>. Registros: `ssh diego-portfolio "cd portfolio; docker compose logs --tail 100"`.
- Con CI/CD activo ([docs/CI-CD.md](docs/CI-CD.md)), cada merge a `main` publica la imagen en GHCR y, tras tu aprobación en el entorno `production`, la despliega por SSH junto con el contenido. Desde entonces, código y contenido se publican con CI/CD. `Deploy-Server.ps1 -Backup` y `-SetTunnelToken` son operaciones de mantenimiento. Atención: `-ReplaceData` también sube las imágenes locales, incluso con `-SkipBuild`; por tanto sustituye las publicadas desde `main` además del contenido. Para cambiar solo el contenido, usa `-ContentOnly`.
- El CI/CD despliega `api`, `web` y el contenido. Ni copia `deploy/compose.yaml` al servidor ni recrea `tunnel`, así que un cambio de versión de `cloudflared` se aplica a mano por SSH: actualizar la imagen en `~/portfolio/compose.yaml` y ejecutar `docker compose up -d tunnel`.
- Copia de seguridad: `.\scripts\Deploy-Server.ps1 -Backup` descarga el volumen del servidor (contenido, imágenes y visitas) a `App_Data/backups/servidor-<fecha>.tgz`. Hazla antes de cada `-ReplaceData` o `-ContentOnly`.

## Rama main protegida

`main` solo cambia mediante pull request desde `develop`. GitHub impide el push directo, forzar el historial y borrar la rama, y exige que pasen las comprobaciones («Backend checks») antes del merge. Publicar en producción pide dos confirmaciones: el merge de la PR y la aprobación del despliegue en el entorno `production`. En local, `.githooks/pre-push` pregunta antes de cada push y rechaza los push a `main`. Se activa una vez por clon con `git config core.hooksPath .githooks`. Desde una interfaz gráfica sin consola, el push se rechaza: hazlo desde la terminal.

## Estructura

```text
├── Portfolio.Api/              Controladores por funcionalidad, middleware y composición
├── Portfolio.Application/      Servicios, validación y contratos (Portfolios, Images, GitHub, Sitemaps, Seo, Visitors)
├── Portfolio.Errors/           Catálogo es/en, contratos de error, registro y correlación
├── Portfolio.Domain/           Modelos: perfil, proyectos, experiencia, formación, diario, GitHub
├── Portfolio.Infrastructure/   Repositorios JSON e imágenes, cliente HTTP de GitHub
├── Portfolio.Tests/            Pruebas xUnit
├── scripts/test-api.mjs        Prueba de integración de la API compilada
├── content/                    Contenido publicado (es, en) e imágenes; lo edita el editor local
├── .github/scripts/            Despliegue: imágenes por digest, contenido (pack-content.mjs, deploy-content.sh)
├── Dockerfile · compose.yaml   Imagen y contenedor de la API
└── Start-Api.ps1 · Start-Docker.ps1
```

## Contenido del perfil y proyectos

`profile.hasDrivingLicense` y `profile.hasOwnVehicle` son booleanos opcionales al importar contenido antiguo (por defecto `false`). Se editan desde Perfil y se muestran en Sobre mí, el CV y su versión imprimible cuando están activados. `project.experienceId` vincula el trabajo a una etapa profesional; vacío indica un proyecto personal.

El contenido incluye Azure y pipelines de CI/CD, uso diario de Claude y Codex y aprendizaje con LLMs locales. La ficha `peluqueria-lucia` describe la web pública, gestión y agenda del salón como proyecto en desarrollo; su composición decorativa utiliza `art: "salon"`. Los textos y competencias se gestionan desde el editor, no están incrustados en las páginas.

## CI/CD por SSH

El workflow `.github/workflows/ci-cd.yml` comprueba las PR y publica una imagen al actualizar `main`. El despliegue a la VM espera aprobación en el entorno `production`, usa secretos de GitHub y conserva los datos. Configuración, credenciales y activación en [docs/CI-CD.md](docs/CI-CD.md).

## Errores e idioma

`Portfolio.Errors` centraliza los códigos y mensajes. La API devuelve Problem Details con correlación y mantiene un historial privado en el volumen persistente. El editor local incorpora una pestaña Errores con consulta protegida y mensajes en es/en; Production no permite acceder al historial por HTTP.

`language` se envía en el cuerpo JSON, en GET como query y en multipart como campo. Español es el valor predeterminado. Elige el contenido que se lee o se guarda (`GET /api/portfolio?language=en`, `PUT` con `"language": "en"`) y el idioma de los mensajes de operación y error; guardar un idioma no modifica los demás. Contratos, retención y pruebas en [docs/ERRORS-AND-LANGUAGE.md](docs/ERRORS-AND-LANGUAGE.md).

La ficha del proyecto `portfolio` incluye diez apartados sobre arquitectura, VM Ubuntu en Azure, Docker Compose, Cloudflare Tunnel, integración continua, protección de main, aprobación de producción, imágenes en GHCR, publicación SSH y recuperación. `project.sections` es una colección aditiva y editable de `{ title, text }`, vacía por defecto en contenido anterior.
