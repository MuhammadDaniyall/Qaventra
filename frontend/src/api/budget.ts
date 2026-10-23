import { http } from './client';

export type BudgetStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Closed';

export interface BudgetPlanListItem {
  id: string;
  name: string;
  fromDate: string;
  toDate: string;
  status: BudgetStatus;
  headcount: number;
  cost: number;
  positions: number;
}

export interface BudgetLine {
  id: string;
  departmentId: string | null;
  department: string;
  designationId: string | null;
  designation: string;
  headcount: number;
  monthlyCost: number;
  filledFrom: string | null;
  notes: string | null;
  onRoll: number;
  hiring: number;
  vacant: number;
  budgetCost: number;
  actualCost: number;
  variance: number;
}

export interface BudgetPlan {
  id: string;
  name: string;
  fromDate: string;
  toDate: string;
  status: BudgetStatus;
  notes: string | null;
  submittedAtUtc: string | null;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  lines: BudgetLine[];
  unplanned: Array<{ departmentId: string | null; department: string; designationId: string | null; designation: string; onRoll: number; monthlyCost: number }>;
  headcount: number;
  onRoll: number;
  vacant: number;
  hiring: number;
  budgetCost: number;
  actualCost: number;
  canEdit: boolean;
  canDecide: boolean;
}

export interface PositionInput {
  departmentId: string | null;
  designationId: string | null;
  headcount: number;
  monthlyCost: number;
  filledFrom: string | null;
  notes: string | null;
}

export interface BudgetCheck {
  hasPlan: boolean;
  plan: string | null;
  inBudget: boolean;
  approved: number;
  onRoll: number;
  hiring: number;
  open: number;
}

export const budgetApi = {
  plans: () => http.get<BudgetPlanListItem[]>('/budget/plans'),
  plan: (id: string) => http.get<BudgetPlan>(`/budget/plans/${id}`),
  create: (b: { name: string; fromDate: string; toDate: string; notes: string | null }, fromStaff: boolean) =>
    http.post<BudgetPlanListItem>(`/budget/plans${fromStaff ? '?fromStaff=true' : ''}`, b),
  update: (id: string, b: { name: string; fromDate: string; toDate: string; notes: string | null }) => http.put(`/budget/plans/${id}`, b),
  savePositions: (id: string, positions: PositionInput[]) => http.put(`/budget/plans/${id}/positions`, { positions }),
  remove: (id: string) => http.del(`/budget/plans/${id}`),
  submit: (id: string) => http.post(`/budget/plans/${id}/submit`),
  decide: (id: string, approve: boolean, note: string | null) => http.post(`/budget/plans/${id}/decide`, { approve, note }),
  close: (id: string) => http.post(`/budget/plans/${id}/close`),
  check: (departmentId: string | null, designationId: string | null) =>
    http.get<BudgetCheck>('/budget/check', { departmentId: departmentId || undefined, designationId: designationId || undefined }),
};
