# Qaventra deployment

Production runs on MonsterASP.NET (free plan) at https://qaventra.runasp.net, with a SQL Server database `db74823` in the EU datacenter. The API and the web app are one IIS site.

## Deploy

```
& G:\HRMS\deploy\deploy-prod.ps1
```

It runs the backend tests, builds the package, asks for `yes`, uploads over FTPS and checks the live site (web app 200, API 401 without login, database answers a fake login with 401). If the tests fail, nothing is uploaded.

### From a Mac (or Linux)

```
./deploy/deploy-prod.sh
```

Same steps and the same `secrets` files as the Windows script. Copy `deploy\secrets\monsterasp.txt` and `deploy\secrets\production.json` from the Windows machine first, so the JWT key and Owner login stay the same (a fresh `production.json` signs everyone out and creates a new Owner password). LocalDB is Windows-only, so on a Mac only the domain tests (tax, payroll, calendar, geo-fence) run before the upload; the API tests still run on Windows. `--build-only` builds `out/site` without uploading, `--skip-tests` and `--yes` work like on Windows. Needs the .NET 10 SDK, Node, curl, python3 and zip.

## Files

| File | What it does |
|---|---|
| `deploy-prod.ps1` | The whole deployment, start to finish |
| `deploy-prod.sh` | The same deployment from macOS or Linux, in one script |
| `build-package.ps1` | Builds the web app and API into `out\site` and `out\qaventra-site.zip`, writes `appsettings.Production.json` |
| `upload-ftps.ps1` | Uploads `out\site` to `/wwwroot` over FTPS with curl; shows an "updating" page during the upload; `web.config` goes last |
| `secrets\monsterasp.txt` | `ConnectionString=`, `SiteUser=`, `SitePassword=`, `SiteUrl=` (git-ignored) |
| `secrets\production.json` | JWT signing key and the Owner login, created on the first build and reused (git-ignored) |

## After changing a password in the MonsterASP panel

- Database password: update the `Password=` part of `ConnectionString` in `secrets\monsterasp.txt`, then deploy.
- Site/FTP password: update `SitePassword=` in `secrets\monsterasp.txt`.

## Notes

- Database tables are created or updated automatically when the site starts (`Database:MigrateOnStartup`).
- The database is reachable only from sites hosted at MonsterASP, not from this computer.
- Web Deploy (port 8172) refused the site login, so uploads use FTPS instead.
- Keep a copy of `secrets\production.json`. Losing the JWT key signs everyone out; losing it does not lose data.
