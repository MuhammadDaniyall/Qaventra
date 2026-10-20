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
    # IIS needs a moment to shut the app down; until then its DLLs are locked (curl exit 25).
    Start-Sleep -Seconds 20

    # The host is Windows IIS: Linux and macOS native runtimes are never loaded, and the 38 MB ones break the FTPS link.
    $files = Get-ChildItem $site -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\runtimes\\(linux|osx|unix)' } |
        Sort-Object { $_.Name -eq 'web.config' }
    function Send($f, [int]$tries) {
        $rel = $f.FullName.Substring($site.Length + 1).Replace('\', '/')
        for ($attempt = 1; $attempt -le $tries; $attempt++) {
            curl.exe -s -S --ssl-reqd --ftp-create-dirs --connect-timeout 30 --max-time 300 -K $cfg -T $f.FullName "$base/$rel" 2>$null
            if ($LASTEXITCODE -eq 0) { return $true }
            Write-Host "retry $attempt for $rel (curl exit $LASTEXITCODE)"
            Start-Sleep -Seconds (5 * $attempt)
        }
        return $false
    }

    # The link drops now and then (curl 56), and IIS can hold a DLL for minutes after going offline (curl 25).
    # Files that still fail are left for a second pass, which has always succeeded once the old app is gone.
    $failed = @()
    $i = 0
    foreach ($f in $files) {
        if (Send $f 3) { $i++ } else { $failed += $f }
        if (($i + $failed.Count) % 20 -eq 0) { Write-Host "uploaded $i / $($files.Count)" }
    }
    if ($failed.Count) {
        Write-Host "Second pass for $($failed.Count) locked file(s) in 60 seconds..."
        Start-Sleep -Seconds 60
        foreach ($f in $failed) {
            if (-not (Send $f 6)) { throw "Upload failed for $($f.Name). The site still shows the 'updating' page; run this script again." }
            $i++
        }
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
