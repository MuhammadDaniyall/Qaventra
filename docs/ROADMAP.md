# Qaventra roadmap: FlowHCM module parity

Source: the 18 FlowHCM 5.10 user manuals (Asset, Attendance, Desk, Employee, Separation, Expense, Helpdesk, Leave, Letter,
Manpower Budgeting, Onboarding, Organization, Payroll, Performance, Recruitment, Scheduled Alert, Scheduled Report, Travel).
Goal: every feature they list, built cleaner and more modern (self-service first, mobile-friendly, live previews, fewer forms).

Status: ✅ done · 🟡 partly · ⬜ not started

## Phase 0: shared engine (every module uses these)

The manuals repeat the same five patterns in almost every module. Build once, reuse everywhere.

| Piece | What it gives every module | Status |
|---|---|---|
| Approval workflows | Per request type: auto-approve, line manager, manager's manager, HR, specific employee, in any order (up to 5 steps); approve/reject with notes; one "Approvals" inbox; timeline on every request | ✅ for leave and expenses; each new module adds its request type |
| Attachments | Files on leave requests and separations, employee document folder with type and expiry date; PDF/images/Word/Excel up to 5 MB; open in browser | ✅ (expense receipts kept on claim items) |
| Notifications | In-app bell (refreshes every minute) + email via SMTP for approvals needed, decisions, resignations, letters, payslips; per-user email switch | ✅ (email needs Smtp settings; editable templates later) |
| Excel import/export | Templates, row-by-row check, all-or-nothing import for employees and attendance; exports for employees, attendance, leave, expenses, payroll (bank-ready) | ✅ (more imports per module as they land) |
| Reports | "View report" on every grid: filter, print, export | ⬜ |

## Phase 1: finish what exists

| Module | Have | Still to build |
|---|---|---|
| Employee | Profile, department, designation, manager, salary, login, documents, Excel import/export, personal email/marital status/blood group, emergency contacts, profile change requests with approval, job history (promotions, transfers, new manager), re-hire, bulk logins with forced password change, password reset | Custom fields, role templates, suggestions box |
| Attendance | Geo check-in/out, shifts, holidays, daily view, manual entry, Excel import/export, attendance requests (missed punch, work from home, official duty, time correction) through approval chains, roster (shift or day off per person per day, bulk planning), shift change and swap requests (colleague agrees, then approval), late and half-day penalties on the payslip, monthly summary and attendance close | Breaks, exemptions, biometric devices |
| Leave | Types, balances, apply, approval chains, attachments, half day and short leave, policies (monthly accrual, carry forward, max days, notice, gender, probation), HR balance adjustments, comp-off for weekend/holiday work | Reason types, leave quota bulk upload, encashment requests |
| Payroll | Runs, payslips, tax slabs, EOBI, Excel export, expense reimbursements, HR allowances/bonuses/deductions per month, overtime claims (rate x hourly basic), advance salary, loans with automatic installments | Pay schedule, commission, PF/gratuity loans, provident fund & VPS, gratuity, hold salary, salary change requests, bank transfer files, payslip email, opening tax, payroll approvals |
| Organization | Departments (with head, usable as an approval step), designations, offices, shifts, holidays, announcements (audience, schedule, pin, files, read counts), polls (audience, anonymous, results rules), custom employee fields | Projects & tasks, day close, document management, ID card templates, system/exception logs, last-login report |

## Phase 2: new modules (started 2026-10-20)

| Module | Built | Still to build (from manual) |
|---|---|---|
| Letters | ✅ Templates with placeholders, issue with reference no., print on letterhead, employee "My documents" | Header/footer/logo designer, custom variables, letter approvals, email letter, candidate letters (needs Recruitment) |
| Separation | ✅ Resign → accept → clearance checklist → exit interview → final settlement → complete (status, login off) | Clearance and exit-interview templates, questionnaire categories, multi-level clearance routing, FnF approval and disbursement month, bulk upload |
| Expense | ✅ Multi-line claims with receipts, manager/HR approval, paid through payroll or final settlement | Configurable categories/types, quotas, rates (per km etc.) and distance mapping, advance expense and adjustment, include-in-payroll switch, bulk upload |

## Phase 3: remaining modules

| Module | Main features |
|---|---|
| Helpdesk | **Done:** categories with agents, lead, target hours and rules (agent may close, auto-close days, reopen window); ticket types with their own target and agents; automatic assignment (in turn or least busy); tickets with priority, files, replies, internal notes, editing replies; take/assign, involve colleagues; waiting/resolved/closed with rating, reopen; due-soon reminders and overdue escalation to the lead; company names for statuses and priorities; reports (first answer, time to fix, within target, ratings by category and agent) and Excel export. Not yet: replying by email |
| Asset | **Done:** types (one or many per person, return on exit, handlers), inventory with tag, serial, cost, warranty, condition and status (available, assigned, repair, retired, lost) and full history; hand-over and take-over with condition and notes; temporary loans with late-return flag; requests for new assets through approval and return requests straight to the handler; starter kits; employee Assets tab; assets to return on the separation page; Excel export |
| Onboarding | **Done:** templates (tasks with category, who does it: HR, manager, buddy, the joiner or a named person, due days from joining; default template starts for every new employee; optional starter kit), first-day details (time, place, buddy, instructions), task checklists with late flags and auto-complete, welcome page for the joiner with profile checklist, introduction questionnaire posted as a welcome announcement |
| Travel | Multi-step travel request (route, mode, pick & drop, accommodation), travel advance, reason types |
| Performance | **Done:** rating names, bell-curve targets and raise steps; templates with weighted sections (own goals or shared competencies) and optional self review; review cycles for everyone or departments with stage deadlines; goals written by the employee and approved by the manager; self and manager scores with comments, strengths, development plan and potential; weighted scoring; HR calibration with 9-box, bell curve against target and suggested raise; release, employee sign-off (agree/disagree), send back a stage. Next: KPI libraries, multiple reviewers, quarterly check-ins |
| Recruitment | Job requests, job posts, public careers portal, candidate pipeline, CV parsing, shortlisting, interviews, offer, hire → employee |
| Manpower budgeting | Yearly position planning, employee budgeting, open/changed/transferred positions, approvals |
| Desk management | Offices/floors/rooms, desk booking with availability |
| Scheduled alerts | Birthdays, anniversaries, absences, probation end, document expiry: who, when (before/after), email template |
| Scheduled reports | Pick a report, filter employees, recipients, daily/weekly/monthly delivery |

## Where we aim to beat FlowHCM

- Mobile-first self-service (PWA): check-in, leave, claims with camera receipts, payslips, letters.
- Live previews everywhere (letters, settlement, payroll) instead of submit-then-check.
- One approvals inbox across modules instead of an approvals tab per module.
- Payroll pulls approved items (expenses, overtime, loans) automatically.
