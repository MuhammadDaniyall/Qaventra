# Qaventra deployment

Production runs on MonsterASP.NET (free plan) at https://qaventra.runasp.net, with a SQL Server database `db74823` in the EU datacenter. The API and the web app are one IIS site.

## Deploy

```
& G:\HRMS\deploy\deploy-prod.ps1
```

It runs the backend tests, builds the package, asks for `yes`, uploads over FTPS and checks the live site (web app 200, API 401 without login, database answers a fake login with 401). If the tests fail, nothing is uploaded.

## Files

| File | What it does |
|---|---|
| `deploy-prod.ps1` | The whole deployment, start to finish |
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
