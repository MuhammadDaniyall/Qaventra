# FlowHCM 5.10: full feature inventory and gap list

Source: live walkthrough of the FlowHCM QA tenant on 2026-10-21. 247 screens were read from the app's own view
templates (every form field, tab, grid column, button and dropdown, including hidden modals), plus the 136-report
catalog. This complements [ROADMAP.md](ROADMAP.md), which came from the user manuals.

Legend for "Us": ✅ we have it · 🟡 partly · ⬜ missing

---

## 0. Patterns FlowHCM repeats on every screen

Build these once in our shared engine; every module then gets them for free.

| Pattern | What FlowHCM does | Us |
|---|---|---|
| Request + approval | Every request screen has "All requests / My requests", Requested By / Requested For (apply on behalf of someone), comments, cancel, delete, an "Approval routing" timeline popup | ✅ engine, 🟡 "on behalf of" per module |
| Approval chain config | Per request type: 1–5 levels (separation clearance up to 10), each level = line manager / manager's manager / HR / **specific employee** / other; "Notify on each level" via Email / Notification / Both; "Pending request count" | ✅ |
| Forcefully approve | HR can force-approve over the chain; status "Forcefully Approved" | ⬜ |
| My approval / Other approval | Approvers see their own queue and, if HR, everyone else's pending items | 🟡 |
| Delegation | **Delegation Request**: employee delegates approvals for a module to another employee for a period | ⬜ |
| Global filter bar | Country, Province, City, Area, Vendor, Station, Department, Sub-department, Employee Group, Employee, Division, Grade, Cost Center, Employee Status, Designation, include inactive | 🟡 (we filter by department/office) |
| Excel upload | Almost every list has "Upload Excel" with a template and a row-error popup ("You cannot proceed because the following errors exist") | ✅ employees/attendance, ⬜ most others |
| View report | Every request list has "View Report" (opens the matching report pre-filtered) | ⬜ |
| Change history | Settings screens keep "History" (change type, modified by, date, from → to) | ✅ audit log, ⬜ per-screen history popup |
| Report signatures | Attendance/leave/payroll/piece-work reports print 1–5 signature blocks (title + designation) | ⬜ |
| Translations / RTL | Whole UI is translatable; Arabic/Urdu right-to-left layout; Urdu payslip format | ⬜ |
| Quick access + global search | Search box over all ~260 forms; user pins favourite forms | ⬜ |

---

## 1. Dashboard

- Tabs: **ESS dashboard, Attendance dashboard, Payroll dashboard, Recruitment dashboard, Employee matrix, Help desk, Organogram, Trend analysis**.
- ESS widgets: my info card, birthdays/work anniversaries (today and this month, send wishes), attendance flag summary chart
  (Present, Late, Early, Half day, Absent, Short day, Absent for short time, Leave, Missing, H/D leave, Present on
  holiday, Gazetted holiday, Off) for current or previous month and "worked hours vs worked between shift", check-in
  button, missing/absent entries with a one-click "request" link, leave balance, my requests vs my approvals counters
  (clearance, attendance, leave, payroll, payroll & FnF), weekly scheduled vs worked hours, attendance summary, latest
  payslip, my team / my managers, news, HR policies, polls (open/closed, vote, undo vote), helpdesk tickets (mine / to resolve).
- Admin widgets: in/out activity, available employees, overall missing and absent entries, pending requests per module.
- Dashboard export to PNG/PDF/Excel; month/week/day views.

Us: 🟡 dashboard with charts. Missing: tabbed dashboards, organogram, employee matrix, trend analysis, polls/news
widgets, "request from missing entry" shortcut, birthday wishes.

## 2. Organization

