import { http } from './client';

export type ApprovalRequestType =
  'Leave' | 'Expense' | 'Attendance' | 'CompOff' | 'PayRequest' | 'Loan' | 'ProfileChange' | 'ShiftChange' | 'Asset' | 'JobRequest' | 'Travel' | 'Training' | 'Exemption' | 'OffDay' | 'Timesheet';
export type ApproverKind = 'LineManager' | 'SecondLevelManager' | 'Hr' | 'SpecificEmployee' | 'DepartmentHead';
export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export type ApprovalStepStatus = 'Waiting' | 'Current' | 'Approved' | 'Rejected' | 'Skipped';

export interface FlowStep {
  kind: ApproverKind;
  employeeId: string | null;
  employeeName?: string | null;
}

export interface ApprovalFlow {
  requestType: ApprovalRequestType;
  isDefault: boolean;
  steps: FlowStep[];
}

export interface InboxItem {
  requestType: ApprovalRequestType;
  subjectId: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  summary: string;
  step: number;
  totalSteps: number;
  /** False when HR sees an item that waits for someone else (HR can still step in). */
  assignedToMe: boolean;
  createdAtUtc: string;
  /** Set when the item is here because its approver delegated to the signed-in user. */
  actingFor: string | null;
}

export interface Delegation {
  id: string;
  fromEmployeeId: string;
  from: string;
  toEmployeeId: string;
  to: string;
  fromDate: string;
  toDate: string;
  requestTypes: ApprovalRequestType[];
  reason: string | null;
  isCancelled: boolean;
  isActive: boolean;
  createdByName: string | null;
  canCancel: boolean;
}

export interface ApprovalStep {
  order: number;
  kind: ApproverKind;
  approverName: string | null;
  status: ApprovalStepStatus;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  note: string | null;
}

export interface ApprovalTimeline {
  status: ApprovalStatus;
  steps: ApprovalStep[];
  /** HR approved it over the remaining steps. */
  forced: boolean;
}

export const approvalsApi = {
  flows: () => http.get<ApprovalFlow[]>('/approvals/flows'),
  saveFlow: (type: ApprovalRequestType, steps: Array<{ kind: ApproverKind; employeeId: string | null }>) => http.put(`/approvals/flows/${type}`, { steps }),
  inbox: () => http.get<InboxItem[]>('/approvals/inbox'),
  /** Undefined for requests submitted before approval chains existed. */
  timeline: (type: ApprovalRequestType, subjectId: string) => http.get<ApprovalTimeline | undefined>(`/approvals/${type}/${subjectId}`),
  /** Approve or reject any kind of request; HR may force-approve over the remaining steps (a note is required). */
  decide: (type: ApprovalRequestType, subjectId: string, b: { approve: boolean; note: string | null; force?: boolean }) =>
    http.post(`/approvals/${type}/${subjectId}/decide`, b),
  delegations: () => http.get<Delegation[]>('/approvals/delegations'),
  delegate: (b: {
    fromEmployeeId: string | null;
    toEmployeeId: string;
    fromDate: string;
    toDate: string;
    requestTypes: ApprovalRequestType[];
    reason: string | null;
  }) => http.post('/approvals/delegations', b),
  cancelDelegation: (id: string) => http.post(`/approvals/delegations/${id}/cancel`),
};

export const APPROVER_LABELS: Record<ApproverKind, string> = {
  LineManager: 'Line manager',
  SecondLevelManager: "Manager's manager",
  Hr: 'HR',
  SpecificEmployee: 'Specific person',
  DepartmentHead: 'Department head',
};

export const REQUEST_LABELS: Record<ApprovalRequestType, string> = {
  Leave: 'Leave requests',
  Expense: 'Expense claims',
  Attendance: 'Attendance requests',
  CompOff: 'Comp-off claims',
  PayRequest: 'Pay requests',
  Loan: 'Loans',
  ProfileChange: 'Profile changes',
  ShiftChange: 'Shift requests',
  Asset: 'Asset requests',
  JobRequest: 'Hiring requests',
  Travel: 'Travel requests',
  Training: 'Training requests',
  Exemption: 'Excuses',
  OffDay: 'Days off',
  Timesheet: 'Timesheets',
};
