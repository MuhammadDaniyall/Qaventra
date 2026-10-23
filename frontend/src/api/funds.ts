import { http } from './client';

export type FundKind = 'ProvidentFund' | 'Gratuity';
export type FundEntryKind = 'Opening' | 'Contribution' | 'Profit' | 'Withdrawal' | 'Adjustment';
export type TaxAdjustmentKind = 'PreviousIncome' | 'PreviousTax' | 'TaxCredit' | 'OtherIncome';

export const TAX_ADJUSTMENT_LABELS: Record<TaxAdjustmentKind, string> = {
  PreviousIncome: 'Salary from previous employer',
  PreviousTax: 'Tax paid at previous employer',
  TaxCredit: 'Tax credit',
  OtherIncome: 'Other taxable income',
};

export interface FundSummary {
  employeeId: string;
  name: string;
  code: string;
  pfMember: boolean;
  pfEmployeeShare: number;
  pfEmployerShare: number;
  pfProfit: number;
  pfWithdrawn: number;
  pfBalance: number;
  pfVested: number;
  serviceYears: number;
  gratuityAccrued: number;
  gratuityWithdrawn: number;
  gratuityBalance: number;
}

export interface FundEntry {
  id: string;
  fund: FundKind;
  kind: FundEntryKind;
  date: string;
  employeeAmount: number;
  employerAmount: number;
  note: string | null;
}

export interface FundWithdrawal {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  fund: FundKind;
  amount: number;
  reason: string;
  payWithSalary: boolean;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
  createdAtUtc: string;
  decisionNote: string | null;
}

export interface TaxAdjustment {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  taxYearCode: string;
  kind: TaxAdjustmentKind;
  amount: number;
  note: string | null;
}

export const fundsApi = {
  mine: () => http.get<FundSummary>('/funds/me'),
  summary: (includeLeft = false) => http.get<FundSummary[]>('/funds/summary', { includeLeft }),
  ledger: (employeeId: string) => http.get<FundEntry[]>(`/funds/ledger/${employeeId}`),
  addEntry: (b: {
    employeeId: string;
    fund: FundKind;
    kind: FundEntryKind;
    date: string;
    employeeAmount: number;
    employerAmount: number;
    note: string | null;
  }) => http.post('/funds/entries', b),
  deleteEntry: (id: string) => http.del(`/funds/entries/${id}`),
  profit: (b: { date: string; ratePercent: number; note: string | null }) => http.post<{ members: number; total: number }>('/funds/profit', b),
  myWithdrawals: () => http.get<FundWithdrawal[]>('/funds/withdrawals/my'),
  withdraw: (b: { fund: FundKind; amount: number; reason: string; payWithSalary: boolean; employeeId?: string | null }) => http.post('/funds/withdrawals', b),
  cancelWithdrawal: (id: string) => http.post(`/funds/withdrawals/${id}/cancel`),
  withdrawals: (status?: string) => http.get<FundWithdrawal[]>('/funds/withdrawals', status ? { status } : undefined),
  approve: (id: string, note: string | null) => http.post(`/funds/withdrawals/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/funds/withdrawals/${id}/reject`, { note }),
  taxYears: () => http.get<string[]>('/funds/tax-years'),
  taxAdjustments: (taxYear?: string) => http.get<TaxAdjustment[]>('/funds/tax-adjustments', taxYear ? { taxYear } : undefined),
  addTaxAdjustment: (b: { employeeId: string; taxYearCode: string; kind: TaxAdjustmentKind; amount: number; note: string | null }) =>
    http.post('/funds/tax-adjustments', b),
  deleteTaxAdjustment: (id: string) => http.del(`/funds/tax-adjustments/${id}`),
  splits: (employeeId: string) => http.get<{ costCenterId: string; percent: number }[]>(`/funds/splits/${employeeId}`),
  saveSplits: (employeeId: string, splits: { costCenterId: string; percent: number }[]) => http.put(`/funds/splits/${employeeId}`, { splits }),
};
