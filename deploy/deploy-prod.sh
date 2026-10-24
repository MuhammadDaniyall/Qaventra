#!/usr/bin/env bash
# Qaventra production deployment from macOS or Linux: test -> build -> confirm -> upload (FTPS) -> health check.
# Does the same as deploy-prod.ps1 and uses the same git-ignored secrets, so either machine can deploy.
#
#   ./deploy/deploy-prod.sh               # normal deploy
#   ./deploy/deploy-prod.sh --skip-tests  # only when you are sure (not recommended)
#   ./deploy/deploy-prod.sh --build-only  # build deploy/out/site and stop, nothing is uploaded
#   ./deploy/deploy-prod.sh --yes         # skip the confirmation question
#
# Needs deploy/secrets/monsterasp.txt (ConnectionString, SiteUser, SitePassword, SiteUrl) and
# deploy/secrets/production.json (JWT key and owner login; created on the first build). Copy both from
# the Windows machine so the JWT key and owner login stay the same. Passwords never reach the command line.
# Tools: dotnet 10 SDK, node/npm, curl, python3, zip (all present on a normal Mac with Homebrew).
set -euo pipefail

deploy="$(cd "$(dirname "$0")" && pwd)"
root="$(dirname "$deploy")"
out="$deploy/out"
site="$out/site"
secrets_dir="$deploy/secrets"
skip_tests=0 build_only=0 yes=0
for arg in "$@"; do
  case "$arg" in
    --skip-tests) skip_tests=1 ;;
    --build-only) build_only=1 ;;
    --yes) yes=1 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

step() { printf '\n\033[36m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31m%s\033[0m\n' "$1" >&2; exit 1; }

# key=value lines, split on the first "=" only (passwords and connection strings contain "=").
secret() { sed -n "s/^[[:space:]]*$1[[:space:]]*=//p" "$secrets_dir/monsterasp.txt" | head -1 | tr -d '\r'; }

[[ -f "$secrets_dir/monsterasp.txt" ]] || fail "Missing $secrets_dir/monsterasp.txt (copy it from the Windows machine)."
for key in ConnectionString SiteUser SitePassword SiteUrl; do
  [[ -n "$(secret "$key")" ]] || fail "monsterasp.txt has no $key= line"
done
site_url="$(secret SiteUrl)"; site_url="${site_url%/}"

if (( ! skip_tests )); then
  step 'Running backend tests'
  # The API tests need a SQL Server: LocalDB on Windows, or HRMS_TEST_SQL (see README, Docker) elsewhere.
  if [[ -n "${HRMS_TEST_SQL:-}" ]]; then
    dotnet test "$root/backend/tests/HRMS.Tests" --nologo -v q || fail 'Tests failed. Nothing was deployed.'
  else
    echo 'No HRMS_TEST_SQL: running only the domain tests (tax, payroll, calendar, geo-fence).'
    dotnet test "$root/backend/tests/HRMS.Tests" --nologo -v q --filter "FullyQualifiedName~HRMS.Tests.Domain" \
      || fail 'Tests failed. Nothing was deployed.'
  fi
fi

step 'Building the package'
mkdir -p "$secrets_dir"
python3 - "$secrets_dir/production.json" "$(secret ConnectionString)" <<'PY'
# Secrets are created once and reused, so redeploys keep the same keys and owner password.
import base64, json, os, secrets, string, sys
path, conn = sys.argv[1], sys.argv[2]
if os.path.exists(path):
    with open(path, encoding='utf-8-sig') as f:
        s = json.load(f)
else:
    alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    s = {'JwtKey': base64.b64encode(secrets.token_bytes(48)).decode(), 'OwnerEmail': 'owner@qaventra.com',
         'OwnerPassword': ''.join(secrets.choice(alphabet) for _ in range(16)) + '!9', 'ConnectionString': ''}
    print('Created deploy/secrets/production.json. Keep a copy: it holds the owner login and the JWT key.')
