import type { Lookup } from '../types';
import { http } from './client';

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type TicketStatus = 'Open' | 'InProgress' | 'WaitingOnEmployee' | 'Resolved' | 'Closed' | 'Cancelled';

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  WaitingOnEmployee: 'Waiting on employee',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
};

export const PRIORITIES: TicketPriority[] = ['Low', 'Medium', 'High', 'Urgent'];

export interface TicketCategory {
  id: string;
  name: string;
  description: string | null;
  targetHours: number;
  agentIds: string[];
  agents: string[];
  isActive: boolean;
  openTickets: number;
}

export interface SaveTicketCategory {
  name: string;
  description: string | null;
  targetHours: number;
  agentIds: string[];
  isActive: boolean;
}

export interface TicketListItem {
  id: string;
  code: string;
  subject: string;
  categoryId: string;
  category: string;
  priority: TicketPriority;
  status: TicketStatus;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  department: string | null;
  assigneeEmployeeId: string | null;
  assignee: string | null;
  createdAtUtc: string;
  dueAtUtc: string;
  lastActivityAtUtc: string;
  isOverdue: boolean;
  replies: number;
  rating: number | null;
}

export interface TicketComment {
  id: string;
  authorEmployeeId: string | null;
  authorName: string;
  body: string;
  isInternal: boolean;
  isSystem: boolean;
  createdAtUtc: string;
}

export interface TicketDetail {
  ticket: TicketListItem;
  description: string;
  raisedBy: string;
  involvedIds: string[];
  involved: string[];
  firstResponseAtUtc: string | null;
  resolvedAtUtc: string | null;
  closedAtUtc: string | null;
  feedback: string | null;
  isAgent: boolean;
  isRequester: boolean;
  comments: TicketComment[];
  /** Agents only: who the ticket can be assigned to. */
  assignees: Lookup[];
}

export interface HelpdeskSummary {
  open: number;
  inProgress: number;
  waiting: number;
  resolved: number;
  overdue: number;
  unassigned: number;
  mine: number;
}

export type QueueScope = 'all' | 'mine' | 'unassigned' | 'overdue';
export type QueueStatus = 'open' | 'resolved' | 'closed' | 'any';

export const helpdeskApi = {
  me: () => http.get<{ isAgent: boolean }>('/helpdesk/me'),
  categories: () => http.get<TicketCategory[]>('/helpdesk/categories'),
  createCategory: (b: SaveTicketCategory) => http.post('/helpdesk/categories', b),
  updateCategory: (id: string, b: SaveTicketCategory) => http.put(`/helpdesk/categories/${id}`, b),
  deleteCategory: (id: string) => http.del(`/helpdesk/categories/${id}`),
  my: () => http.get<TicketListItem[]>('/helpdesk/tickets/my'),
  queue: (q: { scope: QueueScope; status: QueueStatus; categoryId?: string; search?: string }) => http.get<TicketListItem[]>('/helpdesk/tickets', q),
  summary: () => http.get<HelpdeskSummary>('/helpdesk/summary'),
  get: (id: string) => http.get<TicketDetail>(`/helpdesk/tickets/${id}`),
  create: (b: { forEmployeeId: string | null; categoryId: string; subject: string; description: string; priority: TicketPriority }) =>
    http.post<TicketListItem>('/helpdesk/tickets', b),
  comment: (id: string, body: string, isInternal: boolean) => http.post(`/helpdesk/tickets/${id}/comments`, { body, isInternal }),
  update: (id: string, b: { assigneeEmployeeId: string | null; priority: TicketPriority; categoryId: string }) => http.put(`/helpdesk/tickets/${id}`, b),
  status: (id: string, status: TicketStatus, note: string | null) => http.post(`/helpdesk/tickets/${id}/status`, { status, note }),
  involve: (id: string, employeeIds: string[]) => http.post(`/helpdesk/tickets/${id}/involve`, { employeeIds }),
  close: (id: string, rating: number | null, feedback: string | null) => http.post(`/helpdesk/tickets/${id}/close`, { rating, feedback }),
  reopen: (id: string, reason: string) => http.post(`/helpdesk/tickets/${id}/reopen`, { reason }),
  cancel: (id: string) => http.post(`/helpdesk/tickets/${id}/cancel`),
};
