import type {
  AuditLog,
  AuditLogQuery,
  Company,
  CompanyAdminCredentials,
  CreateCompanyRequest,
  PagedResult,
  Payment,
  PlatformStats,
  RecordPaymentRequest,
  RecordStamps,
  Session,
  UpdateCompanyRequest,
} from '../types';
import { http, request } from './client';

export const platformApi = {
  stats: () => http.get<PlatformStats>('/platform/stats'),
  companies: () => http.get<Company[]>('/platform/companies'),
  company: (id: string) => http.get<Company>(`/platform/companies/${id}`),
  createCompany: (b: CreateCompanyRequest) => http.post<CompanyAdminCredentials>('/platform/companies', b),
  updateCompany: (id: string, b: UpdateCompanyRequest) => http.put<Company>(`/platform/companies/${id}`, b),
  setCompanyStatus: (id: string, isActive: boolean) => http.put(`/platform/companies/${id}/status`, { isActive }),
  resetAdminPassword: (id: string, password: string | null) =>
    http.post<CompanyAdminCredentials>(`/platform/companies/${id}/reset-admin-password`, { password }),
  payments: (id: string) => http.get<Payment[]>(`/platform/companies/${id}/payments`),
  recordPayment: (id: string, b: RecordPaymentRequest) => http.post<Company>(`/platform/companies/${id}/payments`, b),
  /** Emails and notifies the company's admins about renewal now (once a day). */
  remind: (id: string) => http.post<Company>(`/platform/companies/${id}/remind`, {}),
  /** Permanent. confirmName must equal the company name exactly. */
  deleteCompany: (id: string, confirmName: string) =>
    request<void>(`/platform/companies/${id}`, { method: 'DELETE', body: { confirmName } }),
  /** Returns a company-scoped session for the owner. */
  enterCompany: (id: string) => http.post<Session>(`/platform/companies/${id}/enter`),
};

/** Rates are fractions (0.11 = 11%). toAmount is null for the top slab. */
export interface TaxSlab {
  fromAmount: number;
  toAmount: number | null;
  fixedTax: number;
  rate: number;
}

export interface TaxYear {
  id: string;
  code: string;
  startDate: string;
  endDate: string;
  surchargeThreshold: number;
  surchargeRate: number;
  slabs: TaxSlab[];
  /** Payroll runs (all companies) that used this year. */
  payrolls: number;
  stamps: RecordStamps;
}

export type SaveTaxYearRequest = Omit<TaxYear, 'id' | 'payrolls' | 'stamps'>;

/** Salaried income tax years shared by every company; platform owner only. */
export const taxYearsApi = {
  list: () => http.get<TaxYear[]>('/platform/tax-years'),
  create: (b: SaveTaxYearRequest) => http.post<TaxYear>('/platform/tax-years', b),
  update: (id: string, b: SaveTaxYearRequest) => http.put<TaxYear>(`/platform/tax-years/${id}`, b),
  remove: (id: string) => http.del(`/platform/tax-years/${id}`),
};

export const auditApi = {
  list: (q: AuditLogQuery) => http.get<PagedResult<AuditLog>>('/audit-logs', { ...q }),
};