| Screen | Key details | Us |
|---|---|---|
| Department | Name, code, head, title; sub-departments (with head and code) are a separate constant; Excel upload | ✅ (no sub-dept, no code) |
| Constants (one screen, ~50 lookup lists) | Entity, WPS company, license branch, employee group, expense unit, designation (+code), visa company/designation, employee status (permanent/contractual), allowance titles, deduction titles, station (+code, time zone), employee bank, job title (+expected hiring days, JD), job field, field of study, asset type, sub-department, province, country, vendor (+VAT, service charge), area, city, employee position, document type, resign reason, marital status, exit type, prefix, employer bank (+branch, account no/title), division (+head), grade, floor, room, university, gender, minimum wage, per-piece category, bank branch, client/customer, region, cost center, position title, GL class, qualification type, health insurance, nationality, competency + competency category, transition type, transition nature | 🟡 (departments, designations, offices) |
| Announcements | Title, type, display start/end, audience by employee status/gender/designation, exempted employees, attachment as file or URL, details | ⬜ |
| Polls | Title, start/end, audience filters, send email, question with 2–n options, results with PDF, repost | ⬜ |
| Holidays & gazetted holidays | Holiday type (gazetted vs special), religion-specific, station-specific, exempted employees, send email, Excel upload | 🟡 (plain holidays) |
| Geo-location / IP restriction | Per employee: allowed IP, MAC, mobile device id, lat/long + area name + radius in metres, two-factor flag, capture photo on sign-in; "apply same location" to many; Excel | 🟡 (office geofence only) |
| Projects & tasks | Project: customer, manager, coordinator, mode (open/close), budget, dates, worksite, stations, employees with hourly rate; tasks with team lead and include/exclude members | ⬜ |
| Month close / Day close | Lock a payroll period or day; "process overtime" for a date range; roll back | ⬜ |
| Custom fields | Per module and category (employee, expense category/type, leave type…); field types with values; replicate to profile request | ⬜ |
| Document management | Per module: attachments allowed, storage used per year | ⬜ |
| ID card template | Front/back image designer, drag fields, colour/font size, portrait/landscape, apply to all | ⬜ |
| Biometric devices | Token settings; device list with IP, port, type, station, password, last sync | ⬜ |
| Company settings | Name, logo, URL, account manager, employee limit, expiry, enabled modules | ✅ (platform side) |
| Email domain setting | Allowed email domains | ⬜ |
| System logs | Data logs (IP, MAC, module, method, screen resolution), user last login, exception logs | 🟡 (audit log) |

## 3. Employee

**Employee profile** (tabs: General, Additional, Company, Documents, Assets, Salary, Dependants, Custom fields):
- General: prefix, code, punch code, first/last/father name, mobile, email, reports-to, allow attendance from mobile /
  from web, photo, roles template, username, scheduled-report and alert opt-ins.
- Additional: marital status, gender, DOB, place of birth, nationality, family code, religion; bank (account, title,
  bank, branch code); address/state/zip; emergency contact; **government IDs**: CNIC + issue/expiry, EOBI no + entry
  date, social security no, passport + dates, visa no/company/designation + dates, Emirates ID + dates, NTN; **WPS**
  (company, permit company, license branches); **health insurance** (title, policy no, dates); **academics** (qualification
  type, institute, field of study, grade/CGPA, dates, majors, transcript); **work history** (organization, designation,
  job field, dates, salary, reason for leaving).
- Company: division, grade, status, region, cost center, GL class, position type (vacant/replacement), employee position,
  joining, confirmation, contract start/end, expected confirmation days, resign date; key roles, job description, competencies.
- Dependants: name, relation, CNIC, DOB, marital status, nominee.
- Assets tab, documents tab with type/expiry.
- Salary tab (see Payroll → Salary setup).
- List actions: export, Excel upload (employees, assets, dependants), send credentials, re-hire, inactivate with exit
  type/reason/leaving date, blacklist/whitelist, change password, password-setup link, add to schedule.

