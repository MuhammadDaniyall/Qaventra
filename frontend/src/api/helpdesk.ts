import type { Lookup } from '../types';
import { http } from './client';
import { downloadFile } from './excel';

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

export type TicketAssignment = 'Manual' | 'RoundRobin' | 'LeastBusy';

export interface TicketType {
  id: string;
  name: string;
  /** Overrides the category's target when set. */
  targetHours: number | null;
  /** Overrides the category's agents when not empty. */
  agentIds: string[];
  agents: string[];
  isActive: boolean;
}

export interface SaveTicketType {
  name: string;
  targetHours: number | null;
  agentIds: string[];
  isActive: boolean;
}

export interface HelpdeskLabels {
  statuses: Record<TicketStatus, string>;
  priorities: Record<TicketPriority, string>;
}

export interface ReportRow {
  id: string | null;
  name: string;
  raised: number;
  resolved: number;
  open: number;
  avgFirstResponseHours: number | null;
  avgResolutionHours: number | null;
  slaMetPercent: number | null;
  avgRating: number | null;
  rated: number;
}

export interface HelpdeskReport {
  from: string;
  to: string;
  total: ReportRow;
  categories: ReportRow[];
  agents: ReportRow[];
  byPriority: Record<TicketPriority, number>;
  byStatus: Record<TicketStatus, number>;
}

export interface TicketCategory {
  id: string;
  name: string;
  description: string | null;
  targetHours: number;
  agentIds: string[];
  agents: string[];
  isActive: boolean;
  openTickets: number;
  leadEmployeeId: string | null;
  lead: string | null;
  assignmentMode: TicketAssignment;
  agentCanClose: boolean;
  autoCloseDays: number;
  reopenDays: number;
  types: TicketType[];
}

export interface SaveTicketCategory {
  name: string;
  description: string | null;
  targetHours: number;
  agentIds: string[];
  isActive: boolean;
  leadEmployeeId: string | null;
  assignmentMode: TicketAssignment;
  agentCanClose: boolean;
  autoCloseDays: number;
  reopenDays: number;
}

export interface TicketListItem {
  id: string;
  code: string;
  subject: string;
  categoryId: string;
  category: string;
  typeId: string | null;
  type: string | null;
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
  editedAtUtc: string | null;
  isDeleted: boolean;
  /** The signed-in user wrote it recently and may still change it. */
  canEdit: boolean;
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
  agentCanClose: boolean;
  canReopen: boolean;
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
  create: (b: { forEmployeeId: string | null; categoryId: string; typeId: string | null; subject: string; description: string; priority: TicketPriority }) =>
    http.post<TicketListItem>('/helpdesk/tickets', b),
  comment: (id: string, body: string, isInternal: boolean) => http.post(`/helpdesk/tickets/${id}/comments`, { body, isInternal }),
  update: (id: string, b: { assigneeEmployeeId: string | null; priority: TicketPriority; categoryId: string; typeId: string | null }) => http.put(`/helpdesk/tickets/${id}`, b),
  editComment: (id: string, commentId: string, body: string) => http.put(`/helpdesk/tickets/${id}/comments/${commentId}`, { body }),
  deleteComment: (id: string, commentId: string) => http.del(`/helpdesk/tickets/${id}/comments/${commentId}`),
  createType: (categoryId: string, b: SaveTicketType) => http.post(`/helpdesk/categories/${categoryId}/types`, b),
  updateType: (id: string, b: SaveTicketType) => http.put(`/helpdesk/types/${id}`, b),
  deleteType: (id: string) => http.del(`/helpdesk/types/${id}`),
  labels: () => http.get<HelpdeskLabels>('/helpdesk/settings'),
  saveLabels: (b: HelpdeskLabels) => http.put<HelpdeskLabels>('/helpdesk/settings', b),
  report: (from: string, to: string) => http.get<HelpdeskReport>('/helpdesk/report', { from, to }),
  exportExcel: (from: string, to: string) => downloadFile(`/helpdesk/export?from=${from}&to=${to}`, `helpdesk-${from}-to-${to}.xlsx`),
  status: (id: string, status: TicketStatus, note: string | null) => http.post(`/helpdesk/tickets/${id}/status`, { status, note }),
  involve: (id: string, employeeIds: string[]) => http.post(`/helpdesk/tickets/${id}/involve`, { employeeIds }),
  close: (id: string, rating: number | null, feedback: string | null) => http.post(`/helpdesk/tickets/${id}/close`, { rating, feedback }),
  reopen: (id: string, reason: string) => http.post(`/helpdesk/tickets/${id}/reopen`, { reason }),
  cancel: (id: string) => http.post(`/helpdesk/tickets/${id}/cancel`),
};
