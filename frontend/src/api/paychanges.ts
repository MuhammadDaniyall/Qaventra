import { http } from './client';

export type RevisionKind = 'Increment' | 'Promotion' | 'Decrement' | 'Correction';

export interface RevisionLine {
  employeeId: string;
  percent: number | null;
  amount: number | null;
}

export interface ReviseRequest {
  effectiveFrom: string;
  kind: RevisionKind;
  includeAllowances: boolean;
  lines: RevisionLine[];
  reason: string | null;
}

export interface RevisionPreview {
  employeeId: string;
  name: string;
  code: string;
  oldBasic: number;
  newBasic: number;
  oldGross: number;
  newGross: number;
  changePercent: number;
  problem: string | null;
}

export interface SalaryRevision {
  id: string;
  employeeId: string;
  name: string;
  code: string;
  effectiveFrom: string;
  kind: RevisionKind;
  oldBasic: number;
  newBasic: number;
  oldGross: number;
  newGross: number;
  reason: string | null;
  byName: string | null;
  createdAtUtc: string;
}

export interface SaveRecurring {
  employeeId: string;
  title: string;
  isEarning: boolean;
  amount: number;
  isTaxable: boolean;
  startYear: number;
  startMonth: number;
  endYear: number | null;
  endMonth: number | null;
  notes: string | null;
}

export interface RecurringItem extends SaveRecurring {
  id: string;
  name: string;
  code: string;
  active: boolean;
}

export interface SalaryHold {
  id: string;
  employeeId: string;
  name: string;
  code: string;
  year: number;
  month: number;
  reason: string;
  heldByName: string | null;
  createdAtUtc: string;
  releasedAtUtc: string | null;
  releasedByName: string | null;
}

export const payChangesApi = {
  preview: (b: ReviseRequest) => http.post<RevisionPreview[]>('/pay-changes/revisions/preview', b),
  revise: (b: ReviseRequest) => http.post<{ revised: number }>('/pay-changes/revisions', b),
  revisions: () => http.get<SalaryRevision[]>('/pay-changes/revisions'),
  recurring: () => http.get<RecurringItem[]>('/pay-changes/recurring'),
  addRecurring: (b: SaveRecurring) => http.post('/pay-changes/recurring', b),
  updateRecurring: (id: string, b: SaveRecurring) => http.put(`/pay-changes/recurring/${id}`, b),
  deleteRecurring: (id: string) => http.del(`/pay-changes/recurring/${id}`),
  holds: () => http.get<SalaryHold[]>('/pay-changes/holds'),
  hold: (b: { employeeId: string; year: number; month: number; reason: string }) => http.post('/pay-changes/holds', b),
  release: (id: string) => http.post(`/pay-changes/holds/${id}/release`),
  quote: (employeeId: string, leaveTypeId: string, days: number) =>
    http.get<{ remaining: number; dayRate: number; amount: number; basis: string }>('/pay-changes/encash/quote', { employeeId, leaveTypeId, days }),
  encash: (b: { employeeId: string; leaveTypeId: string; days: number; note: string | null }) => http.post<{ amount: number }>('/pay-changes/encash', b),
};
