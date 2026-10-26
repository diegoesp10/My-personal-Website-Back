# Local API in Development on http://127.0.0.1:5080, with the editor enabled.
# The frontend (repository My-Personal-WebSite) runs with `npm run dev` and redirects /api to this port.
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Write-Host 'API: http://127.0.0.1:5080/api/health | Contenido: content/ (se publica al fusionar en main) | Datos locales: App_Data/' -ForegroundColor Green
dotnet run --project Portfolio.Api/Portfolio.Api.csproj --launch-profile Portfolio
if ($LASTEXITCODE -ne 0) { throw 'La API no pudo arrancar.' }
