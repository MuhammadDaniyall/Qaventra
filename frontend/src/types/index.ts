// Mirrors the backend DTOs. Enums are serialized as strings; DateOnly is "YYYY-MM-DD"; TimeOnly is "HH:mm:ss".

export type UserRole = 'SuperAdmin' | 'CompanyAdmin' | 'HrManager' | 'Manager' | 'Employee';
export type Gender = 'Male' | 'Female' | 'Other';
export type EmploymentStatus = 'Probation' | 'Active' | 'Resigned' | 'Terminated';
export type MaritalStatus = 'Single' | 'Married' | 'Divorced' | 'Widowed';
export const MARITAL_STATUSES: MaritalStatus[] = ['Single', 'Married', 'Divorced', 'Widowed'];
export const BLOOD_GROUPS = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
export type AttendanceStatus = 'Present' | 'Late' | 'HalfDay' | 'Absent' | 'OnLeave' | 'ShortDay';
export type AttendanceSource = 'Web' | 'Mobile' | 'Biometric' | 'Manual' | 'Request';
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export type SalaryComponentKind = 'Earning' | 'Deduction';
export type AuditCategory = 'Auth' | 'Request' | 'Data';
export type PayrollRunStatus = 'Draft' | 'Finalized' | 'Submitted' | 'Approved';
export type BillingCycle = 'Monthly' | 'Quarterly' | 'HalfYearly' | 'Yearly';
export type TenantAccess = 'Allowed' | 'Inactive' | 'Expired';
export const BILLING_CYCLES: BillingCycle[] = ['Monthly', 'Quarterly', 'HalfYearly', 'Yearly'];
/** Access-blocking codes sent in 403 ProblemDetails. */
export type AccessCode = 'CompanyInactive' | 'SubscriptionExpired' | 'UserInactive';

export type DayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

export const ROLES: UserRole[] = ['SuperAdmin', 'CompanyAdmin', 'HrManager', 'Manager', 'Employee'];
export const EMPLOYMENT_STATUSES: EmploymentStatus[] = ['Probation', 'Active', 'Resigned', 'Terminated'];
export const DAYS_OF_WEEK: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

// ---- Auth ----
export interface Me {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  tenantId: string | null;
  companyName: string | null;
  employeeId: string | null;
  /** True when the platform owner (SuperAdmin) has opened a company. */
  isActingAsOwner: boolean;
  /** Company users: last day of the subscription (inclusive); null = no expiry. */
  subscriptionEndsOn: string | null;
  /** HR issued a temporary password; the user must choose their own before going on. */
  mustChangePassword: boolean;
  /** Access template modules; null = the role's usual access. */
  modules?: string[] | null;
}

