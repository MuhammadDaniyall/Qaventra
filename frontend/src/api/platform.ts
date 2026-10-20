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
  /** Permanent. confirmName must equal the company name exactly. */
  deleteCompany: (id: string, confirmName: string) =>
    request<void>(`/platform/companies/${id}`, { method: 'DELETE', body: { confirmName } }),
  /** Returns a company-scoped session for the owner. */
  enterCompany: (id: string) => http.post<Session>(`/platform/companies/${id}/enter`),
};

export const auditApi = {
  list: (q: AuditLogQuery) => http.get<PagedResult<AuditLog>>('/audit-logs', { ...q }),
};
