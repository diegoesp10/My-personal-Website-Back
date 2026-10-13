# Builds and starts the API container (diego-portfolio-api) in Production.
#   -SkipBuild    reuse the existing image
#   -ReplaceData  replace the content of the Docker volume with App_Data/ (the previous one is kept as a backup)
param([switch]$SkipBuild, [switch]$ReplaceData)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$dockerOs = docker info --format '{{.OSType}}'
if ($LASTEXITCODE -ne 0 -or $dockerOs -ne 'linux') { throw 'Docker Desktop debe estar iniciado con contenedores Linux.' }

# Shared resources: the network joins API and web; the volume keeps content and images.
$network = docker network ls --filter 'name=^diego-portfolio-net$' --format '{{.Name}}'
if (-not $network) {
    docker network create diego-portfolio-net | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la red diego-portfolio-net.' }
}
docker volume create diego-portfolio-data | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el volumen de datos.' }

if (-not $SkipBuild) {
    docker compose build
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo construir la imagen de la API.' }
}

# Imports App_Data into the volume when it is empty (or when -ReplaceData is used). The source is mounted
# read-only and never copied into the image.
$localData = Join-Path $PSScriptRoot 'App_Data'
if (Test-Path -LiteralPath (Join-Path $localData 'portfolio.json')) {
    $replace = if ($ReplaceData) { '1' } else { '0' }
    # No double quotes inside: Windows PowerShell 5.1 does not escape them for native commands.
    # One content file per language (portfolio.json, portfolio.en.json); a language missing in App_Data is left as is
    # and the API creates it from its seed.
    $script = 'set -e; imported=0; for f in portfolio.json portfolio.en.json; do ' +
        'if [ -e /app/data/$f ] && [ -e /import/$f ] && [ $REPLACE = 1 ]; then mkdir -p /app/data/backups; ' +
        'cp /app/data/$f /app/data/backups/${f%.json}-$(date +%Y%m%d-%H%M%S).json; rm /app/data/$f; fi; ' +
        'if [ ! -e /app/data/$f ] && [ -e /import/$f ]; then cp /import/$f /app/data/$f; imported=1; fi; done; ' +
        'if [ $imported = 1 ]; then if [ -d /import/uploads ]; then mkdir -p /app/data/uploads; ' +
        'cp -a /import/uploads/. /app/data/uploads/; fi; echo Contenido local importado al volumen.; fi; ' +
        'if [ -d /import/geo ]; then mkdir -p /app/data/geo; cp -a /import/geo/. /app/data/geo/; fi; ' +
        'chown -R app:app /app/data'
    docker run --rm --user 0 --entrypoint sh -e "REPLACE=$replace" `
        --mount 'type=volume,source=diego-portfolio-data,target=/app/data' `
        --mount "type=bind,source=$localData,target=/import,readonly" `
        diego-portfolio-api:local -c $script
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron importar los datos locales.' }
}

docker compose up -d --wait --wait-timeout 120
if ($LASTEXITCODE -ne 0) { throw 'La API no alcanzó un estado saludable. Consulta docker compose logs.' }
$apiPort = if ($env:PORTFOLIO_API_PORT) { $env:PORTFOLIO_API_PORT } else { '8081' }
Write-Host "API Docker lista: http://127.0.0.1:$apiPort/api/health" -ForegroundColor Green
docker compose ps