| Screen | Key details | Us |
|---|---|---|
| Profile change request | Employee edits any profile section; HR approves | ✅ |
| Employee info request | Side-by-side old vs new data for approval | 🟡 |
| Additional info request | Ask employees to complete specific fields | ⬜ |
| Field settings | Per field: enabled for self-edit, needs approval, mandatory; with history | ⬜ |
| Employee transfer / transition | Transfer date; change of division, grade, cost center, position, designation, status, manager, payroll setup; transition type and nature; Excel | ✅ job history, ⬜ transition types / Excel |
| Amend employee department | Date-wise department change (for cost allocation) | ⬜ |
| Resignation request | Reason, resignation date, last preferred working date | ✅ |
| Role management | Per-employee screen permissions, **role templates**, copy roles from one user to another, Excel | 🟡 (fixed roles) |
| Suggestions | Title, category, anonymous option; category routes to approvers and notifies by email | ⬜ |
| Employee approvals | One inbox for profile, transfer, info, resignation, suggestion, additional info | ✅ (shared inbox) |

## 4. Attendance

| Screen | Key details | Us |
|---|---|---|
| Shift plan | Code, name, start/end, min/max start, starts previous day / ends next day, break window, sign-in/out time limits, role-based shifts; **flag rules** per shift (late/early/half day/short day by minutes, next-day flags, hours) | 🟡 (basic shifts) |
| Schedule | Monthly schedules mapping shifts to employees, copy shift/week/month, replicate to other months, scheduled vs unscheduled employees, Excel | 🟡 (roster in progress) |
| Roster template, mapping, clone | Weekly rotation templates; map to employees from an effective date (with approval); clone month to month | 🟡 |
| Amend attendance | HR edits day-by-day in/out, sees breaks, remote work and raw logs, reposts attendance/penalties, Excel upload | 🟡 |
| Amend employee shift / overtime shift / overtime | Date-wise shift override, copy shifts between employees, repost history | ⬜ |
| Attendance request | Missed punch / correction (in & out date/time, type, reason), irregular attendance, mark from dashboard, Excel | ✅ |
| Exemption request | Exempt a flag (late, early…) on a date; reason types with "min working hours" validation | ⬜ |
| Remote work request | Date with in/out time | ✅ (work from home) |
| Shift request / shift swap / shift transfer | Current vs desired shift, multi-employee; swap with another employee | 🟡 (in progress) |
| Off-day request | Change weekly off (alternate/recursive) from an effective date | ⬜ |
| Worksheet (timesheet) | Daily lines: project, start/end, total/travel/worked hours, completion hours | ⬜ |
| Attendance close | Monthly freeze per payslip period with preview (present, off, absent, invalid, holidays, sandwich, penalty, deduction, OT) and approval | ⬜ |
| Penalty rules | Flag + count (e.g. 3 lates) → deduct leave or pay; once or every occurrence; exempted count; gender/status scope; from beginning | ⬜ |
| Flag exemption quota | Number of exemption requests allowed per flag | ⬜ |
| Reason types | Attendance reasons (attachment allowed/mandatory), exemption reasons | ⬜ |
| Approvals | One screen, filter by type: attendance, exemption, worksheet, remote work, shift, close, transfer, swap, roster, off-day | ✅ inbox |
| Temporary card | Temporary punch cards and assignment | ⬜ |
| Attendance flags | Present, Late, Early, Short day, Absent for short time, Half day, Quarter-day leave, Missing, Break error, Present on holiday, Gazetted/Special holiday, Off | 🟡 |

## 5. Leave