if conn:
    s['ConnectionString'] = conn
with open(path, 'w', encoding='utf-8') as f:
    json.dump(s, f, indent=2)
PY

echo 'Building web app...'
(cd "$root/frontend" && { [[ -d node_modules ]] || npm ci --silent; } && npm run build >/dev/null) || fail 'Frontend build failed'

echo 'Publishing API...'
rm -rf "$out"
dotnet publish "$root/backend/src/HRMS.Api/HRMS.Api.csproj" -c Release -o "$site" --nologo -v q >/dev/null || fail 'dotnet publish failed'
rm -f "$site"/appsettings.Development*.json
mkdir -p "$site/wwwroot"
cp -R "$root/frontend/dist/." "$site/wwwroot/"

python3 - "$secrets_dir/production.json" "$site/appsettings.Production.json" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as f:
    s = json.load(f)
cfg = {
    'ConnectionStrings': {'Hrms': s['ConnectionString']},
    'Jwt': {'Issuer': 'qaventra', 'Audience': 'qaventra', 'Key': s['JwtKey'], 'ExpiryMinutes': 480},
    'Database': {'MigrateOnStartup': True},
    'SuperAdmin': {'Email': s['OwnerEmail'], 'Password': s['OwnerPassword']},
    'Logging': {'LogLevel': {'Default': 'Warning', 'Microsoft.Hosting.Lifetime': 'Information'}},
}
with open(sys.argv[2], 'w', encoding='utf-8') as f:
    json.dump(cfg, f, indent=2)
PY

(cd "$site" && zip -qr "$out/qaventra-site.zip" .)
echo "Package ready: $out/qaventra-site.zip ($(du -h "$out/qaventra-site.zip" | cut -f1))"

if (( build_only )); then
  echo 'Build only: nothing was uploaded.'
  exit 0
fi

if (( ! yes )); then
  printf "\nUpload to %s now? Users see an 'updating' page for about a minute. Type yes to continue: " "$site_url"
  read -r answer
  [[ "$answer" == "yes" ]] || { echo 'Cancelled. Nothing was uploaded.'; exit 0; }
fi

step 'Uploading over FTPS'
site_user="$(secret SiteUser)"
base="ftp://$site_user.siteasp.net/wwwroot"
# The login goes to curl through a private config file, never on the command line.
cfg="$(mktemp)"; offline="$(mktemp)"
trap 'rm -f "$cfg" "$offline"' EXIT
chmod 600 "$cfg"
python3 - "$cfg" "$site_user" "$(secret SitePassword)" <<'PY'
import sys
path, user, pw = sys.argv[1:]
esc = lambda v: v.replace('\\', '\\\\').replace('"', '\\"')
with open(path, 'w') as f:
    f.write(f'user = "{esc(user)}:{esc(pw)}"\n')
PY
# Some home and office DNS servers don't know the *.siteasp.net FTP names. Fall back to public DNS and pin the
# address with --resolve, which keeps the real host name for TLS.
ftp_host="$site_user.siteasp.net"
resolve=()
if ! python3 -c 'import socket, sys; socket.gethostbyname(sys.argv[1])' "$ftp_host" 2>/dev/null; then
  ftp_ip="$(dig +short A "$ftp_host" @1.1.1.1 2>/dev/null | grep -E '^[0-9.]+$' | tail -1 || true)"
  [[ -n "$ftp_ip" ]] || fail "Can't find $ftp_host, even with public DNS. Check the internet connection and SiteUser."
  echo "This network's DNS doesn't know $ftp_host; using $ftp_ip from public DNS."
  resolve=(--resolve "$ftp_host:21:$ftp_ip")
fi
ftp() { curl -s -S --ssl-reqd -K "$cfg" ${resolve[@]+"${resolve[@]}"} "$@"; }

