# Virtual clients on production from Windows: the same as run-virtual-clients.sh (the Mac's launchd job).
# Task Scheduler starts it every 30 minutes, Monday to Saturday, 08:00-23:30 (see deploy/README.md).
# Uses every deploy/secrets/logins-*.txt that seed-demo.mjs produced; does nothing until one exists.
# Run it on one computer only: progress is kept per computer, so two computers would do each day's work twice.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$secrets = Join-Path $root 'deploy\secrets'
$log = Join-Path $secrets 'virtual-clients-daily.log'
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$logins = @(Get-ChildItem -Path $secrets -Filter 'logins-*.txt' -ErrorAction SilentlyContinue | ForEach-Object { "deploy/secrets/$($_.Name)" })
if ($logins.Count -eq 0) {
    Add-Content -Path $log -Value "$stamp no deploy/secrets/logins-*.txt yet; copy them from the Mac or seed a company first."
    exit 0
}
Add-Content -Path $log -Value "$stamp starting with $($logins -join ', ')"
$node = 'C:\Program Files\nodejs\node.exe'
if (-not (Test-Path $node)) { $node = 'node' }
# cmd's redirection keeps the log plain UTF-8/ANSI text (PowerShell 5's >> would write UTF-16).
& cmd.exe /c "`"$node`" deploy/virtual-clients.mjs --logins $($logins -join ',') >> `"$log`" 2>&1"
exit $LASTEXITCODE
