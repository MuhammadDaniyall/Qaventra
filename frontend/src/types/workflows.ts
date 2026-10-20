// Letters, expense claims and separations.

// ---- Letters ----
export type LetterCategory =
  | 'Offer'
  | 'Appointment'
  | 'Confirmation'
  | 'Increment'
  | 'Experience'
  | 'Relieving'
  | 'SalaryCertificate'
  | 'Warning'
  | 'Other';

export interface LetterPlaceholder {
  key: string;
  label: string;
  example: string;
}

export interface LetterTemplate {
  id: string;
  name: string;
  category: LetterCategory;
  subject: string;
  body: string;
  isActive: boolean;
}

export type SaveLetterTemplateRequest = Omit<LetterTemplate, 'id'>;

export interface LetterPreview {
  subject: string;
  body: string;
  unknownPlaceholders: string[];
}

export interface IssueLetterRequest {
  employeeId: string;
  category: LetterCategory;
  subject: string;
  body: string;
  issuedOn: string | null;
  shareWithEmployee: boolean;
}

export interface IssuedLetter {
  id: string;
  referenceNo: string;
  category: LetterCategory;
  subject: string;
  issuedOn: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  issuedByName: string | null;
  sharedWithEmployee: boolean;
}

export interface LetterDocument extends IssuedLetter {
  body: string;
  designation: string | null;
  companyName: string;
  companyAddress: string | null;
  companyPhone: string | null;
}

// ---- Expenses ----
export type ExpenseCategory =
  | 'Travel'
  | 'Fuel'
  | 'Meals'
  | 'Accommodation'
  | 'Medical'
  | 'Mobile'
  | 'OfficeSupplies'
  | 'Training'
  | 'Other';
export type ExpenseStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled' | 'Paid';

export interface ExpenseClaim {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  title: string;
  status: ExpenseStatus;
  total: number;
  itemCount: number;
  receiptCount: number;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  paidAtUtc: string | null;
  paidVia: string | null;
}

export interface ExpenseItem {
  id: string;
  date: string;
  category: ExpenseCategory;
  description: string;
  amount: number;
  receiptId: string | null;
  receiptFileName: string | null;
}

export interface ExpenseClaimDetail {
  claim: ExpenseClaim;
  items: ExpenseItem[];
  canDecide: boolean;
}

export interface ReceiptUpload {
  fileName: string;
  contentType: string;
  base64: string;
}

export interface SubmitExpenseRequest {
  title: string;
  items: Array<{ date: string; category: ExpenseCategory; description: string; amount: number; receipt: ReceiptUpload | null }>;
}

// ---- Separation ----
export type SeparationType = 'Resignation' | 'Termination' | 'Retirement' | 'ContractEnd';
export type SeparationStatus = 'Requested' | 'InProgress' | 'Completed' | 'Rejected' | 'Withdrawn';

export interface SeparationSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  department: string | null;
  designation: string | null;
  type: SeparationType;
  status: SeparationStatus;
  noticeDate: string;
  proposedLastDay: string;
  lastWorkingDay: string | null;
  clearanceDone: number;
  clearanceTotal: number;
  createdAtUtc: string;
}

export interface ClearanceItem {
  id: string;
  department: string;
  title: string;
  isCleared: boolean;
  clearedByName: string | null;
  clearedAtUtc: string | null;
  note: string | null;
}

export interface Settlement {
  hasSalary: boolean;
  monthlyGross: number;
  dailyRate: number;
  leaveBalances: Array<{ leaveType: string; remaining: number }>;
  claims: Array<{ id: string; title: string; total: number }>;
  encashedLeaveDays: number;
  leaveEncashment: number;
  noticeServedDays: number;
  noticeShortfallDays: number;
  recoverNoticeShortfall: boolean;
  noticeRecovery: number;
  expenseReimbursement: number;
  otherAdditions: number;
  otherDeductions: number;
  note: string | null;
  net: number;
  savedAtUtc: string | null;
}

export interface Separation {
  summary: SeparationSummary;
  joiningDate: string;
  noticePeriodDays: number;
  reason: string | null;
  decisionNote: string | null;
  decidedAtUtc: string | null;
  exitReason: string | null;
  exitRating: number | null;
  wouldRecommend: boolean | null;
  exitFeedback: string | null;
  exitInterviewAtUtc: string | null;
  clearance: ClearanceItem[];
  settlement: Settlement | null;
  completedAtUtc: string | null;
}

export interface InitiateSeparationRequest {
  employeeId: string;
  type: SeparationType;
  noticeDate: string;
  lastWorkingDay: string;
  noticePeriodDays: number;
  reason: string | null;
}

export interface SaveSettlementRequest {
  encashedLeaveDays: number;
  recoverNoticeShortfall: boolean;
  otherAdditions: number;
  otherDeductions: number;
  note: string | null;
}
