import type { BillingCycle, RecordStamps } from '../types';
import { http } from './client';

export const billingKeys = {
  billing: ['billing'] as const,
  plans: ['platform', 'plans'] as const,
  modules: ['platform', 'modules'] as const,
  companyPlan: (id: string) => ['platform', 'companies', id, 'plan'] as const,
  invoices: (status: string) => ['platform', 'invoices', status] as const,
};

export type InvoiceStatus = 'Unpaid' | 'AwaitingApproval' | 'Paid' | 'Cancelled';
export const INVOICE_LABELS: Record<InvoiceStatus, string> = {
  Unpaid: 'Unpaid',
  AwaitingApproval: 'Being checked',
  Paid: 'Paid',
  Cancelled: 'Cancelled',
};
export const INVOICE_TONES: Record<InvoiceStatus, 'amber' | 'blue' | 'green' | 'gray'> = {
  Unpaid: 'amber',
  AwaitingApproval: 'blue',
  Paid: 'green',
  Cancelled: 'gray',
};

export interface ModuleInfo {
  key: string;
  label: string;
  core: boolean;
}

export interface Invoice {
  id: string;
  tenantId: string;
  company: string | null;
  number: string;
  amount: number;
  periods: number;
  issuedOn: string;
  description: string | null;
  status: InvoiceStatus;
  provider: string | null;
  providerReference: string | null;
  hasProof: boolean;
  reviewNote: string | null;
  paidAtUtc: string | null;
  stamps: RecordStamps;
}

export interface Billing {
  company: string;
  planId: string | null;
  planName: string | null;
  billingCycle: BillingCycle | null;
  amountPerCycle: number | null;
  employees: number;
  maxEmployees: number | null;
  subscriptionEndsOn: string | null;
  trialEndsOn: string | null;
  daysLeft: number | null;
  access: 'Allowed' | 'Inactive' | 'Expired';
  enabledModules: string[] | null;
  modules: ModuleInfo[];
  invoices: Invoice[];
  providers: { key: string; label: string; online: boolean }[];
}

export interface PaymentStart {
  redirectUrl: string | null;
  instructions: string | null;
  reference: string | null;
}

export interface Plan {
  id: string;
  name: string;
  description: string | null;
  pricePerEmployee: number;
  flatPrice: number;
  maxEmployees: number | null;
  modules: string[];
  trialDays: number;
  isActive: boolean;
  sortOrder: number;
  companies: number;
  stamps: RecordStamps;
}

export type SavePlan = Omit<Plan, 'id' | 'companies' | 'stamps'>;

export interface CompanyPlan {
  planId: string | null;
  planName: string | null;
  trialEndsOn: string | null;
  enabledModules: string[] | null;
  modules: ModuleInfo[];
}

export const billingApi = {
  get: () => http.get<Billing>('/billing'),
  createInvoice: (periods: number) => http.post<Invoice>('/billing/invoices', { periods }),
  pay: (id: string, provider: string) =>
    http.post<PaymentStart>(`/billing/invoices/${id}/pay`, { provider, returnUrl: `${window.location.origin}/billing` }),
  uploadProof: (id: string, file: File, reference: string) => {
    const form = new FormData();
    form.append('file', file);
    form.append('reference', reference);
    return http.upload<Invoice>(`/billing/invoices/${id}/proof`, form);
  },
  cancel: (id: string) => http.del(`/billing/invoices/${id}`),
};

export const platformBillingApi = {
  modules: () => http.get<ModuleInfo[]>('/platform/modules'),
  plans: () => http.get<Plan[]>('/platform/plans'),
  createPlan: (body: SavePlan) => http.post<Plan>('/platform/plans', body),
  updatePlan: (id: string, body: SavePlan) => http.put<Plan>(`/platform/plans/${id}`, body),
  deletePlan: (id: string) => http.del(`/platform/plans/${id}`),
  companyPlan: (id: string) => http.get<CompanyPlan>(`/platform/companies/${id}/plan`),
  setCompanyPlan: (id: string, body: { planId: string | null; trialEndsOn: string | null; modules: string[] | null }) =>
    http.put<CompanyPlan>(`/platform/companies/${id}/plan`, body),
  invoices: (status: InvoiceStatus | 'all') =>
    http.get<Invoice[]>('/platform/invoices', status === 'all' ? { all: true } : { status }),
  proofPath: (id: string) => `/platform/invoices/${id}/proof`,
  approve: (id: string, note: string | null) => http.post(`/platform/invoices/${id}/approve`, { note }),
  reject: (id: string, note: string) => http.post(`/platform/invoices/${id}/reject`, { note }),
};
