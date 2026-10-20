import type { Payslip, PayslipSummary, PayrollRun, PayrollRunDetail, PayrollSettings } from '../types';
import { http } from './client';

export const payrollApi = {
  runs: () => http.get<PayrollRun[]>('/payroll/runs'),
  process: (year: number, month: number) => http.post<PayrollRunDetail>('/payroll/runs', { year, month }),
  run: (id: string) => http.get<PayrollRunDetail>(`/payroll/runs/${id}`),
  finalize: (id: string) => http.post(`/payroll/runs/${id}/finalize`),
  myPayslips: () => http.get<PayslipSummary[]>('/payroll/my-payslips'),
  payslip: (id: string) => http.get<Payslip>(`/payroll/payslips/${id}`),
  settings: () => http.get<PayrollSettings>('/payroll/settings'),
  saveSettings: (b: PayrollSettings) => http.put('/payroll/settings', b),
};
