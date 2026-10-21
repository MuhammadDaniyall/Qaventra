import type { DayOfWeek } from '../types';
import { http } from './client';

export type ShiftAssignmentSource = 'Roster' | 'Request' | 'Swap';

export interface RosterShift {
  id: string;
  name: string;
  startTime: string;
  endTime: string;
}

export interface RosterCell {
  date: string;
  shiftId: string | null;
  isOff: boolean;
  isHoliday: boolean;
  /** Set on the roster rather than following the usual shift. */
  rostered: boolean;
  source: ShiftAssignmentSource | null;
}

export interface RosterRow {
  employeeId: string;
  employeeCode: string;
  name: string;
  department: string | null;
  defaultShiftId: string | null;
  days: RosterCell[];
}

export interface Roster {
  from: string;
  to: string;
  shifts: RosterShift[];
  rows: RosterRow[];
  /** "2026-9" style keys of closed months in range. */
  closedMonths: string[];
}

export interface AssignRoster {
  employeeIds: string[];
  from: string;
  to: string;
  weekdays: DayOfWeek[] | null;
  shiftId: string | null;
  off: boolean;
  clear: boolean;
}

export type ShiftRequestKind = 'Change' | 'Swap';
export type ShiftRequestStatus = 'AwaitingColleague' | 'Pending' | 'Approved' | 'Rejected' | 'Cancelled' | 'Declined';

export interface ShiftRequest {
  id: string;
  kind: ShiftRequestKind;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  fromDate: string;
  toDate: string;
  shiftName: string | null;
  swapWithEmployeeId: string | null;
  swapWithName: string | null;
  reason: string;
  status: ShiftRequestStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
}

export interface MonthlyRow {
  employeeId: string;
  employeeCode: string;
  name: string;
  department: string | null;
  workingDays: number;
  present: number;
  late: number;
  halfDay: number;
  leave: number;
  absent: number;
  offDays: number;
  workedMinutes: number;
  penaltyDays: number;
}

export interface MonthlyAttendance {
  year: number;
  month: number;
  closed: boolean;
  closedAtUtc: string | null;
  closedByName: string | null;
  canClose: boolean;
  countedTo: string;
  rows: MonthlyRow[];
}

export const schedulingApi = {
  roster: (from: string, to: string, departmentId?: string) => http.get<Roster>('/attendance/roster', { from, to, departmentId }),
  assign: (b: AssignRoster) => http.put<{ days: number }>('/attendance/roster', b),
  colleagues: () => http.get<{ id: string; name: string }[]>('/attendance/colleagues'),
  myShiftRequests: () => http.get<ShiftRequest[]>('/attendance/shift-requests/my'),
  shiftRequests: (status = 'Pending') => http.get<ShiftRequest[]>('/attendance/shift-requests', { status }),
  requestChange: (b: { from: string; to: string; shiftId: string; reason: string }) => http.post<ShiftRequest>('/attendance/shift-requests/change', b),
  requestSwap: (b: { colleagueId: string; from: string; to: string; reason: string }) => http.post<ShiftRequest>('/attendance/shift-requests/swap', b),
  accept: (id: string) => http.post(`/attendance/shift-requests/${id}/accept`),
  decline: (id: string) => http.post(`/attendance/shift-requests/${id}/decline`),
  approve: (id: string, note: string | null) => http.post(`/attendance/shift-requests/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/attendance/shift-requests/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/attendance/shift-requests/${id}/cancel`),
  monthly: (year: number, month: number, departmentId?: string) => http.get<MonthlyAttendance>('/attendance/monthly', { year, month, departmentId }),
  close: (year: number, month: number) => http.post(`/attendance/periods/${year}/${month}/close`),
  reopen: (year: number, month: number) => http.post(`/attendance/periods/${year}/${month}/reopen`),
};
