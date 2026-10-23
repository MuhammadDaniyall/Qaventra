import { http } from './client';

export interface LeaveReason {
  id: string;
  leaveTypeId: string;
  name: string;
  maxDays: number | null;
  requiresDocument: boolean;
  isActive: boolean;
}

export interface OpeningBalance {
  employeeId: string;
  name: string;
  code: string;
  leaveTypeId: string;
  leaveType: string;
  entitled: number;
  carriedForward: number;
  adjustment: number;
  used: number;
  remaining: number;
}

export type ExemptionFlag = 'Late' | 'HalfDay' | 'ShortDay' | 'Present';

export interface Exemption {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  date: string;
  flag: ExemptionFlag;
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
  createdAtUtc: string;
  decisionNote: string | null;
  checkIn: string | null;
}

export const leaveRulesApi = {
  reasons: () => http.get<LeaveReason[]>('/leave-rules/reasons'),
  addReason: (b: Omit<LeaveReason, 'id'>) => http.post('/leave-rules/reasons', b),
  updateReason: (id: string, b: Omit<LeaveReason, 'id'>) => http.put(`/leave-rules/reasons/${id}`, b),
  deleteReason: (id: string) => http.del(`/leave-rules/reasons/${id}`),
  balances: (year: number) => http.get<OpeningBalance[]>('/leave-rules/balances', { year }),
  saveBalances: (year: number, lines: Array<{ employeeId: string; leaveTypeId: string; carriedForward: number; adjustment: number }>, note: string | null) =>
    http.put<{ changed: number }>('/leave-rules/balances', { year, lines, note }),
};

export const exemptionsApi = {
  my: () => http.get<Exemption[]>('/exemptions/my'),
  quota: () => http.get<{ allowed: number; used: number; withinDays: number }>('/exemptions/quota'),
  request: (date: string, reason: string) => http.post('/exemptions', { date, reason }),
  cancel: (id: string) => http.post(`/exemptions/${id}/cancel`),
  approve: (id: string, note: string | null) => http.post(`/exemptions/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/exemptions/${id}/reject`, { note }),
  settings: () => http.get<{ exemptionsPerMonth: number; exemptionWithinDays: number }>('/exemptions/settings'),
  saveSettings: (b: { exemptionsPerMonth: number; exemptionWithinDays: number }) => http.put('/exemptions/settings', b),
};
