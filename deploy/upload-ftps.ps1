# Uploads deploy\out\site to the MonsterASP site over FTPS (explicit TLS) with curl.
# Credentials come from deploy\secrets\monsterasp.txt (SiteUser / SitePassword) and never appear on the command line.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$site = Join-Path $root 'deploy\out\site'
$s = @{}; Get-Content (Join-Path $root 'deploy\secrets\monsterasp.txt') | % { $k, $v = $_ -split '=', 2; $s[$k] = $v }
$base = "ftp://$($s.SiteUser).siteasp.net/wwwroot"
$cfg = Join-Path $env:TEMP "qv-curl-$PID.cfg"
Set-Content $cfg "user = `"$($s.SiteUser):$($s.SitePassword)`"" -Encoding ascii
try {
    # Take the running app offline while files are replaced (no-op on first deploy).
    $offline = Join-Path $env:TEMP 'app_offline.htm'
    Set-Content $offline '<h1>Qaventra is being updated. Please try again in a minute.</h1>'
    curl.exe -s -S --ssl-reqd -K $cfg -T $offline "$base/app_offline.htm" | Out-Null

    $files = Get-ChildItem $site -Recurse -File | Sort-Object { $_.Name -eq 'web.config' }
    $i = 0
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($site.Length + 1).Replace('\', '/')
        curl.exe -s -S --ssl-reqd --ftp-create-dirs --retry 3 -K $cfg -T $f.FullName "$base/$rel"
        if ($LASTEXITCODE) { throw "Upload failed for $rel (curl exit $LASTEXITCODE)" }
        $i++
        if ($i % 20 -eq 0) { Write-Host "uploaded $i / $($files.Count)" }
    }
    Write-Host "uploaded $i / $($files.Count)"

    # Remove the host's placeholder page and bring the app back online.
    curl.exe -s -S --ssl-reqd -K $cfg "$base/" -Q '-DELE iisstart.htm' 2>$null | Out-Null
    curl.exe -s -S --ssl-reqd -K $cfg "$base/" -Q '-DELE app_offline.htm' | Out-Null
    Write-Host 'Done.'
}
finally {
    Remove-Item $cfg -ErrorAction SilentlyContinue
}
