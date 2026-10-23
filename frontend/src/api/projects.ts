import { http } from './client';

export type ProjectStatus = 'Active' | 'OnHold' | 'Closed';
export type TimesheetStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';

export interface ProjectMember {
  employeeId: string;
  name?: string | null;
  hourlyRate: number | null;
}

export interface ProjectTask {
  id: string;
  name: string;
  assigneeEmployeeId: string | null;
  assignee: string | null;
  dueDate: string | null;
  estimatedHours: number | null;
  isDone: boolean;
  loggedHours: number;
}

export interface Project {
  id: string;
  name: string;
  code: string | null;
  client: string | null;
  description: string | null;
  managerEmployeeId: string | null;
  manager: string | null;
  startDate: string | null;
  endDate: string | null;
  budgetHours: number | null;
  billable: boolean;
  openToAll: boolean;
  status: ProjectStatus;
  members: ProjectMember[];
  tasks: ProjectTask[];
  loggedHours: number;
  cost: number;
  canManage: boolean;
}

export interface SaveProject {
  name: string;
  code: string | null;
  client: string | null;
  description: string | null;
  managerEmployeeId: string | null;
  startDate: string | null;
  endDate: string | null;
  budgetHours: number | null;
  billable: boolean;
  openToAll: boolean;
  status: ProjectStatus;
  members: ProjectMember[];
}

export interface SaveTask {
  name: string;
  assigneeEmployeeId: string | null;
  dueDate: string | null;
  estimatedHours: number | null;
  isDone: boolean;
}

export interface TimesheetLine {
  date: string;
  projectId: string;
  taskId: string | null;
  minutes: number;
  note: string | null;
}

export interface Timesheet {
  id: string | null;
  employeeId: string;
  employeeName: string;
  weekStart: string;
  status: TimesheetStatus;
  submittedAtUtc: string | null;
  decisionNote: string | null;
  lines: TimesheetLine[];
  totalMinutes: number;
  /** Minutes at work per day (yyyy-mm-dd) from attendance. */
  attendanceMinutes: Record<string, number>;
}

export const projectsApi = {
  list: (includeClosed = false) => http.get<Project[]>('/projects', { includeClosed }),
  get: (id: string) => http.get<Project>(`/projects/${id}`),
  add: (b: SaveProject) => http.post<Project>('/projects', b),
  update: (id: string, b: SaveProject) => http.put<Project>(`/projects/${id}`, b),
  remove: (id: string) => http.del(`/projects/${id}`),
  addTask: (id: string, b: SaveTask) => http.post(`/projects/${id}/tasks`, b),
  updateTask: (id: string, taskId: string, b: SaveTask) => http.put(`/projects/${id}/tasks/${taskId}`, b),
  deleteTask: (id: string, taskId: string) => http.del(`/projects/${id}/tasks/${taskId}`),
  week: (start: string) => http.get<Timesheet>('/timesheets/week', { start }),
  saveWeek: (weekStart: string, lines: TimesheetLine[]) => http.put<Timesheet>('/timesheets/week', { weekStart, lines }),
  submit: (start: string) => http.post<Timesheet>(`/timesheets/week/submit?start=${start}`),
  withdraw: (start: string) => http.post<Timesheet>(`/timesheets/week/withdraw?start=${start}`),
  approvals: (status: TimesheetStatus = 'Submitted') => http.get<Timesheet[]>('/timesheets/approvals', { status }),
  approve: (id: string, note: string | null) => http.post(`/timesheets/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/timesheets/${id}/reject`, { note }),
};
