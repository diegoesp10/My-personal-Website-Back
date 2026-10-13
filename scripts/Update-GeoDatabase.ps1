# Downloads DB-IP City Lite (free, CC BY 4.0, https://db-ip.com/db/lite.php) into App_Data/geo, so the visits map
# can place visitors without sending their address to any service. Optional: behind Cloudflare the location comes
# from its headers. The database is updated monthly; run this script again to refresh it.
# Then copy it to Docker with ..\Start-Docker.ps1 (the geo folder is imported into the data volume).
param([string]$Month = (Get-Date -Format 'yyyy-MM'))
$ErrorActionPreference = 'Stop'
$target = Join-Path (Split-Path $PSScriptRoot -Parent) 'App_Data/geo'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$compressed = Join-Path $target "dbip-city-lite-$Month.mmdb.gz"
$database = Join-Path $target 'dbip-city-lite.mmdb'

$url = "https://download.db-ip.com/free/dbip-city-lite-$Month.mmdb.gz"
Write-Host "Descargando $url"
Invoke-WebRequest -Uri $url -OutFile $compressed -UseBasicParsing

$source = [System.IO.File]::OpenRead($compressed)
try {
    $gzip = New-Object System.IO.Compression.GZipStream($source, [System.IO.Compression.CompressionMode]::Decompress)
    $output = [System.IO.File]::Create("$database.tmp")
    try { $gzip.CopyTo($output) } finally { $output.Dispose(); $gzip.Dispose() }
} finally { $source.Dispose() }
Move-Item -Force "$database.tmp" $database
Remove-Item $compressed
Write-Host ("Base de geolocalización lista: {0} ({1:N0} MB)" -f $database, ((Get-Item $database).Length / 1MB)) -ForegroundColor Green
