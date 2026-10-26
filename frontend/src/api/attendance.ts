import type { Attendance, AttendanceBreak, BreakDay, BreakKind, DailyRow, ManualAttendanceRequest, PunchRequest, Today } from '../types';
import { http } from './client';

export const attendanceApi = {
  today: () => http.get<Today>('/attendance/today'),
  checkIn: (b: PunchRequest) => http.post<Attendance>('/attendance/check-in', b),
  checkOut: (b: PunchRequest) => http.post<Attendance>('/attendance/check-out', b),
  my: (year: number, month: number) => http.get<Attendance[]>('/attendance/my', { year, month }),
  daily: (date: string) => http.get<DailyRow[]>('/attendance/daily', { date }),
  manual: (b: ManualAttendanceRequest) => http.post<Attendance>('/attendance/manual', b),
  startBreak: (kind: BreakKind) => http.post<AttendanceBreak>('/attendance/break/start', { kind }),
  endBreak: () => http.post<AttendanceBreak>('/attendance/break/end'),
  breaks: (from: string, to: string, departmentId?: string) => http.get<BreakDay[]>('/attendance/breaks', { from, to, departmentId: departmentId || undefined }),
};

export type AttendanceRequestKind = 'MissedPunch' | 'WorkFromHome' | 'OfficialDuty' | 'TimeCorrection';
export type RequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export interface AttendanceRequest {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  date: string;
  kind: AttendanceRequestKind;
  checkIn: string;
  checkOut: string | null;
  reason: string;
  status: RequestStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  recordedCheckIn: string | null;
  recordedCheckOut: string | null;
}

export const attendanceRequestsApi = {
  my: () => http.get<AttendanceRequest[]>('/attendance/requests/my'),
  submit: (b: { date: string; kind: AttendanceRequestKind; checkIn: string; checkOut: string | null; reason: string; employeeId?: string | null }) =>
    http.post<AttendanceRequest>('/attendance/requests', b),
  approve: (id: string, note: string | null) => http.post(`/attendance/requests/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/attendance/requests/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/attendance/requests/${id}/cancel`),
};
