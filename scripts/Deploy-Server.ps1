# Publishes the site on the server: an Ubuntu VM with Docker, reachable over SSH with the alias "diego-portfolio"
# defined in ~/.ssh/config (the address is kept only there, never in the repositories).
# Builds the API image here and the web image in the sibling folder "Mi Web Front", uploads both with
# deploy/compose.yaml and deploy/install.sh to ~/portfolio and (re)starts the stack there.
#   -SkipBuild       upload the existing local images
#   -ReplaceData     replace the server content with App_Data/ (the previous one is kept in backups/ in the volume)
#   -SetTunnelToken  ask for the Cloudflare Tunnel token, store it on the server (~/portfolio/.env) and start the tunnel
#   -Backup          download a copy of the server data volume (content, images and visits) to App_Data/backups
# The first deployment imports App_Data when the server has no content; later ones keep the server content.
param([string]$Server = 'diego-portfolio', [switch]$SkipBuild, [switch]$ReplaceData, [switch]$SetTunnelToken, [switch]$Backup)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$frontRoot = Join-Path (Split-Path $root -Parent) 'Mi Web Front'
$work = Join-Path ([System.IO.Path]::GetTempPath()) 'diego-portfolio-deploy'

# Runs a native command and fails on its exit code. Its progress messages go to stderr, which Windows PowerShell 5.1
# turns into terminating errors under 'Stop' when the output is redirected.
function Invoke-Native([scriptblock]$Command, [string]$Message) {
    $ErrorActionPreference = 'Continue'
    & $Command
    if ($LASTEXITCODE -ne 0) { throw $Message }
}

if ($SetTunnelToken) {
    # Accepts the token alone or the whole command shown by Cloudflare (docker run ... --token eyJ...).
    $secure = Read-Host 'Pega el token del túnel (o el comando completo que muestra Cloudflare)' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $text = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    $match = [regex]::Match($text, 'eyJ[A-Za-z0-9_\-+/=.]+')
    if (-not $match.Success) { throw 'No parece un token de Cloudflare Tunnel (empieza por eyJ).' }
    Invoke-Native { $match.Value | ssh $Server 'bash portfolio/install.sh --set-token' } `
        'No se pudo guardar el token en el servidor. ¿Se ha desplegado antes la web?'
    Write-Host 'Túnel configurado.' -ForegroundColor Green
    return
}

if ($Backup) {
    $name = "servidor-$(Get-Date -Format 'yyyyMMdd-HHmmss').tgz"
    $target = Join-Path $root 'App_Data/backups'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Invoke-Native { ssh $Server "docker run --rm --user `$(id -u) --entrypoint tar -v diego-portfolio-data:/d:ro -v `$HOME:/out diego-portfolio-api:local -czf /out/$name -C /d ." } `
        'No se pudo crear la copia en el servidor.'
    Invoke-Native { scp -q "${Server}:$name" (Join-Path $target $name) } 'No se pudo descargar la copia.'
    Invoke-Native { ssh $Server "rm $name" } 'No se pudo borrar la copia temporal del servidor.'
    Write-Host "Copia guardada en $(Join-Path $target $name)" -ForegroundColor Green
    return
}

if (-not $SkipBuild) {
    if (-not (Test-Path -LiteralPath (Join-Path $frontRoot 'compose.yaml'))) { throw "No se encuentra el frontal en $frontRoot." }
    foreach ($folder in @($root, $frontRoot)) {
        Write-Host "== Construyendo $(Split-Path $folder -Leaf)" -ForegroundColor Cyan
        Push-Location -LiteralPath $folder
        try { Invoke-Native { docker compose build } "No se pudo construir la imagen de $folder." }
        finally { Pop-Location }
    }
}

New-Item -ItemType Directory -Force -Path $work | Out-Null
Get-ChildItem -LiteralPath $work | Remove-Item -Force

Write-Host '== Empaquetando imágenes' -ForegroundColor Cyan
$tar = Join-Path $work 'images.tar'
Invoke-Native { docker save -o $tar diego-portfolio-api:local diego-portfolio-web:local } `
    'No se pudieron exportar las imágenes. ¿Existen diego-portfolio-api:local y diego-portfolio-web:local?'
$source = [System.IO.File]::OpenRead($tar)
try {
    $output = [System.IO.File]::Create("$tar.gz")
    $gzip = New-Object System.IO.Compression.GZipStream($output, [System.IO.Compression.CompressionLevel]::Optimal)
    try { $source.CopyTo($gzip) } finally { $gzip.Dispose(); $output.Dispose() }
} finally { $source.Dispose() }
Remove-Item -LiteralPath $tar
$files = @("$tar.gz")

# Content: portfolio.json, portfolio.en.json and uploads. install.sh only uses it when the server has none or with
# -ReplaceData.
$data = Join-Path $root 'App_Data'
if (Test-Path -LiteralPath (Join-Path $data 'portfolio.json')) {
    $items = @('portfolio.json')
    if (Test-Path -LiteralPath (Join-Path $data 'portfolio.en.json')) { $items += 'portfolio.en.json' }
    if (Test-Path -LiteralPath (Join-Path $data 'uploads')) { $items += 'uploads' }
    Invoke-Native { tar -czf (Join-Path $work 'data.tgz') -C $data @items } 'No se pudo empaquetar App_Data.'
    $files += (Join-Path $work 'data.tgz')
}

# Line endings: bash and compose run on Linux.
foreach ($name in @('compose.yaml', 'install.sh')) {
    $text = [System.IO.File]::ReadAllText((Join-Path $root "deploy/$name")) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText((Join-Path $work $name), $text, (New-Object System.Text.UTF8Encoding($false)))
    $files += (Join-Path $work $name)
}

Write-Host '== Subiendo al servidor' -ForegroundColor Cyan
Invoke-Native { ssh $Server 'mkdir -p portfolio' } "No hay conexión SSH con $Server. Revisa ~/.ssh/config."
Invoke-Native { scp -q @files "${Server}:portfolio/" } 'No se pudieron subir los archivos.'

$flag = if ($ReplaceData) { '--replace-data' } else { '' }
Invoke-Native { ssh $Server "bash portfolio/install.sh $flag" } `
    'El despliegue falló en el servidor. Revisa los registros con: ssh diego-portfolio "cd portfolio; docker compose logs --tail 100"'
Remove-Item -LiteralPath $work -Recurse -Force
Write-Host 'Despliegue completado.' -ForegroundColor Green
