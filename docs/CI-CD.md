# CI/CD del portfolio en Azure

Cada repositorio tiene su workflow .github/workflows/ci-cd.yml y sus scripts bajo .github/scripts/.
Las PR a main ejecutan las comprobaciones, sin credenciales SSH ni publicación de imágenes.
Un push o merge a main ejecuta pruebas, construye la imagen linux/amd64 con el Dockerfile del repositorio y la publica en GHCR.
El job de producción espera la aprobación configurada en GitHub y actualiza solo api (backend) o web (frontend).
También se puede ejecutar desde Actions > Run workflow en main.

## Credenciales: fuera de Git

Configurar primero, en AMBOS repositorios, Settings > Environments > production:

- Required reviewers: Diego (diegoesp10).
- Prevent self-review: desactivado si Diego inicia y aprueba sus propias publicaciones.
- Allow administrators to bypass protection rules: desactivado.
- Deployment branches: seleccionar únicamente la rama main.

El script check-production.sh detiene el job si no hay al menos un revisor obligatorio o si el commit aprobado ya no es el actual main.
Esta comprobación requiere actions: read, declarado en el workflow. Crear y proteger el entorno antes de subir las pipelines.
El entorno es configuración de GitHub; escribir environment: production en YAML no configura por sí solo sus revisores.

Añadir estos Environment secrets en production de cada repositorio:

| Nombre             | Valor                                                          |
| ------------------ | -------------------------------------------------------------- |
| SSH_HOST           | Host o IP de la VM. No incluir usuario ni puerto.              |
| SSH_USER           | Usuario Linux que puede ejecutar Docker en el stack existente. |
| SSH_PRIVATE_KEY    | Clave privada OpenSSH dedicada a estas publicaciones.          |
| SSH_KEY_PASSPHRASE | Opcional: contraseña de la clave si está cifrada.              |
| SSH_KNOWN_HOSTS    | Entrada known_hosts verificada para el host y puerto de la VM. |

Añadir estas Environment variables si cambian los valores habituales:

| Nombre      | Por defecto                                 |
| ----------- | ------------------------------------------- |
| SSH_PORT    | 22                                          |
| DEPLOY_PATH | portfolio, relativo al home del usuario SSH |

DEPLOY_PATH puede ser una ruta absoluta simple. Debe apuntar a la carpeta del Compose de producción existente, con compose.yaml y .env.
No subir una clave, contraseña, token, .env, dirección del servidor ni datos de clientes a los repositorios.
No enviar estos valores por el chat.

La clave pública correspondiente debe estar en ~/.ssh/authorized_keys del usuario de la VM.
Generar una clave específica permite revocarla sin cambiar la que utilizas desde tu ordenador.
La clave y su posible contraseña solo se cargan en archivos temporales privados y en ssh-agent del runner; al terminar se eliminan.

Para registrar la clave desde PowerShell sin imprimirla, una vez creado el entorno:

```powershell
$portfolioKeyPath = Join-Path $env:USERPROFILE '.ssh\portfolio-actions'
# Genera la pareja si no existe; la contraseña se introduce de forma interactiva.
ssh-keygen -t ed25519 -f $portfolioKeyPath -C 'portfolio-github-actions'

# Ejecuta estos comandos solo después de autorizar la clave pública en la VM.
Get-Content -LiteralPath $portfolioKeyPath -Raw |
  gh secret set SSH_PRIVATE_KEY --env production --repo diegoesp10/My-personal-Website-Back
Get-Content -LiteralPath $portfolioKeyPath -Raw |
  gh secret set SSH_PRIVATE_KEY --env production --repo diegoesp10/My-Personal-WebSite
```

Los demás secretos se introducen en la interfaz de GitHub o con gh secret set, sin escribir sus valores en comandos versionados.
Para SSH_KNOWN_HOSTS, obtener la clave pública del host y comprobar su huella por una conexión ya confiable o desde la consola de Azure.
Con puerto distinto de 22, la entrada debe usar [host]:puerto.
La pipeline no acepta automáticamente una clave de host obtenida mediante ssh-keyscan sin verificar.

## Token para GHCR

No crear un PAT ni guardar un token de GitHub permanente en la VM.
GitHub Actions genera GITHUB_TOKEN para cada job:

- publish tiene packages: write y publica la imagen de su repositorio.
- deploy tiene packages: read y usa su token temporal para descargar esa misma imagen.

La imagen se asocia a su repositorio mediante el workflow y la etiqueta org.opencontainers.image.source.
Si el paquete ya existía sin asociar, habilitar el acceso de Actions desde el repositorio correspondiente en la configuración del paquete.
Esto funciona con paquetes privados; no es necesario hacer públicas las imágenes.

El token de descarga se transmite por stdin de SSH, no como argumento ni dentro de los archivos del repositorio.
Docker recibe el token por --password-stdin. Su configuración temporal en la VM se elimina al finalizar.
El token de Cloudflare Tunnel permanece en la .env de la VM; estas pipelines no lo necesitan y no lo modifican.

## Qué cambia en el servidor

No se construye código en la VM ni se descarga una main mutable.
Se descarga la imagen exacta ghcr.io/diegoesp10/diego-portfolio-api@sha256:... o diego-portfolio-web@sha256:...
Luego se actualiza la etiqueta local que ya usa tu Compose: diego-portfolio-api:local o diego-portfolio-web:local.
Solo se recrea el servicio seleccionado con --no-deps y se espera a su healthcheck.
El contenedor del túnel y el otro servicio no se recrean.

Requisitos: stack ya aprovisionado, Docker Compose con --wait, Bash, curl, flock y timeout; Ubuntu y VM x86-64.
El usuario SSH debe tener acceso a Docker. Si SSH solo permite la IP de tu equipo, habilitar una ruta de acceso para el runner o elegir un runner con conectividad a la VM; no es necesario abrir HTTP/HTTPS.
La pipeline no cambia reglas de red de Azure ni crea una VM.

Ambos repositorios adquieren el mismo bloqueo portfolio/.deploy/deploy.lock.
La versión anterior se conserva con la etiqueta :previous.
Si falla el arranque o las comprobaciones /healthz y /api/health, se intenta restaurar la imagen anterior y el workflow termina con error.
Si también falla la restauración, el log lo indica expresamente para intervenir.
La recreación de un servicio puede producir una breve interrupción; esto no implementa un despliegue blue/green.

Los metadatos del despliegue quedan en .deploy/releases/api.json o web.json, sin credenciales.
No se reemplazan portfolio.json, uploads, estadísticas ni el volumen diego-portfolio-data.
Cambiar seed.json en Git no sustituye el contenido ya persistido; una actualización de contenido requiere su propio proceso.
No se ejecuta -ReplaceData, docker compose down -v ni una limpieza de imágenes en estas pipelines.

## Proteger main

En ambos repositorios, configurar una regla para main:

- Exigir PR para cualquier cambio y las comprobaciones Backend checks o Frontend checks, respectivamente.
- Bloquear force pushes y eliminación de main.
- Aplicar las restricciones también a administradores.
- Si trabajas solo, no exigir una revisión de PR de otra persona: no puedes aprobar tu propia PR.

Las dos decisiones son fusionar la PR y aprobar el job de producción. GitHub no muestra dos preguntas en cada git push.
Los revisores y las reglas de rama no se activan solo por añadir el YAML; se configuran en GitHub.

## Validación local y activación

```powershell
# En el backend:
dotnet build DiegoPortfolio.slnx
dotnet test DiegoPortfolio.slnx --no-build
node scripts/test-api.mjs
node .github/scripts/test-deployment.mjs

# En el frontend:
npm run build
node .github/scripts/test-deployment.mjs
```

Las pruebas de despliegue simulan comandos en carpetas temporales. No conectan con Docker, SSH ni Azure.
Los scripts deben conservar finales de línea LF.

Orden de activación:

1. Crear y proteger production en ambos repositorios.
2. Autorizar la clave pública en la VM y cargar los secretos y variables en GitHub.
3. Publicar los archivos de CI/CD en main mediante PR y configurar sus comprobaciones como obligatorias.
4. Aprobar el primer job de producción y comprobar la web.
5. Continuar con PR > pruebas > merge > imagen > aprobación > SSH.

No se ha realizado un despliegue real durante la generación de estas pipelines.

Fuentes:

- https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets
- https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments
- https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry
