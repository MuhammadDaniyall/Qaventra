import { http } from './client';

export type PieceStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export interface PieceCategory {
  id: string;
  name: string;
  items: number;
}

export interface SavePieceItem {
  categoryId: string | null;
  name: string;
  unit: string;
  rate: number;
  dailyTarget: number | null;
  isActive: boolean;
}

export interface PieceItem extends SavePieceItem {
  id: string;
  category: string | null;
  specialRates: number;
}

export interface PieceRate {
  id: string;
  employeeId: string;
  employeeName: string;
  rate: number;
}

export interface PieceEntry {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  itemId: string;
  item: string;
  unit: string;
  date: string;
  quantity: number;
  rate: number;
  amount: number;
  efficiency: number | null;
  notes: string | null;
  enteredByName: string | null;
  status: PieceStatus;
  decidedByName: string | null;
  decisionNote: string | null;
  paid: boolean;
  canDecide: boolean;
  canDelete: boolean;
}

export interface PieceSummaryRow {
  name: string;
  quantity: number;
  amount: number;
  days: number;
  efficiency: number | null;
}

export interface PieceReport {
  amount: number;
  quantity: number;
  people: number;
  pending: number;
  pendingAmount: number;
  byItem: PieceSummaryRow[];
  byEmployee: PieceSummaryRow[];
}

export const pieceApi = {
  categories: () => http.get<PieceCategory[]>('/piece-work/categories'),
  addCategory: (name: string) => http.post('/piece-work/categories', { name }),
  deleteCategory: (id: string) => http.del(`/piece-work/categories/${id}`),
  items: () => http.get<PieceItem[]>('/piece-work/items'),
  addItem: (b: SavePieceItem) => http.post('/piece-work/items', b),
  updateItem: (id: string, b: SavePieceItem) => http.put(`/piece-work/items/${id}`, b),
  deleteItem: (id: string) => http.del(`/piece-work/items/${id}`),
  rates: (itemId: string) => http.get<PieceRate[]>(`/piece-work/items/${itemId}/rates`),
  setRate: (itemId: string, employeeId: string, rate: number) => http.put(`/piece-work/items/${itemId}/rates`, { employeeId, rate }),
  deleteRate: (id: string) => http.del(`/piece-work/rates/${id}`),

  entries: (q: { year: number; month: number; team?: boolean; status?: PieceStatus }) =>
    http.get<PieceEntry[]>('/piece-work/entries', { year: q.year, month: q.month, team: q.team || undefined, status: q.status }),
  save: (b: { itemId: string; date: string; lines: Array<{ employeeId: string; quantity: number; notes: string | null }> }) =>
    http.post<{ added: number; approved: boolean }>('/piece-work/entries', b),
  decide: (ids: string[], approve: boolean, note: string | null) => http.post<{ decided: number }>('/piece-work/entries/decide', { ids, approve, note }),
  remove: (id: string) => http.del(`/piece-work/entries/${id}`),
  team: () => http.get<Array<{ id: string; name: string }>>('/piece-work/team'),
  report: (year: number, month: number) => http.get<PieceReport>('/piece-work/report', { year, month }),
};