| Screen | Key details | Us |
|---|---|---|
| Leave type | Paid, quota-based, pro-rata, monthly or yearly entitlement, accrual (after N days, deduct taken/absent from accrual), carry forward (max), encashment (method: annual gross/365, /260, basic/365, fixed days…, max days), gender/status/grade scope, holiday & gazetted-holiday eligibility, **sandwich and club rules** (incl. half day, include gazetted), min/max consecutive days, notice before leave, request after/before N days, allowed before confirmation, monthly limit, attachment required after N days, durations (full/half/both), own approval chain, history | 🟡 |
| Leave request | Duration, days, from/to, pay through (salary/other), reason type, **station leave** with address, attachment, balance grid (entitled, taken, pending, sandwich pending, carried over, compensatory, balance), Excel, on behalf | ✅ (no station leave, no sandwich) |
| Leave reason types | Per leave type, max days per reason | ⬜ |
| Leave quota | Company-wise or employee-wise quota creation for the leave year, current vs previous quota, add/subtract operations, Excel | 🟡 (HR adjustments) |
| Opening leave balance | Effective-date opening balances | ⬜ |
| Compensatory leave config | From working-day / holiday / gazetted / special OT: hours → leave count slabs, rounding, min minutes, include pre-shift OT, limits | 🟡 (comp-off) |
| Compensation leave request | Multiple employees, compensate holiday or OT, lock after OT processed | 🟡 |
| Leave penalty rule | Deduct leave for flags | ⬜ |
| Settings | Leave year start (Jan 1 or specific date), auto carry forward, allow station leave, report signatures | 🟡 |

## 6. Payroll

**Payroll setup** (multiple setups, copy from another; criteria by designation, division, grade, status, gender):
- Pay schedule: monthly/weekly/daily, month start/end day, first-half end date, days to allow delete / un-disburse.
- Per-day salary and pro-rata methods; payroll and remit currency.
- Payslip options: format, show leave balance, PF/VPS contribution, loan-type wise, allowance/OT calculation, profit
  fund, fiscal-year tax, group-wise names, footer, up to 3 signatures.
- Tax rules: slabs (from, to, tax %, exempt amount, additional fixed tax).
- Advance salary policy: only for present days, % cap, requests per fiscal year, after N days from payroll start or N
  months from joining, restatement period.
- Salary change policy: apply from change date, allow back-dated arrears.
- Overtime: per working day / holiday / gazetted / special; from scheduled out time, rounding, minimum minutes, include
  pre-shift OT, taxable, limits by day/week/month, minute slabs → amounts.
- Auto deductions: attendance-flag rules (monthly/daily late minutes, early minutes, count-wise), absence (from
  beginning, exempted days), missing attendance, sandwich/club days, off-day limits, short time, pre-joining and
  post-leaving days (incl. holidays).
- Final settlement options.
- VPS (voluntary pension) policy and PF policy: contribution start, employee/employer share, arrears, tax treatment,
  withdrawal limits, employer share excluded on certain exit types, PF loan (method, limit, Musharaka/Mudaraba, tenure, rate).
- Social security: PESSI / SESSI / BESSI / ESSI min and max, EOBI max, age limits, pro-rata.

| Screen | Key details | Us |
|---|---|---|
| Salary setup | Monthly, hourly or daily salary; breakup items (fixed or %), per-item: include in monthly salary, leave applicable, present on holidays, taxable, deduction method; OT rates (WD/holiday/gazetted/special), monthly OT limit, short-time rate; payment method (bank/cash/split %), employer bank, transaction type; recurring deductions; tax deduction toggle; Excel | 🟡 |
| Process payroll | Pick setup + period, preview, save, disburse, un-disburse, delete, move to FnF, send payslips by email, approval routing | 🟡 (runs, no disburse/approval) |
| Payslip approvals | Approve payroll before disbursement | ⬜ |
| Payroll approvals | One queue: advance, OT, allowance, deduction, bonus, reimbursement, commission, salary change, loan, PF loan, loan adjustment, leave encashment, tax adjustment, piece unit, PF withdrawal, allowance change, hold salary, gratuity withdrawal | 🟡 |
| Allowance / deduction / bonus | Year+month, type, taxable with tax %, days count or unit-wise; bonus as fixed or % of salary for many employees; Excel | ✅ (no tax % per item) |
| Recurring allowances / deductions | Start month/year, condition, leave applicable, taxable | ⬜ |
| Overtime request | Prior OT (pre-approval) vs actual, multiple employees, hours per OT type | ✅ |
| Advance salary | Title, date, amount, paid through; Excel | ✅ |
| Loans | General / PF / gratuity loan; loan types with criteria (applicable from joining or confirmation, employer bears %, cap, markup/annual rate, processing fee, visibility); repayment schedule; adjustments (prepay, change installment, markup/principal) | 🟡 |
| Commission, reimbursement | Commission per month; reimbursement lines with category, item, receipt no | ⬜ |
| Salary changes | Increment/decrement by amount or %, allowance changes, Excel export/import | ⬜ |
| Leave encashment / settlement | Company or employee wise, period, process taken leave, include in payslip, taxable | ⬜ |
| Tax adjustment | Opening tax, withholding tax, tax adjustment types (tax credit, deductible allowance, increase annual salary) | ⬜ |
| Provident fund / VPS / profit fund | Fund setup per employee, opening PF, PF withdrawal, profit distribution | ⬜ |
| Gratuity withdrawal | Against gratuity balance | ⬜ |
| Hold salary | Hold / release by month | ⬜ |
| Salary distribution | Split salary cost across stations by % | ⬜ |
| Fiscal year, forex rates, CPR, allowance rate, withholding tax types | Settings lookups | 🟡 (fiscal year in settings) |

