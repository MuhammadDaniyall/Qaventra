import type { ExpenseClaim, ExpenseClaimDetail, ExpenseStatus, SubmitExpenseRequest } from '../types/workflows';
import { getToken, http } from './client';

export const expensesApi = {
  my: () => http.get<ExpenseClaim[]>('/expenses/my'),
  submit: (b: SubmitExpenseRequest) => http.post<ExpenseClaim>('/expenses', b),
  approvals: (status: ExpenseStatus) => http.get<ExpenseClaim[]>('/expenses/approvals', { status }),
  get: (id: string) => http.get<ExpenseClaimDetail>(`/expenses/${id}`),
  approve: (id: string, note: string | null) => http.post(`/expenses/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/expenses/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/expenses/${id}/cancel`),
  /** Receipts need the bearer token, so they are fetched as blobs and shown through object URLs. */
  receipt: async (claimId: string, receiptId: string): Promise<Blob> => {
    const res = await fetch(`/api/expenses/${claimId}/receipts/${receiptId}`, {
      headers: { Authorization: `Bearer ${getToken() ?? ''}` },
    });
    if (!res.ok) throw new Error('Could not load the receipt.');
    return res.blob();
  },
};
