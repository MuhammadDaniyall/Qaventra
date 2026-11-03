# FlowHCM gap audit (2026-11-03)

Every row of FLOWHCM_FEATURES.md that was not marked done, checked against the code. Its status column is stale; this list is current.
About 36 rows are now fully built. What follows is what is still partial or missing.

## 0–2. Patterns, dashboard, organization
- Global filter bar: no country/province/city/area/vendor/designation/office filters.
- Excel upload only for employees and attendance.
- "View report" from request lists (pre-filtered report): missing.
- Arabic and an Urdu payslip format: missing (Urdu UI and RTL are done).
- Dashboard: no attendance/payroll/recruitment/helpdesk tabs, employee matrix, "available employees" widget, export to PNG/PDF/Excel.
- Constants: no vendor, location (country/province/city/area), banks, prefix, nationality, job field, field of study, document type, GL class, competency lists; designations have no code.
- Announcements and polls: audience by department only (no status/gender/designation, no exemptions); polls have no undo-vote, email on post, repost, results PDF.
- Geo/IP: no MAC/device id, two-factor flag, photo on check-in, area name.
- Close: no day-level close, no "process overtime for a date range".
- Custom fields only on employees (none per module), no "replicate to profile request".
- Document management (per-module attachment settings, storage used): missing.
- ID card: settings live in localStorage; no back side, drag-to-place fields, orientation, font size.

## 3. Employee
- Additional info request: missing.
- Field settings (self-edit / needs approval / mandatory per field): missing.
- Transfer: only department, designation, manager; no division/grade/cost-center/status changes, transition type, Excel.
- Date-wise department amendment for cost allocation: missing.

## 4. Attendance
- Roster: no copy shift/week/month, replicate, Excel; no roster templates or clone.
- Amend attendance: no raw punch log view, no repost of attendance/penalties.
- Shifts: no copy between employees, no overtime shift, no repost history.
- Attendance close: no preview summary, no approval.
- Penalty rules: no gender/status scope, no "from beginning".
- Exemption quota is one company-wide number, not per flag.
- Attendance/exemption reason types: missing.
- Temporary card: missing.
- Flags: no Missing, Quarter-day, Break error, Absent for short time, Present on holiday.

## 5. Leave
- Leave type: no club rules, status/grade scope, monthly limit, request after/before N days, holiday eligibility, per-type encashment, per-type approval chain.
- Station leave (with address): missing.
- Compensatory leave from overtime slabs: missing; comp-off is a manual claim, not linked to OT.
- Leave year start is calendar year only.

## 6. Payroll
- Salary setup: no hourly/daily pay, %-of-basic components, per-component flags, per-employee OT rates, bank/cash split.
- Salary change, hold, encashment and tax adjustment have no approval step.
- Loans: no types/criteria, markup, processing fee, PF loan, prepay/installment change.
- Salary changes touch basic only; no allowance changes, no Excel.
- No forex rates, CPR, withholding tax types.
- No "move to final settlement" from process payroll.

## 7. Reports
- 33 reports against FlowHCM's 136. Missing among the top ones: payslip report, leave ledger, loan balance/schedule, employee profile report; nothing for separation, recruitment, performance, helpdesk, overtime, salary history, increments.
- Payslips are not password protected.

## 8. Performance
- No 12-box, multiple-choice KPIs, grace marks, attachments on evaluations, final/modifier reviewer stages, clone setup, Excel import of KPI libraries, per-stage visibility, sub-KPI weightage.

## 9. Recruitment
- No bulk CV import with resume parsing, Excel candidate import, candidate sign-up with OTP, interview rescheduling, pipeline templates, skill sets, questionnaire categories, field templates, re-apply after N months, careers branding, whitelist, manager-shortlist / interview 1–5 / hiring-approval stages.

## 10. Letters
- No data tables in templates, custom variables, bulk issue, header editor.

## 11. Expense
- No approved amount per line, quotas with carry-forward / pro-rata / accrual, monthly rates, employee-wise entitlements, quota year start, scope by gender/status/designation/group, taxable flag, include-in-payslip, per-category approval chain.

## 12. Separation
- Final settlement is not paid through a payroll run (only a "paid via" note).

## 13–23. Other modules
- Help desk: settings per category only; ticket types have no lead rule, involve list, email switches, comment edit/delete.
- Assets: no asset categories, no Excel import.
- Travel: minimum notice is one company setting, not per approval level.
- Manpower budget: no positions (code, grade, cost center, reports-to, salary band), position transfer, job descriptions.
- Onboarding: no approval step, fixed task categories, no attachments per task, no change-password step.
- Training: external trainers are free text, programs have no attachments, no settings.
- Piece work: paid in normal payroll, no separate run, no attachments, no settings.
- Scheduled reports/alerts: only daily/weekly/monthly; no "every N", last day of month, data window, multi-level recipients; alerts only "days before".
- Email: one global SMTP, no per-company SMTP or test send; no header/footer; no per-form or per-stage templates.
- Integrations: JV is generic CSV (no Oracle/SAP format); GL mapping is 9 fixed lines; no field mapping, compliance APIs, Muqeem, Abhi.
- Desks: a booking covers one day, not a range.

## Done since the audit
- Reports: loan balances, salary revisions, separations, hiring pipeline, helpdesk tickets.
- Letters issued to many employees at once.
- Expense: approved amount per line.
- Interview rescheduling.
- Station leave with address and phone.
- Final settlement paid through the leaving month's payroll.
- AI: HR assistant with voice, AI summary on insights, AI CV check.

- Leave types: max days ahead, max days back, monthly limit.
- Assets: Excel import.
- Roster: copy a week forward.
- Payslip password: not needed (payslips are only shown after sign-in, never emailed as PDF).