## 7. Reports (136 in the catalog)

Every report has filters, PDF/Excel export and optional signatures. Grouped:
- **Attendance (≈45)**: detail, summary, monthly summary, register (+detail), late comers, early comers, early leavers,
  absent, missing, breaks (details/logs), manual entry/logs, machine logs, work schedule, shift report, shift history,
  shift plan, irregular shift, consecutive days, location detail, location-wise activity, physical attendance,
  activity logs, turnover, sandwich deduction, attendance close, attendance/exemption/off-day request reports, worksheet
  summary/detail, attendance analysis, weekly schedule template.
- **Leave (≈10)**: summary, quota, balance, ledger, trend (graphical, top 10), request reports, compensation leave.
- **Payroll (≈55)**: payslip, payroll summary, salary sheet, salary report, salary advice + **cheque print**, bank
  transfer formats, WPS, net payables, salary payout, payroll comparison (and cost-center wise), payroll analysis,
  yearly/monthly tax, tax certificate, tax liability, tax reconciliation, FBR employee-wise tax, yearly tax computation,
  EOBI, social security, PF ledger/report, VPS, gratuity balance/ledger, loan balance/ledger/schedule, advance salary,
  overtime (summary, register, payout, detail, manual analysis), bonus, allowance, deduction, reimbursement, penalty,
  short/excess time, daily wages, hold salary, increment/decrement, salary history, salary change, leave encashment,
  salary distribution, payroll approval view. Payslips are password-protected (verify user before viewing).
- **HR (≈20)**: employee profile, list, history, matrix, statistics, card (ID cards), department head count, employee
  department, roles, documents, dependants, academics, location, utilization, transfers, resignation summary/detail,
  assets, polls, suggestions.
- **Separation, helpdesk, recruitment, performance, expense, travel, desk, training, piece work, budget, integration** reports.

Us: 🟡 exports only. Biggest gap after approvals: a generic report runner (filters + PDF/Excel + signatures) and the top
20 reports (attendance detail/summary/register, late/absent, leave balance/ledger, payslip, salary sheet, bank advice,
tax, EOBI, loan ledger, employee list/profile, head count).

## 8. Performance

- **Setup**: setup type upper-limit or percentage based; section-wise; **9-box or 12-box matrix** (X/Y sections);
  KPI choices (score or multiple choice); show weightage at section/objective/KPI/sub-KPI levels; KPI dates; frequency;
  bell curve templates (% of employees per rating); raise suggestion policy (score slab → raise %); what reviewers can
  see/do (others' evaluations, feedback to manager, overall rating, attachments, revert, strengths, areas of
  development, development plan) with per-stage restrictions; approval routing; clone setup.
