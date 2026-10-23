import { http } from './client';
import type { UserRole } from '../types';

export interface AccessModule {
  key: string;
  label: string;
}

export interface AccessTemplate {
  id: string;
  name: string;
  description: string | null;
  modules: string[];
  users: number;
}

export interface AccessUser {
  userId: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  accessTemplateId: string | null;
}

export type SuggestionStatus = 'New' | 'UnderReview' | 'Accepted' | 'Implemented' | 'Declined';

export interface SuggestionCategory {
  id: string;
  name: string;
  reviewerIds: string[];
  isActive: boolean;
}

export interface Suggestion {
  id: string;
  title: string;
  details: string;
  categoryId: string | null;
  category: string | null;
  isAnonymous: boolean;
  from: string | null;
  fromCode: string | null;
  status: SuggestionStatus;
  response: string | null;
  respondedByName: string | null;
  respondedAtUtc: string | null;
  createdAtUtc: string;
  mine: boolean;
}

export interface HistoryEntry {
  timestampUtc: string;
  userEmail: string | null;
  action: string;
  /** JSON: { Field: { old, new } } */
  changes: string | null;
}

export const accessApi = {
  modules: () => http.get<AccessModule[]>('/access/modules'),
  templates: () => http.get<AccessTemplate[]>('/access/templates'),
  addTemplate: (b: { name: string; description: string | null; modules: string[] }) => http.post('/access/templates', b),
  updateTemplate: (id: string, b: { name: string; description: string | null; modules: string[] }) => http.put(`/access/templates/${id}`, b),
  deleteTemplate: (id: string) => http.del(`/access/templates/${id}`),
  users: () => http.get<AccessUser[]>('/access/users'),
  setTemplate: (userId: string, accessTemplateId: string | null) => http.put(`/access/users/${userId}`, { accessTemplateId }),
};

export const suggestionsApi = {
  categories: () => http.get<SuggestionCategory[]>('/suggestions/categories'),
  addCategory: (b: Omit<SuggestionCategory, 'id'>) => http.post('/suggestions/categories', b),
  updateCategory: (id: string, b: Omit<SuggestionCategory, 'id'>) => http.put(`/suggestions/categories/${id}`, b),
  deleteCategory: (id: string) => http.del(`/suggestions/categories/${id}`),
  my: () => http.get<Suggestion[]>('/suggestions/my'),
  submit: (b: { title: string; details: string; categoryId: string | null; isAnonymous: boolean }) => http.post('/suggestions', b),
  review: () => http.get<Suggestion[]>('/suggestions/review'),
  respond: (id: string, status: SuggestionStatus, response: string | null) => http.put(`/suggestions/${id}/respond`, { status, response }),
  canReview: () => http.get<boolean>('/suggestions/can-review'),
};

export const historyApi = {
  get: (entityType: string, entityId: string) => http.get<HistoryEntry[]>(`/history/${entityType}/${entityId}`),
};
