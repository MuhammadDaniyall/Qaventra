# Qaventra

Multi-company HR software for Pakistani businesses: employees, attendance with geo-fencing, leave with approvals, payroll with income tax and EOBI, employee self-service, and a full audit trail.

| Part | Tech | Folder |
|---|---|---|
| API | ASP.NET Core (.NET 10), EF Core 10, JWT | `backend/src/HRMS.Api` |
| Business rules | Plain C# (tax, payroll, calendar, geo-fence) | `backend/src/HRMS.Domain` |
| Database | SQL Server (LocalDB in development) | `backend/src/HRMS.Infrastructure` |
| Web app | React + TypeScript + Tailwind | `frontend` |
| Tests | xUnit, real SQL Server for API tests | `backend/tests/HRMS.Tests` |

## Run locally

1. Copy `backend/src/HRMS.Api/appsettings.Development.example.json` to `appsettings.Development.json` and fill in `Jwt:Key` (32+ random characters) and `SuperAdmin:Password`.
2. Start the API (creates the `HRMS` database on LocalDB and the SuperAdmin on first run):
   ```
   cd backend/src/HRMS.Api
   dotnet run --launch-profile http
   ```
   API: http://localhost:5080 — Swagger UI: http://localhost:5080/swagger
3. Start the web app:
   ```
   cd frontend
   npm install
   npm run dev
   ```
   Open http://localhost:5173

Run the tests with `dotnet test backend/tests/HRMS.Tests` (needs LocalDB on Windows, or `HRMS_TEST_SQL` pointing at any SQL Server elsewhere, see `deploy/README.md`; each run creates and drops its own database).

## Roles

| Role | Can do |
|---|---|
| SuperAdmin | Platform owner. Sees every company, suspends/activates companies, reads every audit log. Belongs to no company. |
| CompanyAdmin | Everything in their company, including audit logs and creating other admins. |
| HrManager | Employees, salaries, attendance corrections, leave approvals, payroll, settings. |
| Manager | Their direct reports: team list, team attendance, leave approvals. |
| Employee | Self-service: check in/out, apply for leave, payslips, profile. |

## Multi-company isolation

Every company-owned table has a `TenantId`. `HrmsDbContext` adds a query filter on all of them using the signed-in user's company, and stamps `TenantId` on new rows, so one company can never read or write another's data. Only SuperAdmin endpoints bypass the filter, explicitly.

## Audit trail

Every event is stored in `AuditLogs` and can never be edited or deleted through the API:

- **Auth**: sign-in succeeded/failed/blocked, logout, password change, company registration.
- **Request**: every `/api/*` call with user, method, path, status code, duration, IP and browser.
- **Data**: every row created, updated or deleted, with old and new values. Password hashes are masked.

SuperAdmin sees all companies' logs; a CompanyAdmin sees only their own company's.

## Payroll rules (verify before going live)

- Salaried income tax slabs for **tax year 2025-26** are seeded, including the 9% surcharge above Rs 10 million. Add each new year's slabs after the federal budget (`TaxYears` / `TaxSlabs` tables). If a month has no matching tax year, payroll uses the latest one and shows a warning.
- EOBI defaults to Rs 400 employee / Rs 2,000 employer per month. Change them in Payroll settings to match the current EOBI notification.
- Earnings are prorated by calendar days; unpaid leave (and optionally absent days) reduce paid days. Fixed deductions such as loans are not prorated.
- Tax is withheld monthly as the annualised tax on the month's taxable earnings divided by 12. It does not yet adjust for tax already deducted earlier in the year.