export interface Session {
  token: string;
  expiresAtUtc: string;
  user: Me;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

// ---- Common ----
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Lookup {
  id: string;
  name: string;
}

// ---- Platform ----
export interface Company {
  id: string;
  name: string;
  isActive: boolean;
  /** Computed: Inactive (switched off) or Expired (end date passed). */
  access: TenantAccess;
  createdAtUtc: string;
  phone: string | null;
  city: string | null;
  address: string | null;
  /** Licensed seats; null = unlimited. */
  maxEmployees: number | null;
  billingCycle: BillingCycle | null;
  /** Fixed base fee per cycle. */
  planPrice: number | null;
  /** Per licensed employee per month. */
  pricePerEmployee: number | null;
  /** Computed bill for one cycle. */
  amountPerCycle: number | null;
  /** Inclusive last day; null = no expiry. */
  subscriptionEndsOn: string | null;
  /** Negative when expired. */
  daysLeft: number | null;
  lastPaymentOn: string | null;
  /** Active + probation employees. */
  employees: number;
  users: number;
  adminName: string | null;
  adminEmail: string | null;
  lastActivityUtc: string | null;
}

export interface CompanyPlan {
  maxEmployees: number | null;
  billingCycle: BillingCycle | null;
  planPrice: number | null;
  pricePerEmployee: number | null;
  subscriptionEndsOn: string | null;
}

export interface Payment {
  id: string;
  amount: number;
  paidOn: string;
  periodFrom: string;
  periodTo: string;
  method: string | null;
  reference: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export interface RecordPaymentRequest {
  amount: number;
  paidOn: string;
  periods: number;
  method: string | null;
  reference: string | null;
  notes: string | null;
}

export interface CreateCompanyRequest extends CompanyPlan {
  name: string;
  /** Optional: when both are omitted the server names the admin "<Company name> Super Admin". */
  adminFirstName?: string;
  adminLastName?: string;
  adminEmail: string;
  /** null = generate one on the server. */
  adminPassword: string | null;
  phone: string | null;
  city: string | null;
  address: string | null;
}

/** PUT replaces every field, so always send the full set. */
export interface UpdateCompanyRequest extends CompanyPlan {
  name: string;
  phone: string | null;
  city: string | null;
  address: string | null;
}

/** generatedPassword is set only when the server created it: show once, never store. */
export interface CompanyAdminCredentials {
  company: Company;
  adminEmail: string;
  generatedPassword: string | null;
}

export interface PlatformStats {
  companies: number;
  activeCompanies: number;
  inactiveCompanies: number;
  expiredCompanies: number;
  /** Ending within 7 days. */
  expiringSoon: number;
  employees: number;
  users: number;
  revenueThisMonth: number;
  revenueThisYear: number;
  loginsToday: number;
  failedLoginsToday: number;
  eventsToday: number;
  expiringCompanies: Company[];
}

// ---- Audit ----
export interface AuditLog {
  id: number;
  timestampUtc: string;
  category: AuditCategory;
  tenantId: string | null;
  companyName: string | null;
  userId: string | null;
  userEmail: string | null;
  role: UserRole | null;
  action: string;
  entityType: string | null;
  entityId: string | null;
  changes: string | null;
  path: string | null;
  statusCode: number | null;
  durationMs: number | null;
  ipAddress: string | null;
  userAgent: string | null;
  correlationId: string | null;
}

export interface AuditLogQuery {
  category?: AuditCategory | '';
  tenantId?: string;
  user?: string;
  action?: string;
  entityType?: string;
  fromUtc?: string;
  toUtc?: string;
  page?: number;
  pageSize?: number;
}

// ---- Organization ----
export interface Department {
  id: string;
  name: string;
  code: string | null;
  employees: number;
  headEmployeeId: string | null;
  head: string | null;
}
export interface SaveDepartmentRequest {
  name: string;
  code: string | null;
  headEmployeeId?: string | null;
}

export interface Designation {
  id: string;
  title: string;
  employees: number;
}
export interface SaveDesignationRequest {
  title: string;
}

// ---- Setup ----
export interface OfficeLocation {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  radiusMeters: number;
}
export type SaveOfficeLocationRequest = Omit<OfficeLocation, 'id'>;

export interface Shift {
  id: string;
  name: string;
  startTime: string;
  endTime: string;
  graceMinutes: number;
  weeklyOffDays: DayOfWeek[];
  isDefault: boolean;
  /** Leaving more than this many minutes early counts as an early leave. */
  earlyLeaveGraceMinutes: number;
  /** Worked under this many minutes = half day (null: half the shift). */
  halfDayMinutes: number | null;
  /** Worked under this many minutes = short day (null: no short days). */
  shortDayMinutes: number | null;
  /** How early before the start check-in opens (null: any time). */
  earliestCheckInMinutes: number | null;
  breakMinutes: number;
}
export type SaveShiftRequest = Omit<Shift, 'id'>;

export interface Holiday {
  id: string;
  date: string;
  name: string;
  kind: 'Public' | 'Special';
  /** Region/station ids it applies to; empty = everywhere. */
  regionIds: string[];
  religion: string | null;
  /** People who work that day anyway. */
  exemptEmployeeIds: string[];
}
export type SaveHolidayRequest = Omit<Holiday, 'id'>;

export type LeaveAccrual = 'Upfront' | 'Monthly';
export type LeaveDuration = 'FullDay' | 'HalfDay' | 'ShortLeave';
export type HalfDayPart = 'FirstHalf' | 'SecondHalf';

export interface LeaveType {
  id: string;
  name: string;
  code: string;
  annualQuota: number;
  isPaid: boolean;
  isActive: boolean;
  accrual: LeaveAccrual;
  /** Most unused days moved into next year; 0 = none. */
  carryForwardMax: number;
  maxConsecutiveDays: number | null;
  minNoticeDays: number;
  applicableGender: Gender | null;
  allowedOnProbation: boolean;
  allowHalfDay: boolean;
  allowShortLeave: boolean;
  /** Credited by approved comp-off requests instead of a yearly quota. */
  isCompensatory: boolean;
  /** Offs and holidays inside a request count as leave. */
  sandwich: boolean;
  /** Longer requests need a supporting document before approval. */
  documentAfterDays: number | null;
}
export type SaveLeaveTypeRequest = Omit<LeaveType, 'id'>;

// ---- Employees ----
export interface EmployeeListItem {
  id: string;
  employeeCode: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  department: string | null;
  designation: string | null;
  status: EmploymentStatus;
  joiningDate: string;
  hasLogin: boolean;
}

export interface Employee {
  id: string;
  employeeCode: string;
  firstName: string;
  lastName: string;
  email: string | null;
  phone: string | null;
  cnic: string | null;
  dateOfBirth: string | null;
  gender: Gender | null;
  joiningDate: string;
  leavingDate: string | null;
  status: EmploymentStatus;
  address: string | null;
  bankName: string | null;
  bankAccount: string | null;
  personalEmail: string | null;
  maritalStatus: MaritalStatus | null;
  bloodGroup: string | null;
  departmentId: string | null;
  department: string | null;
  designationId: string | null;
  designation: string | null;
  managerId: string | null;
  manager: string | null;
  shiftId: string | null;
  shift: string | null;
  userId: string | null;
  loginRole: UserRole | null;
  loginActive: boolean | null;
}

export interface SaveEmployeeRequest {
  firstName: string;
  lastName: string;
  joiningDate: string;
  employeeCode: string | null;
  email: string | null;
  phone: string | null;
  cnic: string | null;
  dateOfBirth: string | null;
  gender: Gender | null;
  status: EmploymentStatus;
  leavingDate: string | null;
  address: string | null;
  bankName: string | null;
  bankAccount: string | null;
  personalEmail: string | null;
  maritalStatus: MaritalStatus | null;
  bloodGroup: string | null;
  departmentId: string | null;
  designationId: string | null;
  managerId: string | null;
  shiftId: string | null;
}

export interface EmployeeQuery {
  search?: string;
  departmentId?: string;
  status?: EmploymentStatus | '';
  /** Only people who have left (resigned or terminated). */
  former?: boolean;
  withoutLogin?: boolean;
  /** Division, grade, cost center, region, group or sub-department id. */
  groupId?: string;
  page?: number;
  pageSize?: number;
}

export interface EmergencyContact {
  name: string;
  relation: string;
  phone: string;
  altPhone: string | null;
}

export type JobChangeKind = 'Transfer' | 'Promotion' | 'Demotion' | 'Redesignation' | 'ManagerChange' | 'Rehire' | 'ProfileEdit';

export interface JobChange {
  id: string;
  kind: JobChangeKind;
  effectiveDate: string;
  fromDepartment: string | null;
  toDepartment: string | null;
  fromDesignation: string | null;
  toDesignation: string | null;
  fromManager: string | null;
  toManager: string | null;
  note: string | null;
  recordedByName: string | null;
  createdAtUtc: string;
}

export interface IssuedLogin {
  employeeId: string;
  name: string;
  email: string;
  /** Only when it could not be emailed; shown once. */
  password: string | null;
}

export interface IssueLoginsResult {
  emailed: boolean;
  created: IssuedLogin[];
  skipped: { employeeId: string; name: string; reason: string }[];
}

export interface FieldChange {
  field: string;
  label: string;
  from: string | null;
  to: string | null;
}

export interface ProfileChange {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  changes: FieldChange[];
  reason: string | null;
  status: LeaveStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
}

export interface CreateLoginRequest {
  email: string;
  password: string;
  role: UserRole;
}

export interface UpdateLoginRequest {
  role: UserRole;
  isActive: boolean;
}

export interface SalaryComponent {
  name: string;
  kind: SalaryComponentKind;
  amount: number;
  isTaxable: boolean;
}

export interface Salary {
  id: string | null;
  effectiveFrom: string;
  basicSalary: number;
  eobiApplicable: boolean;
  components: SalaryComponent[];
}

// ---- Attendance ----
export interface Attendance {
  id: string;
  employeeId: string;
  date: string;
  /** Company-local date-time, no timezone suffix. */
  checkIn: string;
  checkOut: string | null;
  status: AttendanceStatus;
  source: AttendanceSource;
  workedMinutes: number;
  office: string | null;
  /** Why it was entered or changed, e.g. from an approved request. */
  note: string | null;
  lateMinutes: number;
  earlyLeaveMinutes: number;
}

export interface Today {
  date: string;
  record: Attendance | null;
  shiftName: string | null;
  shiftStart: string | null;
  shiftEnd: string | null;
  locationRequired: boolean;
}

export interface DailyRow {
  employeeId: string;
  employeeCode: string;
  fullName: string;
  department: string | null;
  record: Attendance | null;
  leaveType: string | null;
}

export interface PunchRequest {
  latitude: number | null;
  longitude: number | null;
  source: AttendanceSource;
}

export interface ManualAttendanceRequest {
  employeeId: string;
  date: string;
  checkIn: string;
  checkOut: string | null;
}

// ---- Leave ----
export interface LeaveRequest {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  leaveTypeId: string;
  leaveType: string;
  fromDate: string;
  toDate: string;
  days: number;
  reason: string | null;
  status: LeaveStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  attachmentCount: number;
  duration: LeaveDuration;
  halfDayPart: HalfDayPart | null;
  /** Short leave only, "HH:mm:ss". */
  startTime: string | null;
  endTime: string | null;
}

export interface LeaveBalance {
  leaveTypeId: string;
  leaveType: string;
  code: string;
  /** The year's quota. */
  entitled: number;
  /** How much of the quota is earned so far (equals entitled unless accrual is monthly). */
  earned: number;
  carriedForward: number;
  adjustment: number;
  adjustmentNote: string | null;
  /** From approved comp-off requests. */
  credited: number;
  used: number;
  pending: number;
  available: number;
  accrual: LeaveAccrual;
  isCompensatory: boolean;
}

export interface ApplyLeaveRequest {
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  reason: string | null;
  duration?: LeaveDuration;
  halfDayPart?: HalfDayPart | null;
  startTime?: string | null;
  endTime?: string | null;
  reasonId?: string | null;
  /** Filing for someone else (HR, or a manager for their team). */
  employeeId?: string | null;
}

// ---- Payroll ----
export interface PayrollRun {
  id: string;
  year: number;
  month: number;
  status: PayrollRunStatus;
  taxYearCode: string;
  processedAtUtc: string;
  finalizedAtUtc: string | null;
  employees: number;
  totalGross: number;
  totalDeductions: number;
  totalNet: number;
  submittedAtUtc: string | null;
  approvedByName: string | null;
  approvedAtUtc: string | null;
  approvalNote: string | null;
  disbursedAtUtc: string | null;
  disbursedVia: string | null;
  /** Salaries on hold this month (left out of the bank advice). */
  held: number;
}

export interface PayslipSummary {
  id: string;
  year: number;
  month: number;
  runStatus: PayrollRunStatus;
  employeeId: string;
  employeeCode: string;
  employeeName: string;
  department: string | null;
  paidDays: number;
  grossEarnings: number;
  incomeTax: number;
  eobiEmployee: number;
  otherDeductions: number;
  netPay: number;
  heldReason?: string | null;
}

export interface PayrollRunDetail {
  run: PayrollRun;
  payslips: PayslipSummary[];
  warnings: string[];
}

export interface PayslipLine {
  name: string;
  kind: SalaryComponentKind;
  amount: number;
}

export interface Payslip {
  id: string;
  companyName: string;
  year: number;
  month: number;
  runStatus: PayrollRunStatus;
  employeeCode: string;
  employeeName: string;
  department: string | null;
  designation: string | null;
  cnic: string | null;
  bankName: string | null;
  bankAccount: string | null;
  daysInMonth: number;
  unpaidDays: number;
  paidDays: number;
  grossEarnings: number;
  taxableIncome: number;
  incomeTax: number;
  eobiEmployee: number;
  eobiEmployer: number;
  otherDeductions: number;
  netPay: number;
  lines: PayslipLine[];
}

export interface PayrollSettings {
  eobiEmployeeContribution: number;
  eobiEmployerContribution: number;
  deductAbsentDays: boolean;
  /** Overtime pay per hour = basic / 30 / 8 x this. */
  overtimeMultiplier: number;
  /** Late arrivals per penalty; 0 = off. */
  latesPerPenalty: number;
  latePenaltyDays: number;
  deductHalfDays: boolean;
  requireApproval?: boolean;
  encashOnGross?: boolean;
}

// ---- Dashboard ----
export interface NameCount {
  name: string;
  count: number;
}
export interface TrendPoint {
  date: string;
  present: number;
  late: number;
}
export interface Dashboard {
  headcount: number;
  newJoinersThisMonth: number;
  presentToday: number;
  lateToday: number;
  onLeaveToday: number;
  pendingApprovals: number;
  byDepartment: NameCount[];
  attendanceTrend: TrendPoint[];
  lastPayroll: PayrollRun | null;
  upcomingHolidays: Holiday[];
}
