import { http } from './client';
import type { ExpenseCategory } from '../types/workflows';

export type ExpenseCalculation = 'Amount' | 'PerUnit' | 'Distance';

export interface ExpenseGradeRate {
  gradeId: string;
  rate: number | null;
  yearlyLimit: number | null;
}

export interface ExpenseType {
  id: string;
  name: string;
  category: ExpenseCategory;
  calculation: ExpenseCalculation;
  unit: string | null;
  rate: number | null;
  perClaimLimit: number | null;
  yearlyLimit: number | null;
  receiptRequired: boolean;
  gradeIds: string[];
  isActive: boolean;
  gradeRates: ExpenseGradeRate[];
}
export type SaveExpenseType = Omit<ExpenseType, 'id'>;

export interface ExpenseTypeForMe {
  id: string;
  name: string;
  category: ExpenseCategory;
  calculation: ExpenseCalculation;
  unit: string | null;
  rate: number | null;
  perClaimLimit: number | null;
  yearlyLimit: number | null;
  usedThisYear: number;
  receiptRequired: boolean;
}

export interface DistanceRoute {
  id: string;
  fromCity: string;
  toCity: string;
  km: number;
}

export interface ExpenseAdvance {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  purpose: string;
  amount: number;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
  adjusted: number;
  outstanding: number;
  closedAtUtc: string | null;
  recovered: number;
  createdAtUtc: string;
  decisionNote: string | null;
}

export const expenseSetupApi = {
  types: () => http.get<ExpenseType[]>('/expense-setup/types'),
  typesFor: (employeeId?: string) => http.get<ExpenseTypeForMe[]>('/expense-setup/types/for', employeeId ? { employeeId } : undefined),
  addType: (b: SaveExpenseType) => http.post('/expense-setup/types', b),
  updateType: (id: string, b: SaveExpenseType) => http.put(`/expense-setup/types/${id}`, b),
  deleteType: (id: string) => http.del(`/expense-setup/types/${id}`),
  routes: () => http.get<DistanceRoute[]>('/expense-setup/routes'),
  addRoute: (b: { fromCity: string; toCity: string; km: number }) => http.post('/expense-setup/routes', b),
  deleteRoute: (id: string) => http.del(`/expense-setup/routes/${id}`),
  myAdvances: () => http.get<ExpenseAdvance[]>('/expense-setup/advances/my'),
  openAdvances: (employeeId?: string) => http.get<ExpenseAdvance[]>('/expense-setup/advances/open', employeeId ? { employeeId } : undefined),
  requestAdvance: (b: { purpose: string; amount: number; employeeId?: string | null }) => http.post('/expense-setup/advances', b),
  cancelAdvance: (id: string) => http.post(`/expense-setup/advances/${id}/cancel`),
  advances: (status?: string) => http.get<ExpenseAdvance[]>('/expense-setup/advances', status ? { status } : undefined),
  approveAdvance: (id: string, note: string | null) => http.post(`/expense-setup/advances/${id}/approve`, { note }),
  rejectAdvance: (id: string, note: string | null) => http.post(`/expense-setup/advances/${id}/reject`, { note }),
  closeAdvance: (id: string) => http.post(`/expense-setup/advances/${id}/close`),
};

/** Km between two cities from the list, either way round. */
export function routeKm(routes: DistanceRoute[] | undefined, from: string, to: string): number | null {
  const a = from.trim().toLowerCase();
  const b = to.trim().toLowerCase();
  const r = (routes ?? []).find(
    (x) => (x.fromCity.toLowerCase() === a && x.toCity.toLowerCase() === b) || (x.fromCity.toLowerCase() === b && x.toCity.toLowerCase() === a),
  );
  return r ? r.km : null;
}