- **Templates and libraries**: performance templates (sections, weightage, order, visibility, who may write objectives,
  limits on objectives/KPIs, grace marks, comment rules); KPI, section and objective libraries (Excel); review categories;
  performance year start month.
- **Process**: write KPIs/objectives (with approval) → create review (self, line manager, additional employee, final,
  modifier reviews each with start date and deadline; annual/bi-annual/quarterly/monthly) → evaluations (score, grace,
  comments, attachments, revert) → sign-off by employee (accept/reject) → process closure.

Us: ⬜

## 9. Recruitment

- Job request (vacant or replacement position, number of positions, age and salary range, gender, qualification,
  target joining date, attachment) with approval.
- Job post (copy from request, pinned/filters, closing date, publish to career portal, assign recruiter, pipeline,
  job-opening field template, shareable link).
- Candidates: manual, Excel, **bulk CV folder import with resume parsing**, transcripts; personal, qualification,
  experience, documents; blacklist/whitelist; source (walk-in, advertisement, reference).
- **Screening board**: per job post, columns Applied → Manager shortlist → List for test → Shortlisted → Interviews 1–5 →
  Hiring approval → Offer → Recruited / Rejected / Dropped out / Hold; shortlisting score by gender, nationality,
  qualification, experience, keywords; email candidates; move between pipeline stages.
- Interviews: onsite/online (Google Meet, Teams, custom link), interviewer, template, attachments; evaluation with
  question marks; reschedule; hiring approvals; recruitment approvals.
- Settings: interview templates (questions by category, marks), pipeline templates, skill sets, questionnaire
  categories, recruitment designations, job-opening field templates, approvals, career-portal branding (logo, banner,
  agreement note), re-apply after N months, guest apply, candidate reschedule.
- **Candidate portal**: sign up with OTP, profile, apply, see tasks/applications, respond to offer, reschedule interview.

Us: ⬜

## 10. Letters

Employee letters and candidate letters, letter approvals, letter setup (type single/multiple employees, category,
header/body/footer editor with placeholders and data tables, custom variables, page margins, header/footer on every
page, approval chain, email on submission and final approval, and **auto-use** in recruitment offers, transfers, salary
changes and exits).

Us: ✅ core; ⬜ approvals, candidate letters, auto-use in other modules, data tables, margins.

## 11. Expense

Expense request (multi-line: category, type, amount or quantity, distance from-to, attachment, include in payslip,
against an advance), advance expense, advance adjustment (adjust or increment), approvals with approved amount per line.
Settings: categories (advance or not, include in payslip, own approval chain), types (quota, pro-rata, carry forward,
fixed days, quantity based, distance, unit, calculation type, gender/status/group/designation/grade scope, accrual,
limits, taxable, multi-year quota), rates per month (copy, Excel), quotas (company or employee wise), entitlements,
distance mapping between cities, quota year start.

Us: ✅ claims; ⬜ advance expense, quotas, rates, distance, entitlements.

## 12. Employee separation

Employee exit (exit type, resignation and leaving date, position, clearance template with up to 10 levels, exit
interview with template, panel and date, restrict login), clearance approvals, exit interviews, separation approvals,
final settlement (run FnF per payroll setup and period, disburse month, preview, approval), clearance templates, exit
interview questionnaire categories and templates, settings.

Us: ✅ core flow; ⬜ templates, 10-level clearance, panel interviews, FnF approval and disbursement.

## 13. Help desk

Ticket request (raised for, category, type, priority, status, attachments, stats bar, cancel, history, reply by email
inbox), resolution panel (assign, reassign, reminders, involve employees, update status, remarks), involvement panel.
Settings: categories (agent may close, auto close after, restrict reopen), ticket types (agent type, group or agent,
**target resolution hours**, lead assignment rules, who to involve, email triggers, edit/delete comment), groups with
leads, custom status and priority names.

Us: ⬜

