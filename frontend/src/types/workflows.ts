// Letters, expense claims and separations.

import type { RecordStamps } from './index';

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
  requiresApproval?: boolean;
  selfService?: boolean;
  autoUse?: LetterAutoUse | null;
  marginTopMm?: number;
  marginSideMm?: number;
  footer?: string | null;
  stamps: RecordStamps;
}

export type LetterAutoUse = 'Offer' | 'Hire' | 'JobChange' | 'SalaryRevision' | 'Exit' | 'Confirmation';

export type SaveLetterTemplateRequest = Omit<LetterTemplate, 'id' | 'stamps'>;

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
  templateId?: string | null;
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
  status?: 'Issued' | 'Pending' | 'Rejected';
  requested?: boolean;
  requestReason?: string | null;
  createdAtUtc: string;
  stamps: RecordStamps;
}

export interface LetterDocument extends IssuedLetter {
  body: string;
  designation: string | null;
  companyName: string;
  companyAddress: string | null;
  companyPhone: string | null;
  marginTopMm?: number;
  marginSideMm?: number;
  footer?: string | null;
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
  /** Part of the total covered by an advance; payroll pays only the rest. */
  advanceAdjusted?: number;
  stamps: RecordStamps;
}

export interface ExpenseItem {
  id: string;
  date: string;
  category: ExpenseCategory;
  description: string;
  amount: number;
  receiptId: string | null;
  receiptFileName: string | null;
  /** What the approver allowed when it differs from the amount claimed. */
  approvedAmount: number | null;
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
  items: Array<{
    date: string;
    category: ExpenseCategory;
    description: string;
    amount: number;
    receipt: ReceiptUpload | null;
    expenseTypeId?: string | null;
    quantity?: number | null;
    fromCity?: string | null;
    toCity?: string | null;
  }>;
  employeeId?: string | null;
  advanceId?: string | null;
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
  stamps: RecordStamps;
}

export interface ClearanceItem {
  id: string;
  department: string;
  title: string;
  isCleared: boolean;
  clearedByName: string | null;
  clearedAtUtc: string | null;
  note: string | null;
  stamps: RecordStamps;
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
  /** UAE / Saudi end-of-service gratuity and how it was worked out (0 under Pakistan payroll). */
  gratuity?: number;
  gratuityNote?: string | null;
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
