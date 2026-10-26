import { http } from './client';

export type PayrollGlLine =
  | 'SalaryExpense'
  | 'EmployerContributionExpense'
  | 'IncomeTaxPayable'
  | 'EobiPayable'
  | 'ProvidentFundPayable'
  | 'SocialSecurityPayable'
  | 'OtherDeductionsPayable'
  | 'SalaryPayable';

export interface GlAccount {
  id: string;
  code: string;
  name: string;
}

export interface GlMapping {
  line: PayrollGlLine;
  label: string;
  glAccountId: string | null;
}

export interface JournalLine {
  accountCode: string;
  accountName: string;
  description: string;
  department: string | null;
  debit: number;
  credit: number;
}

export interface Journal {
  year: number;
  month: number;
  date: string;
  lines: JournalLine[];
  totalDebit: number;
  totalCredit: number;
  unmapped: string[];
}

export const accountingApi = {
  accounts: () => http.get<GlAccount[]>('/payroll/gl/accounts'),
  addAccount: (b: { code: string; name: string }) => http.post<GlAccount>('/payroll/gl/accounts', b),
  updateAccount: (id: string, b: { code: string; name: string }) => http.put<GlAccount>(`/payroll/gl/accounts/${id}`, b),
  removeAccount: (id: string) => http.del(`/payroll/gl/accounts/${id}`),
  mapping: () => http.get<GlMapping[]>('/payroll/gl/mapping'),
  saveMapping: (items: { line: PayrollGlLine; glAccountId: string | null }[]) => http.put<GlMapping[]>('/payroll/gl/mapping', { items }),
  journal: (runId: string) => http.get<Journal>(`/payroll/runs/${runId}/journal`),
};
