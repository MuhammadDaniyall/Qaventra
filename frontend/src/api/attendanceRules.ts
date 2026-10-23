import { http } from './client';
import type { DayOfWeek } from '../types';

export type PenaltyFlag = 'Late' | 'EarlyLeave' | 'HalfDay' | 'ShortDay' | 'Absent' | 'MissingCheckOut';
export type PenaltyAction = 'DeductPay' | 'DeductLeave';

export const PENALTY_FLAGS: { value: PenaltyFlag; label: string }[] = [
  { value: 'Late', label: 'Late arrivals' },
  { value: 'EarlyLeave', label: 'Early leaves' },
  { value: 'HalfDay', label: 'Half days' },
  { value: 'ShortDay', label: 'Short days' },
  { value: 'Absent', label: 'Absent days' },
  { value: 'MissingCheckOut', label: 'Missing check-outs' },
];

export interface PenaltyRule {
  id: string;
  name: string;
  flag: PenaltyFlag;
  everyCount: number;
  freeCount: number;
  minMinutes: number;
  repeat: boolean;
  action: PenaltyAction;
  days: number;
  leaveTypeId: string | null;
  isActive: boolean;
}
export type SavePenaltyRule = Omit<PenaltyRule, 'id'>;

export interface PenaltyLine {
  ruleId: string;
  ruleName: string;
  flag: PenaltyFlag;
  occurrences: number;
  action: PenaltyAction;
  days: number;
  leaveTypeId: string | null;
}

export interface PenaltyPreviewRow {
  employeeId: string;
  employeeCode: string;
  name: string;
  lines: PenaltyLine[];
  payDays: number;
  leaveDays: number;
}

export interface OffDayRequest {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  fromDate: string;
  toDate: string;
  offDays: DayOfWeek[];
  usualOffDays: DayOfWeek[];
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
  createdAtUtc: string;
  decisionNote: string | null;
}

export const attendanceRulesApi = {
  rules: () => http.get<PenaltyRule[]>('/attendance-rules/penalties'),
  addRule: (b: SavePenaltyRule) => http.post('/attendance-rules/penalties', b),
  updateRule: (id: string, b: SavePenaltyRule) => http.put(`/attendance-rules/penalties/${id}`, b),
  deleteRule: (id: string) => http.del(`/attendance-rules/penalties/${id}`),
  preview: (year: number, month: number) => http.get<PenaltyPreviewRow[]>('/attendance-rules/penalties/preview', { year, month }),
  myOffDays: () => http.get<OffDayRequest[]>('/attendance-rules/off-days/my'),
  requestOffDays: (b: { fromDate: string; toDate: string; offDays: DayOfWeek[]; reason: string }) => http.post('/attendance-rules/off-days', b),
  cancelOffDays: (id: string) => http.post(`/attendance-rules/off-days/${id}/cancel`),
  approveOffDays: (id: string, note: string | null) => http.post(`/attendance-rules/off-days/${id}/approve`, { note }),
  rejectOffDays: (id: string, note: string | null) => http.post(`/attendance-rules/off-days/${id}/reject`, { note }),
};
