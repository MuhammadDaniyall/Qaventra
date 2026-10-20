import type { ApplyLeaveRequest, LeaveBalance, LeaveRequest, LeaveStatus } from '../types';
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
};
