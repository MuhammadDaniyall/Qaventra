import type { RequestStatus } from './attendance';
import { http } from './client';

export type PayRequestKind = 'Allowance' | 'Bonus' | 'Overtime' | 'Deduction' | 'AdvanceSalary';

export interface PayRequest {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  kind: PayRequestKind;
  title: string;
  amount: number;
  isTaxable: boolean;
  isEarning: boolean;
  year: number;
  month: number;
  workDate: string | null;
  hours: number | null;
  reason: string | null;
  status: RequestStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  paidAtUtc: string | null;
  /** Included in a payroll run that is not finalized yet. */
  inDraftPayroll: boolean;
}

export interface Loan {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  title: string;
  amount: number;
  installments: number;
  installmentAmount: number;
  startYear: number;
  startMonth: number;
  reason: string | null;
  status: RequestStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  /** Repaid through finalized payrolls. */
  repaid: number;
  installmentsTaken: number;
  remaining: number;
}

export interface SubmitPayRequest {
  employeeId?: string | null;
  kind: PayRequestKind;
  title?: string | null;
  amount?: number | null;
  isTaxable?: boolean;
  year: number;
  month: number;
  workDate?: string | null;
  hours?: number | null;
  reason?: string | null;
}

export const PAY_KIND_LABELS: Record<PayRequestKind, string> = {
  Allowance: 'Allowance',
  Bonus: 'Bonus',
  Overtime: 'Overtime',
  Deduction: 'Deduction',
  AdvanceSalary: 'Advance salary',
};

export const payApi = {
  myRequests: () => http.get<PayRequest[]>('/pay/requests/my'),
  requests: (year?: number, month?: number) => http.get<PayRequest[]>('/pay/requests', { year, month }),
  submit: (b: SubmitPayRequest) => http.post<PayRequest>('/pay/requests', b),
  overtimeQuote: (date: string, hours: number, employeeId?: string) =>
    http.get<{ amount: number; multiplier: number }>('/pay/overtime-quote', { date, hours, employeeId }),
  approve: (id: string, note: string | null) => http.post(`/pay/requests/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/pay/requests/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/pay/requests/${id}/cancel`),
  myLoans: () => http.get<Loan[]>('/pay/loans/my'),
  loans: () => http.get<Loan[]>('/pay/loans'),
  requestLoan: (b: { employeeId?: string | null; title: string; amount: number; installments: number; startYear: number; startMonth: number; reason: string | null }) =>
    http.post<Loan>('/pay/loans', b),
  approveLoan: (id: string, note: string | null) => http.post(`/pay/loans/${id}/approve`, { note }),
  rejectLoan: (id: string, note: string | null) => http.post(`/pay/loans/${id}/reject`, { note }),
  cancelLoan: (id: string) => http.post(`/pay/loans/${id}/cancel`),
};
