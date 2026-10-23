import type { Payslip, PayslipSummary, PayrollRun, PayrollRunDetail, PayrollSettings } from '../types';
import { http } from './client';

export const payrollApi = {
  runs: () => http.get<PayrollRun[]>('/payroll/runs'),
  process: (year: number, month: number) => http.post<PayrollRunDetail>('/payroll/runs', { year, month }),
  run: (id: string) => http.get<PayrollRunDetail>(`/payroll/runs/${id}`),
  finalize: (id: string) => http.post(`/payroll/runs/${id}/finalize`),
  submit: (id: string) => http.post(`/payroll/runs/${id}/submit`),
  decide: (id: string, approve: boolean, note: string | null) => http.post(`/payroll/runs/${id}/decide`, { approve, note }),
  disburse: (id: string, via: string | null) => http.post(`/payroll/runs/${id}/disburse`, { via }),
  undisburse: (id: string) => http.post(`/payroll/runs/${id}/undisburse`),
  myPayslips: () => http.get<PayslipSummary[]>('/payroll/my-payslips'),
  payslip: (id: string) => http.get<Payslip>(`/payroll/payslips/${id}`),
  settings: () => http.get<PayrollSettings>('/payroll/settings'),
  saveSettings: (b: PayrollSettings) => http.put('/payroll/settings', b),
};
