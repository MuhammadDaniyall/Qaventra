# Qaventra production deployment: test -> build -> confirm -> upload (FTPS) -> health check.
#
#   & G:\HRMS\deploy\deploy-prod.ps1            # normal deploy
#   & G:\HRMS\deploy\deploy-prod.ps1 -SkipTests # only when you are sure (not recommended)
#
# Needs deploy\secrets\monsterasp.txt (ConnectionString, SiteUser, SitePassword, SiteUrl) and
# deploy\secrets\production.json (JWT key and owner login; created on the first build).
# Both are git-ignored. Passwords never appear on the command line or in the output.
param(
    [switch]$SkipTests,
    [switch]$Yes  # skip the confirmation question (for unattended runs)
)
$ErrorActionPreference = 'Stop'
$deploy = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $deploy
$env:Path = "C:\Program Files\nodejs;" + $env:Path

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

$secretsFile = Join-Path $deploy 'secrets\monsterasp.txt'
if (-not (Test-Path $secretsFile)) { throw "Missing $secretsFile" }
$s = @{}; Get-Content $secretsFile | % { $k, $v = $_ -split '=', 2; if ($k) { $s[$k.Trim()] = $v } }
foreach ($key in 'ConnectionString', 'SiteUser', 'SitePassword', 'SiteUrl') {
    if (-not $s[$key]) { throw "monsterasp.txt has no $key= line" }
}
$siteUrl = $s.SiteUrl.TrimEnd('/')

if (-not $SkipTests) {
    Step 'Running backend tests'
    dotnet test (Join-Path $root 'backend\tests\HRMS.Tests') --nologo -v q
    if ($LASTEXITCODE) { throw 'Tests failed. Nothing was deployed.' }
}

Step 'Building the package'
& (Join-Path $deploy 'build-package.ps1') -ConnectionString $s.ConnectionString

if (-not $Yes) {
    $answer = Read-Host "`nUpload to $siteUrl now? Users see an 'updating' page for about a minute. Type yes to continue"
    if ($answer -ne 'yes') { Write-Host 'Cancelled. Nothing was uploaded.'; return }
}

Step 'Uploading over FTPS'
& (Join-Path $deploy 'upload-ftps.ps1')

Step 'Health check'
$ok = $false
for ($i = 1; $i -le 12; $i++) {
    try {
        $web = Invoke-WebRequest "$siteUrl/" -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 60
        $me = Invoke-WebRequest "$siteUrl/api/auth/me" -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 60
        $login = Invoke-WebRequest "$siteUrl/api/auth/login" -Method Post -ContentType 'application/json' `
            -Body '{"email":"health-check@qaventra.invalid","password":"not-a-real-password"}' -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 60
        Write-Host "web app $($web.StatusCode) | API without login $($me.StatusCode) | database (fake login) $($login.StatusCode)"
        if ($web.StatusCode -eq 200 -and $me.StatusCode -eq 401 -and $login.StatusCode -eq 401) { $ok = $true; break }
    }
    catch { Write-Host "not reachable yet: $($_.Exception.Message)" }
    Start-Sleep -Seconds 10
}
if (-not $ok) { throw "Deployed, but the health check failed. Open $siteUrl and check the hosting panel logs." }
Write-Host "`nDeployed and healthy: $siteUrl" -ForegroundColor Green
