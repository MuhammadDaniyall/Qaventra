# Builds the Qaventra deploy package for a single IIS site (MonsterASP.NET or any Windows host):
#   G:\HRMS\deploy\out\site      the files to upload to the site root (API + web app in wwwroot)
#   G:\HRMS\deploy\out\qaventra-site.zip   the same, zipped
# Production secrets live in deploy\secrets\production.json (git-ignored) and are written into
# site\appsettings.Production.json. Run with -ConnectionString the first time.
param(
    [string]$ConnectionString
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$out = Join-Path $root 'deploy\out'
$site = Join-Path $out 'site'
$secretsFile = Join-Path $root 'deploy\secrets\production.json'
$env:Path = "C:\Program Files\nodejs;" + $env:Path

function NewSecret([int]$bytes) { $b = [byte[]]::new($bytes); [Security.Cryptography.RandomNumberGenerator]::Fill($b); [Convert]::ToBase64String($b) }
function NewPassword { $c = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'; (-join (1..16 | % { $c[[Security.Cryptography.RandomNumberGenerator]::GetInt32($c.Length)] })) + '!9' }

# Secrets are created once and reused, so redeploys keep the same keys and owner password.
if (Test-Path $secretsFile) {
    $secrets = Get-Content $secretsFile | ConvertFrom-Json
} else {
    New-Item -ItemType Directory -Force (Split-Path $secretsFile) | Out-Null
    $secrets = [pscustomobject]@{ JwtKey = NewSecret 48; OwnerEmail = 'owner@qaventra.com'; OwnerPassword = NewPassword; ConnectionString = '' }
}
if ($ConnectionString) { $secrets.ConnectionString = $ConnectionString }
$secrets | ConvertTo-Json | Set-Content $secretsFile -Encoding utf8
if (-not $secrets.ConnectionString) { throw 'Pass -ConnectionString with the SQL Server connection string from the hosting panel (only needed once).' }

Write-Host 'Building web app...'
Push-Location (Join-Path $root 'frontend'); npm run build | Out-Null; if ($LASTEXITCODE) { throw 'Frontend build failed' }; Pop-Location

Write-Host 'Publishing API...'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root 'backend\src\HRMS.Api\HRMS.Api.csproj') -c Release -o $site --nologo -v q | Out-Null
if ($LASTEXITCODE) { throw 'dotnet publish failed' }
Remove-Item (Join-Path $site 'appsettings.Development*.json') -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'frontend\dist\*') (New-Item -ItemType Directory -Force (Join-Path $site 'wwwroot')) -Recurse

[ordered]@{
    ConnectionStrings = @{ Hrms = $secrets.ConnectionString }
    Jwt = @{ Issuer = 'qaventra'; Audience = 'qaventra'; Key = $secrets.JwtKey; ExpiryMinutes = 480 }
    Database = @{ MigrateOnStartup = $true }
    SuperAdmin = @{ Email = $secrets.OwnerEmail; Password = $secrets.OwnerPassword }
    Logging = @{ LogLevel = @{ Default = 'Warning'; 'Microsoft.Hosting.Lifetime' = 'Information' } }
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $site 'appsettings.Production.json') -Encoding utf8

Compress-Archive -Path (Join-Path $site '*') -DestinationPath (Join-Path $out 'qaventra-site.zip')
$size = [math]::Round((Get-Item (Join-Path $out 'qaventra-site.zip')).Length / 1MB, 1)
Write-Host "Package ready: $out\qaventra-site.zip ($size MB)"
