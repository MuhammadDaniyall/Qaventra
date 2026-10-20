import { http } from './client';

export type ApprovalRequestType = 'Leave' | 'Expense';
export type ApproverKind = 'LineManager' | 'SecondLevelManager' | 'Hr' | 'SpecificEmployee';
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
}

export const approvalsApi = {
  flows: () => http.get<ApprovalFlow[]>('/approvals/flows'),
  saveFlow: (type: ApprovalRequestType, steps: Array<{ kind: ApproverKind; employeeId: string | null }>) =>
    http.put(`/approvals/flows/${type}`, { steps }),
  inbox: () => http.get<InboxItem[]>('/approvals/inbox'),
  /** Undefined for requests submitted before approval chains existed. */
  timeline: (type: ApprovalRequestType, subjectId: string) => http.get<ApprovalTimeline | undefined>(`/approvals/${type}/${subjectId}`),
};

export const APPROVER_LABELS: Record<ApproverKind, string> = {
  LineManager: 'Line manager',
  SecondLevelManager: "Manager's manager",
  Hr: 'HR',
  SpecificEmployee: 'Specific person',
};

export const REQUEST_LABELS: Record<ApprovalRequestType, string> = {
  Leave: 'Leave requests',
  Expense: 'Expense claims',
};
