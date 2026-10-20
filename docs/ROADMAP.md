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
| Excel import/export | Download sample template, upload, validate row by row, export any grid | ⬜ |
| Reports | "View report" on every grid: filter, print, export | ⬜ |

## Phase 1: finish what exists

| Module | Have | Still to build |
|---|---|---|
| Employee | Profile, department, designation, manager, salary, login | Documents, additional/custom fields, profile change requests with approval, transfers, re-hire, send credentials, role templates, suggestions box, bulk upload/export |
| Attendance | Geo check-in/out, shifts, holidays, daily view, manual entry | Breaks, irregular-attendance requests, exemptions, remote-work requests, shift requests/swaps, roster schedules, penalties, attendance close, biometric devices |
| Leave | Types, balances, apply, approve | Half/short leave, attachments, compensatory (overtime) leave, quota policies (accrual, carry-forward), reason types, bulk upload |
| Payroll | Runs, payslips, tax slabs, EOBI | Pay schedule, allowances/deductions/bonus/commission requests, overtime, loans (incl. PF/gratuity), advance salary, provident fund & VPS, gratuity, hold salary, salary change requests, salary distribution (bank files), payslip email, opening tax, payroll approvals |
| Organization | Departments, designations, offices, shifts, holidays | Announcements, polls, projects & tasks, month/day close, custom fields, document management, ID card templates, system/exception logs, last-login report |

## Phase 2: new modules (started 2026-10-20)

| Module | Built | Still to build (from manual) |
|---|---|---|
| Letters | ✅ Templates with placeholders, issue with reference no., print on letterhead, employee "My documents" | Header/footer/logo designer, custom variables, letter approvals, email letter, candidate letters (needs Recruitment) |
| Separation | ✅ Resign → accept → clearance checklist → exit interview → final settlement → complete (status, login off) | Clearance and exit-interview templates, questionnaire categories, multi-level clearance routing, FnF approval and disbursement month, bulk upload |
| Expense | ✅ Multi-line claims with receipts, manager/HR approval, paid through payroll or final settlement | Configurable categories/types, quotas, rates (per km etc.) and distance mapping, advance expense and adjustment, include-in-payroll switch, bulk upload |

## Phase 3: remaining modules

| Module | Main features |
|---|---|
| Helpdesk | Tickets with category/type/priority, agent groups, assignment, replies, involve employees, target hours, status bar |
| Asset | Categories/types/templates, asset requests and returns, hand-over/take-over with serial numbers, temporary assets |
| Onboarding | Onboarding templates with tasks/assignees/due dates, first-day instructions, new-joiner questionnaire, task clearance |
| Travel | Multi-step travel request (route, mode, pick & drop, accommodation), travel advance, reason types |
| Performance | KPIs/objectives, review cycles, self and manager evaluation, 9-box matrix, bell curve, sign-off |
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
