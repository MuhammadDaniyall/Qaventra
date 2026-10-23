import { http } from './client';

export type AlertKind = 'Birthday' | 'WorkAnniversary' | 'ProbationEnd' | 'DocumentExpiry' | 'NewJoiner' | 'LeaveStarting' | 'NotCheckedIn';
export type ReportKind = 'Headcount' | 'DailyAttendance' | 'OnLeave' | 'PendingApprovals' | 'ExpiringDocuments' | 'JoinersAndLeavers' | 'ProbationDue';
export type ReportFrequency = 'Daily' | 'Weekly' | 'Monthly';

export const ALERT_KINDS: Record<AlertKind, { label: string; title: string; body: string; hint: string }> = {
  Birthday: { label: 'Birthday', title: 'Happy birthday, {firstname}! 🎂', body: 'Wishing {name} a wonderful year ahead.', hint: 'Uses date of birth' },
  WorkAnniversary: {
    label: 'Work anniversary',
    title: '{name} completes {years} years with us',
    body: 'Thank you for {years} years, {firstname}!',
    hint: 'Uses joining date',
  },
  ProbationEnd: {
    label: 'Probation ending',
    title: "{name}'s probation ends on {date}",
    body: 'Please complete the probation review.',
    hint: 'People still on probation',
  },
  DocumentExpiry: {
    label: 'Document expiring',
    title: "{name}'s {document} expires on {date}",
    body: 'Please ask for a renewed copy.',
    hint: 'Employee documents with an expiry date',
  },
  NewJoiner: {
    label: 'New joiner',
    title: '{name} joins {department} on {date}',
    body: 'Please get their desk, laptop and login ready.',
    hint: 'Uses joining date',
  },
  LeaveStarting: { label: 'Leave starting', title: '{name} is on leave from {date}', body: '{details}', hint: 'Approved leave' },
  NotCheckedIn: {
    label: 'Not checked in',
    title: '{name} has not checked in today',
    body: 'They are not on leave and today is a working day.',
    hint: 'Sent at the chosen hour',
  },
};

export const REPORT_KINDS: Record<ReportKind, { label: string; description: string }> = {
  Headcount: { label: 'Headcount', description: 'Employees by department, with joiners and leavers in the period' },
  DailyAttendance: { label: 'Attendance', description: 'Who is in, late, absent or on leave (morning reports show yesterday)' },
  OnLeave: { label: 'Who is on leave', description: 'Approved and pending leave for the coming week (or month)' },
  PendingApprovals: { label: 'Pending approvals', description: 'Everything waiting, who it waits for and for how long' },
  ExpiringDocuments: { label: 'Expiring documents', description: 'CNICs, passports and other documents expired or expiring soon' },
  JoinersAndLeavers: { label: 'Joiners and leavers', description: 'People who joined or left in the period' },
  ProbationDue: { label: 'Probation reviews due', description: 'Probation ending in the next 30 days or overdue' },
};

export const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export interface SaveAlert {
  name: string;
  kind: AlertKind;
  daysBefore: number;
  probationMonths: number;
  sendHour: number;
  toEmployee: boolean;
  toManager: boolean;
  toHr: boolean;
  toEveryone: boolean;
  extraRecipientIds: string[];
  departmentIds: string[];
  titleTemplate: string;
  bodyTemplate: string | null;
  sendEmail: boolean;
  isActive: boolean;
}

export interface ScheduledAlert extends SaveAlert {
  id: string;
  lastRunOn: string | null;
}

export interface SaveReport {
  name: string;
  kind: ReportKind;
  frequency: ReportFrequency;
  day: number;
  sendHour: number;
  recipientIds: string[];
  extraEmails: string | null;
  departmentIds: string[];
  isActive: boolean;
}

export interface ScheduledReport extends SaveReport {
  id: string;
  recipients: string[];
  extraEmails: string;
  lastSentAtUtc: string | null;
  nextRunAtUtc: string | null;
}

export interface ReportTable {
  title: string;
  subtitle: string | null;
  headers: string[];
  rows: string[][];
}

export interface ScheduledRun {
  id: string;
  alertId: string | null;
  reportId: string | null;
  name: string;
  recipients: number;
  items: number;
  note: string | null;
  manual: boolean;
  createdAtUtc: string;
}

export const scheduledApi = {
  alerts: () => http.get<ScheduledAlert[]>('/scheduled/alerts'),
  addAlert: (b: SaveAlert) => http.post<ScheduledAlert>('/scheduled/alerts', b),
  updateAlert: (id: string, b: SaveAlert) => http.put<ScheduledAlert>(`/scheduled/alerts/${id}`, b),
  deleteAlert: (id: string) => http.del(`/scheduled/alerts/${id}`),
  previewAlert: (id: string) => http.get<{ count: number; titles: string[] }>(`/scheduled/alerts/${id}/preview`),
  runAlert: (id: string) => http.post<{ people: number }>(`/scheduled/alerts/${id}/run`),

  reports: () => http.get<ScheduledReport[]>('/scheduled/reports'),
  addReport: (b: SaveReport) => http.post('/scheduled/reports', b),
  updateReport: (id: string, b: SaveReport) => http.put(`/scheduled/reports/${id}`, b),
  deleteReport: (id: string) => http.del(`/scheduled/reports/${id}`),
  previewReport: (id: string) => http.get<ReportTable>(`/scheduled/reports/${id}/preview`),
  previewDraft: (b: SaveReport) => http.post<ReportTable>('/scheduled/reports/preview', b),
  excelPath: (id: string) => `/scheduled/reports/${id}/excel`,
  send: (id: string, test: boolean) => http.post<{ recipients: number }>(`/scheduled/reports/${id}/send${test ? '?test=true' : ''}`),

  runs: () => http.get<ScheduledRun[]>('/scheduled/runs'),
};