# Take the running app offline while files are replaced. If this silently fails the old app keeps every DLL
# locked, so retry and confirm the site really serves the offline page before replacing anything.
echo '<h1>Qaventra is being updated. Please try again in a minute.</h1>' > "$offline"
offline_ok=0
for attempt in 1 2 3 4 5; do
  if ! ftp -T "$offline" "$base/app_offline.htm" >/dev/null 2>&1; then
    echo "retry $attempt for app_offline.htm"; sleep 5; continue
  fi
  for check in 1 2 3 4 5 6; do
    sleep 5
    body="$(curl -s -m 30 -w '\n%{http_code}' "$site_url/api/auth/me" || true)"
    if [[ "${body##*$'\n'}" == "503" && "$body" == *"being updated"* ]]; then offline_ok=1; break; fi
  done
  (( offline_ok )) && break
done
(( offline_ok )) || fail 'Could not take the site offline (app_offline.htm). Nothing else was uploaded; run this script again.'
echo 'Site is showing the updating page.'
# IIS needs a moment to unload the app; until then its DLLs are locked (curl exit 25).
sleep 15

# The host is Windows IIS: Linux and macOS native runtimes are never loaded and the big ones break the FTPS link.
# web.config goes last so IIS restarts only once everything else is in place.
files=()
while IFS= read -r f; do files+=("$f"); done < <(
  cd "$site" && find . -type f ! -path './runtimes/linux*' ! -path './runtimes/osx*' ! -path './runtimes/unix*' ! -name web.config | sed 's|^\./||' | sort
  [[ -f "$site/web.config" ]] && echo web.config
)

send() { # send <relative path> <tries>
  local rel="$1" tries="$2" attempt
  for (( attempt = 1; attempt <= tries; attempt++ )); do
    if ftp --ftp-create-dirs --connect-timeout 30 --max-time 300 -T "$site/$rel" "$base/$rel" >/dev/null 2>&1; then return 0; fi
    echo "retry $attempt for $rel"
    sleep $(( 5 * attempt ))
  done
  return 1
}

# The link drops now and then, and IIS can hold a DLL for minutes after going offline.
# Files that still fail get a second pass once the old app is gone.
failed=() done_count=0
for rel in "${files[@]}"; do
  if send "$rel" 3; then done_count=$(( done_count + 1 )); else failed+=("$rel"); fi
  (( (done_count + ${#failed[@]}) % 20 == 0 )) && echo "uploaded $done_count / ${#files[@]}"
done
if (( ${#failed[@]} )); then
  echo "Second pass for ${#failed[@]} locked file(s) in 60 seconds..."
  sleep 60
  for rel in "${failed[@]}"; do
    send "$rel" 6 || fail "Upload failed for $rel. The site still shows the 'updating' page; run this script again."
    done_count=$(( done_count + 1 ))
  done
fi
echo "uploaded $done_count / ${#files[@]}"

# Remove the host's placeholder page and bring the app back online.
ftp "$base/" -Q '-DELE iisstart.htm' >/dev/null 2>&1 || true
ftp "$base/" -Q '-DELE app_offline.htm' >/dev/null

step 'Health check'
for i in $(seq 1 12); do
  web="$(curl -s -o /dev/null -m 60 -w '%{http_code}' "$site_url/" || true)"
  me="$(curl -s -o /dev/null -m 60 -w '%{http_code}' "$site_url/api/auth/me" || true)"
  login="$(curl -s -o /dev/null -m 60 -w '%{http_code}' -X POST -H 'Content-Type: application/json' \
    -d '{"email":"health-check@qaventra.invalid","password":"not-a-real-password"}' "$site_url/api/auth/login" || true)"
  echo "web app $web | API without login $me | database (fake login) $login"
  if [[ "$web" == 200 && "$me" == 401 && "$login" == 401 ]]; then
    printf '\n\033[32mDeployed and healthy: %s\033[0m\n' "$site_url"
    exit 0
  fi
  sleep 10
done
fail "Deployed, but the health check failed. Open $site_url and check the hosting panel logs."
