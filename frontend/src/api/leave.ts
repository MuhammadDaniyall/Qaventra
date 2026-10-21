import type { ApplyLeaveRequest, LeaveBalance, LeaveRequest, LeaveStatus } from '../types';
import type { RequestStatus } from './attendance';
import { http } from './client';

export const leaveApi = {
  balances: (year?: number) => http.get<LeaveBalance[]>('/leave/balances', { year }),
  countDays: (from: string, to: string) => http.get<{ days: number }>('/leave/count-days', { from, to }),
  my: () => http.get<LeaveRequest[]>('/leave/my'),
  apply: (b: ApplyLeaveRequest) => http.post<LeaveRequest>('/leave', b),
  approvals: (status: LeaveStatus) => http.get<LeaveRequest[]>('/leave/approvals', { status }),
  approve: (id: string, note: string | null) => http.post(`/leave/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/leave/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/leave/${id}/cancel`),
  employeeBalances: (employeeId: string, year: number) => http.get<LeaveBalance[]>(`/leave/balances/${employeeId}`, { year }),
  adjust: (employeeId: string, leaveTypeId: string, year: number, adjustment: number, note: string) =>
    http.put(`/leave/balances/${employeeId}/${leaveTypeId}?year=${year}`, { adjustment, note }),
};

export interface CompOff {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  workedOn: string;
  days: number;
  reason: string;
  status: RequestStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  workedMinutes: number | null;
}

export const compOffApi = {
  my: () => http.get<CompOff[]>('/leave/comp-off/my'),
  submit: (workedOn: string, days: number, reason: string) => http.post<CompOff>('/leave/comp-off', { workedOn, days, reason }),
  approve: (id: string, note: string | null) => http.post(`/leave/comp-off/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/leave/comp-off/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/leave/comp-off/${id}/cancel`),
};
