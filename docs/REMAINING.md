# FlowHCM parity: what is left

Working checklist (from docs/FLOWHCM_FEATURES.md, checked against the code on 2026-10-23). Each batch is built,
tested and committed locally; one production deploy at the end. Tick items as they land.

## A. Employee profile and organization lookups
- [x] Lookups: divisions, grades, cost centers, regions/stations, employee groups, sub-departments (code on department)
- [x] Employee fields: father name, place of birth, nationality, religion, CNIC issue/expiry, passport (+dates), visa (+dates), NTN, EOBI no, social security no, health insurance (title, policy, dates), contract start/end, confirmation date, division/grade/cost center/region/group/sub-department
- [x] Dependants (relation, CNIC, DOB, nominee), academics (qualification, institute, field, grade, dates), work history (organization, designation, dates, salary, reason)
- [x] Employee list filters by the new lookups; reports filter by them
- [x] Inactivate with exit type/reason, blacklist flag

## B. Attendance depth
- [x] Shift flag rules: early-leave grace, half-day and short-day minutes, earliest/latest check-in, break minutes; Early and ShortDay flags
- [x] Holiday types: public/gazetted vs special, office-specific, religion-specific, exempted employees
- [x] Off-day request (change weekly off from a date) through approvals
- [x] Penalty rules: flag + count → deduct leave or pay, once or every occurrence, exempted count
- [x] Projects and tasks; worksheet/timesheet (daily lines, hours) with approval; timesheet report
- [x] Per-employee location / IP restriction for check-in

## C. Shared engine
- [x] "On behalf of" requests (HR and managers apply leave, attendance, expense, overtime, advance for someone)
- [x] Suggestions box (category, anonymous, routed to reviewers)
- [x] Role templates: per-screen permission sets assigned to users
- [x] Global search / quick access over all screens, pinned favourites
- [x] Per-screen change history popup (from audit log)

## D. Payroll depth
- [ ] Provident fund (employee/employer %, balance ledger, opening balance, withdrawal), gratuity (accrual, withdrawal)
- [ ] Tax adjustment (opening tax/previous employer, withholding credit, tax credit), tax certificate
- [ ] Commission requests; un-disburse / reopen; payslip email to all; cheque print report
- [ ] Payment method split (bank/cash), salary distribution across cost centers
- [ ] Social security (PESSI/SESSI) contribution settings
- [ ] Overtime rules by day type (working day / holiday) with rounding and minimum minutes

## E. Expense depth
- [ ] Configurable categories/types with yearly quotas and rates (per km, per unit), distance mapping
- [ ] Advance expense and adjustment against claims

## F. Module depth
- [ ] Separation: clearance templates (multi-level by department/person), exit interview templates, FnF approval
- [ ] Letters: approvals, candidate letters, auto-use (offer, transfer, salary change, exit), page margins
- [ ] Recruitment: candidate portal (status by email link), interview templates with marks, shortlisting score, offer letter PDF
- [ ] Performance: KPI library, multiple reviewers, quarterly check-ins
- [ ] Helpdesk: (done except email reply)

## G. Organization and system
- [ ] Day/month close (lock attendance and payroll edits for a period)
- [ ] ID card template and print
- [ ] Biometric devices (device list + push endpoint with token)
- [ ] Email templates (editable per event), email log, monthly quota
- [ ] Allowed email domains; last-login report; exception log
- [ ] Integrations: chart of accounts, GL mapping, payroll JV export (CSV), API tokens

## H. Dashboard and UX
- [ ] Dashboard tabs (ESS, attendance, payroll, recruitment), organogram, birthday wishes, request from missing day
- [ ] Urdu translation with RTL