## 14. Employee assets

Asset request and return request (with approval and separate hand-over/take-over routing), approvals, hand-over and
take-over (ref no, serial no, permanent or temporary), categories, types (allow multiple, hand-over/take-over
responsible), templates (bundle assigned on joining), Excel, asset report.

Us: ⬜

## 15. Travel

Travel request in steps: general details, departure/arrival, travel mode, pick & drop, accommodation, travel advance
expense; one-way or return; reason types; approvals with minimum notice days per approval level.

Us: ⬜

## 16. Manpower budgeting

Planning (new and existing positions, monthly salary, annual gross, financial impact, import existing employees),
employee budgeting, open positions (include/change positions, approved strength vs vacant vs on-roll), position transfer,
budget approvals, budget duration, position titles (key roles, JD, competencies), employee positions (code, division,
grade, cost center, reports to, min/max salary), settings.

Us: ⬜

## 17. Onboarding

Onboarding per new joiner (change station, reporting time, contact person, instructions, tasks), templates (tasks with
category, assignee, due after N days, notification, attachment), approvals, task clearance, categories,
**welcome-onboard portal** for the new employee (first day info, fill own profile, get introduced, change password),
team-introduction questionnaire and email.

Us: ⬜

## 18. Training

Training request, approval, schedule (internal or external trainer), feedback form, categories, templates (frequency,
completion due within, links, attachments), external trainers, settings.

Us: ⬜

## 19. Desk management

Offices (lat/long, radius), floors, rooms (type, seats), desk request (office, floor, room, date/time range, seats).

Us: ⬜

## 20. Piece work

Piece-work request (category, type, count, date, attachment), approvals, process piece work (like a payroll run with
disburse and payslips), process approvals, categories, types, rates per employee (with per-day production), settings.

Us: ⬜

## 21. Scheduled reports and alerts

- Scheduled report wizard: report → employees → recipients (up to 4 levels) → frequency (every N days/weeks, on
  weekday, on date, first/last day of month, time) → data window.
- Scheduled alerts: alert type (e.g. after N days of joining, event date ± days) → employees → recipients → schedule;
  editable email body with inserted fields.

Us: ⬜

## 22. Email management

Email logs (success, failed, limit reached), email setup per module/form/trigger (and per recruitment pipeline stage)
with header/subject/body/footer, monthly email quota, SMTP credentials with test send.

Us: 🟡 SMTP + notifications; ⬜ editable templates, logs, quota.

## 23. Integrations

Chart of accounts, GL codes, GL mapping for allowances/deductions/expenses/loans/advances, **payroll JV export (Oracle,
SAP formats)**, module-wise integration and compliance APIs with field mapping, API login tokens, Muqeem (Saudi Iqama
issue/renew, exit/re-entry visas), Abhi (earned wage access) employee mapping.

Us: ⬜

---

## Suggested build order (biggest value first)

1. **Shared engine upgrades**: force-approve, delegation, "on behalf of", global filters, report runner with PDF/Excel
   and signatures, per-screen change history, Excel import framework for every list.
2. **Attendance depth**: flag rules per shift, penalty rules, exemptions, off-day and swap requests, attendance close,
   day/month close, amend shift/OT, biometric device sync.
3. **Payroll depth**: payroll setups (multiple), disburse/un-disburse + payslip approval, salary changes, recurring
   items, tax adjustments, PF/VPS/gratuity, hold salary, leave encashment, bank advice and cheque print, top payroll reports.
4. **Leave depth**: sandwich/club rules, accrual and encashment methods, reason types, quota bulk create, opening balances, station leave.
5. **Organization**: constants (cost center, grade, division, station, region…), announcements, polls, projects/tasks + worksheet, custom fields.
6. New modules in order: Help desk → Assets → Onboarding → Performance → Recruitment (+ candidate portal) → Travel →
   Training → Scheduled reports/alerts → Manpower budgeting → Piece work → Desk → Integrations.
