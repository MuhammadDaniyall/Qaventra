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

## AI assistant (optional)

The "Ask AI" assistant stays hidden until `deploy/secrets/production.json` has an `Ai` section with an Anthropic API key
(console.anthropic.com → API keys). `Model` and `MessagesPerMonth` (questions per company a month) are optional:

```json
"Ai": { "ApiKey": "<anthropic api key>", "Model": "claude-haiku-5-5", "MessagesPerMonth": 1000 }
```

Free option: Google Gemini. Create a key at aistudio.google.com (API keys; no card needed) and use it instead:

```json
"Ai": { "GeminiApiKey": "<gemini api key>", "MessagesPerMonth": 1000 }
```

Models are tried in order (default `gemma-4-31b-it,gemini-flash-lite-latest,gemini-flash-latest`); set `"GeminiModels"` to change them.

```json
"Ai": { "GeminiApiKey": "<gemini api key>", "GeminiModels": "gemma-4-31b-it,gemini-flash-lite-latest,gemini-flash-latest" }
```

Gemini is used only when there is no Anthropic `ApiKey`. On Gemini's free tier Google may use what is sent to improve its
products, and the free allowance is limited per minute and per day; a paid Gemini or Anthropic key avoids both.

Backup: an OpenRouter key (openrouter.ai) is used when Gemini is busy or used up for the day, or alone when there is no other key
(its free models allow about 50 requests a day): `"OpenRouterApiKey": "<openrouter key>"`, optionally `"OpenRouterModels"`.

Then deploy again. For local runs, put the same section in `backend/src/HRMS.Api/appsettings.Development.json` or set
`Ai__ApiKey` in the environment.

## Virtual clients

`seed-demo.mjs` creates a client company; copy the `demo-logins.txt` it writes to `secrets/logins-<name>.txt`.
`run-virtual-clients.sh` then runs `virtual-clients.mjs` for every `secrets/logins-*.txt`. The company's HR manager, line
manager, employees and a helpdesk agent use the product through the API during their working hours, as a real company
would:

- Each person gets a plan for the day from their shift on the roster (off days and holidays included): arrival around
  shift start (some late), lunch/prayer/tea breaks, departure around shift end (some overtime, a few forget to check out),
  a few absences and sick days, and daytime work spread over the hours (leave, expenses, helpdesk, letters, timesheets,
  training, suggestions, manager approvals, HR work such as joiners, attendance fixes, announcements, recruitment and
  payroll near the month end). On a day off one or two people may come in. The plan is seeded by company, person and
  date, so every run of the day agrees on it.
- Each run does what has fallen due and is not done yet, then waits for what falls due in the next 25 minutes, so
  punches land on their planned minute. Progress is kept per day in `secrets/virtual-clients-state.json`, so nothing is
  done twice; a run that finds another still going exits.
- Punches are stamped by the server's clock, so they are only made on time (up to 40 minutes late). If the Mac slept
  through a check-in or check-out, it is not made late: the person asks for a missed-punch fix on their next working
  day. Other overdue work is done when the Mac wakes.
- Every run appends a section to that day's report, `secrets/virtual-clients-report-<date>.md` (failures first; each is
  an app bug or a script mistake), and prints one line per company to the log.

The launchd job (`~/Library/LaunchAgents/com.qaventra.virtual-clients.plist`) starts it every 30 minutes Monday to
Saturday from 08:00 to 23:30 (a `StartCalendarInterval` entry per weekday for minutes 0 and 30 of hours 8–23), so the
14:00–23:00 evening shift checks out on time too; it logs to `secrets/virtual-clients-daily.log`, and a run missed
while the Mac slept runs on wake. Stop it with `launchctl bootout gui/$(id -u)/com.qaventra.virtual-clients`.

Useful by hand:

- `node deploy/virtual-clients.mjs --base http://localhost:5080 --logins a.txt --plan` prints today's plan for everyone.
- `--now 2026-11-03T09:30` pretends it is that time (Pakistan time unless a zone is given). The server stamps punches
  with its own clock, so with a `--now` that is not the real time punches are only logged ("dry"); everything else is
  sent. Use `--state-dir <dir>` to keep such tests out of `secrets/`.
- `--days 5` (or `--date 2026-10-30`) backfills whole past days the old way, through missed-punch and work-from-home
  requests; today, if in the range, is played as a normal run.

## Notes

- Database tables are created or updated automatically when the site starts (`Database:MigrateOnStartup`).
- The database is reachable only from sites hosted at MonsterASP, not from this computer.
- Web Deploy (port 8172) refused the site login, so uploads use FTPS instead.
- Keep a copy of `secrets\production.json`. Losing the JWT key signs everyone out; losing it does not lose data.

## Company mail servers

Company admins can set their own SMTP server under Settings → Email. The password is encrypted with AES-GCM
using `Secrets:Key` (32+ characters) when set, otherwise a key derived from `Jwt:Key`. Changing that key means
companies have to enter their mail password again.
