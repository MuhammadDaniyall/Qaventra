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

Same steps and the same `secrets` files as the Windows script. Copy `deploy\secrets\monsterasp.txt` and `deploy\secrets\production.json` from the Windows machine first, so the JWT key and Owner login stay the same (a fresh `production.json` signs everyone out and creates a new Owner password). LocalDB is Windows-only, so on a Mac the script runs only the domain tests (tax, payroll, calendar, geo-fence) unless `HRMS_TEST_SQL` points at a SQL Server. To run every test on a Mac, start SQL Server in Docker once and export the connection string (no database name; each test class makes and drops its own):

```
docker run -d --platform linux/amd64 --name qaventra-test-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<a strong password>' -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
export HRMS_TEST_SQL="Server=localhost,14333;User Id=sa;Password=<that password>;TrustServerCertificate=True"
```

`--build-only` builds `out/site` without uploading, `--skip-tests` and `--yes` work like on Windows. Needs the .NET 10 SDK, Node, curl, python3 and zip.

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

## Email and helpdesk replies (optional)

Email stays off until `deploy/secrets/production.json` has an `Smtp` section. Add `HelpdeskInbox` too and people can
answer ticket emails by replying: Qaventra reads the inbox every two minutes, finds the ticket by its code in the
subject (HD-0042) and adds the reply as the sender. Only someone who may reply on that ticket gets in; other mail is
marked read and skipped.

A free Gmail account works for both. Turn on 2-step verification, create an app password
(Google Account → Security → App passwords) and add, with your own address and app password:

```json
"Smtp": { "Host": "smtp.gmail.com", "Port": 587, "User": "you@gmail.com", "Password": "<app password>", "From": "you@gmail.com", "FromName": "Qaventra" },
"HelpdeskInbox": { "Host": "imap.gmail.com", "Port": 993, "User": "you@gmail.com", "Password": "<app password>" }
```

Then deploy again. Use a mailbox only for Qaventra: every unread email in it is read.

## Notes

- Database tables are created or updated automatically when the site starts (`Database:MigrateOnStartup`).
- The database is reachable only from sites hosted at MonsterASP, not from this computer.
- Web Deploy (port 8172) refused the site login, so uploads use FTPS instead.
- Keep a copy of `secrets\production.json`. Losing the JWT key signs everyone out; losing it does not lose data.
