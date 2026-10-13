# Loads the secrets of the "production" environment that the CI/CD workflows of both repositories use to deploy to the
# server over SSH (docs/CI-CD.md). Everything is read from this computer and sent to GitHub with gh: nothing is printed
# or written to the repositories.
#   -Server   SSH alias of ~/.ssh/config: host and user come from it, the pinned host key from ~/.ssh/known_hosts
#   -KeyPath  private key dedicated to GitHub Actions; its public half must be in authorized_keys on the server
# Run it again to rotate the key or after changing the server address.
param(
    [string]$Server = 'diego-portfolio',
    [string]$KeyPath = (Join-Path $HOME '.ssh/portfolio-actions'),
    [string[]]$Repositories = @('diegoesp10/My-personal-Website-Back', 'diegoesp10/My-Personal-WebSite')
)
$ErrorActionPreference = 'Stop'

# Runs a native command and fails on its exit code. Its messages go to stderr, which Windows PowerShell 5.1 turns into
# terminating errors under 'Stop' when the output is redirected.
function Invoke-Native([scriptblock]$Command, [string]$Message) {
    $ErrorActionPreference = 'Continue'
    & $Command
    if ($LASTEXITCODE -ne 0) { throw $Message }
}

if (-not (Test-Path -LiteralPath $KeyPath)) { throw "No existe la clave privada $KeyPath." }
$settings = @{}
foreach ($line in (ssh -G $Server)) {
    $name, $value = $line -split ' ', 2
    if (-not $settings.ContainsKey($name)) { $settings[$name] = $value }
}
$hostName = $settings['hostname']
$user = $settings['user']
$port = $settings['port']
if (-not $hostName -or $hostName -eq $Server) { throw "El alias $Server no está en ~/.ssh/config." }
$lookup = if ($port -eq '22') { $hostName } else { "[$hostName]:$port" }
$knownHosts = @(ssh-keygen -F $lookup | Where-Object { $_ -and -not $_.StartsWith('#') })
if ($knownHosts.Count -eq 0) { throw "La clave del servidor no está en ~/.ssh/known_hosts: conéctate antes con ssh $Server." }

foreach ($repository in $Repositories) {
    Write-Host "== $repository" -ForegroundColor Cyan
    Invoke-Native { gh secret set SSH_HOST --env production --repo $repository --body $hostName } 'No se pudo guardar SSH_HOST.'
    Invoke-Native { gh secret set SSH_USER --env production --repo $repository --body $user } 'No se pudo guardar SSH_USER.'
    Invoke-Native { ($knownHosts -join "`n") | gh secret set SSH_KNOWN_HOSTS --env production --repo $repository } `
        'No se pudo guardar SSH_KNOWN_HOSTS.'
    Invoke-Native { Get-Content -LiteralPath $KeyPath -Raw | gh secret set SSH_PRIVATE_KEY --env production --repo $repository } `
        'No se pudo guardar SSH_PRIVATE_KEY.'
}
Write-Host 'Secretos de despliegue configurados en los dos repositorios.' -ForegroundColor Green
