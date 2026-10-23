import { http } from './client';

export type TrainingMode = 'Classroom' | 'Online' | 'OnTheJob' | 'External';
export type SessionStatus = 'Scheduled' | 'Completed' | 'Cancelled';
export type EnrollmentStatus = 'Enrolled' | 'Attended' | 'Completed' | 'NoShow' | 'Failed' | 'Withdrawn';
export type ComplianceState = 'Done' | 'DueSoon' | 'Overdue' | 'NotDone';
export type RequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export const MODE_LABELS: Record<TrainingMode, string> = { Classroom: 'Classroom', Online: 'Online', OnTheJob: 'On the job', External: 'Outside institute' };
export const ENROLLMENT_LABELS: Record<EnrollmentStatus, string> = {
  Enrolled: 'Booked',
  Attended: 'Attended',
  Completed: 'Completed',
  NoShow: 'No-show',
  Failed: 'Not passed',
  Withdrawn: 'Withdrawn',
};
export const COMPLIANCE_LABELS: Record<ComplianceState, string> = { Done: 'Up to date', DueSoon: 'Due soon', Overdue: 'Overdue', NotDone: 'Not done' };

export interface TrainingMe {
  isHr: boolean;
  hasTeam: boolean;
  isTrainer: boolean;
}

export interface TrainingCategory {
  id: string;
  name: string;
  programs: number;
}

export interface SaveProgram {
  title: string;
  categoryId: string | null;
  description: string | null;
  mode: TrainingMode;
  durationHours: number | null;
  link: string | null;
  isMandatory: boolean;
  departmentIds: string[];
  completeWithinDays: number | null;
  repeatEveryMonths: number | null;
  isActive: boolean;
}

export interface TrainingProgram extends SaveProgram {
  id: string;
  category: string | null;
  sessions: number;
  completed: number;
}

export interface SaveSession {
  programId: string;
  startsAtUtc: string;
  endsAtUtc: string;
  venue: string | null;
  trainerEmployeeId: string | null;
  externalTrainer: string | null;
  capacity: number | null;
  cost: number | null;
  notes: string | null;
}

export interface TrainingSession {
  id: string;
  programId: string;
  program: string;
  mode: TrainingMode;
  startsAtUtc: string;
  endsAtUtc: string;
  venue: string | null;
  trainerEmployeeId: string | null;
  trainer: string | null;
  externalTrainer: string | null;
  capacity: number | null;
  cost: number | null;
  status: SessionStatus;
  notes: string | null;
  enrolled: number;
  completed: number;
  averageRating: number | null;
  canRun: boolean;
}

export interface Enrollment {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  department: string | null;
  status: EnrollmentStatus;
  nominatedByName: string | null;
  score: number | null;
  completedOn: string | null;
  feedbackRating: number | null;
  trainerRating: number | null;
  wouldRecommend: boolean | null;
  feedbackComment: string | null;
}

export interface SessionDetail {
  session: TrainingSession;
  enrollments: Enrollment[];
  canNominate: boolean;
}

export interface MyTraining {
  enrollmentId: string;
  sessionId: string;
  program: string;
  mode: TrainingMode;
  startsAtUtc: string;
  endsAtUtc: string;
  venue: string | null;
  trainer: string | null;
  status: EnrollmentStatus;
  score: number | null;
  completedOn: string | null;
  feedbackGiven: boolean;
  link: string | null;
}

export interface ComplianceCell {
  employeeId: string;
  programId: string;
  state: ComplianceState;
  lastDone: string | null;
  dueOn: string | null;
  scheduled: boolean;
}

export interface Compliance {
  programs: Array<{ id: string; title: string; repeatEveryMonths: number | null }>;
  rows: Array<{ employeeId: string; name: string; code: string; department: string | null; cells: ComplianceCell[] }>;
  done: number;
  dueSoon: number;
  overdue: number;
  notDone: number;
}

export interface TrainingRequest {
  id: string;
  employeeId: string;
  employeeName: string;
  programId: string | null;
  course: string;
  provider: string | null;
  preferredDate: string | null;
  cost: number | null;
  reason: string;
  status: RequestStatus;
  decisionNote: string | null;
  completedOn: string | null;
  createdAtUtc: string;
}

export interface TrainingReportRow {
  name: string;
  sessions: number;
  people: number;
  hours: number;
  cost: number;
  rating: number | null;
}

export interface TrainingReport {
  sessions: number;
  peopleTrained: number;
  hours: number;
  cost: number;
  averageRating: number | null;
  recommendPercent: number | null;
  byProgram: TrainingReportRow[];
  byDepartment: TrainingReportRow[];
}

export const trainingApi = {
  me: () => http.get<TrainingMe>('/training/me'),
  categories: () => http.get<TrainingCategory[]>('/training/categories'),
  addCategory: (name: string) => http.post('/training/categories', { name }),
  updateCategory: (id: string, name: string) => http.put(`/training/categories/${id}`, { name }),
  deleteCategory: (id: string) => http.del(`/training/categories/${id}`),
  programs: () => http.get<TrainingProgram[]>('/training/programs'),
  addProgram: (b: SaveProgram) => http.post('/training/programs', b),
  updateProgram: (id: string, b: SaveProgram) => http.put(`/training/programs/${id}`, b),
  deleteProgram: (id: string) => http.del(`/training/programs/${id}`),

  sessions: (past = false) => http.get<TrainingSession[]>('/training/sessions', { past: past || undefined }),
  session: (id: string) => http.get<SessionDetail>(`/training/sessions/${id}`),
  addSession: (b: SaveSession) => http.post<TrainingSession>('/training/sessions', b),
  updateSession: (id: string, b: SaveSession) => http.put(`/training/sessions/${id}`, b),
  cancelSession: (id: string) => http.post(`/training/sessions/${id}/status`, { status: 'Cancelled' }),
  enroll: (id: string, employeeIds: string[]) => http.post<{ added: number }>(`/training/sessions/${id}/enroll`, { employeeIds }),
  attendance: (id: string, marks: Array<{ enrollmentId: string; status: EnrollmentStatus; score: number | null }>) =>
    http.post(`/training/sessions/${id}/attendance`, { marks }),
  withdraw: (enrollmentId: string) => http.post(`/training/enrollments/${enrollmentId}/withdraw`),
  feedback: (enrollmentId: string, b: { rating: number; trainerRating: number | null; wouldRecommend: boolean | null; comment: string | null }) =>
    http.post(`/training/enrollments/${enrollmentId}/feedback`, b),
  nominees: () => http.get<Array<{ id: string; name: string }>>('/training/nominees'),

  my: () => http.get<MyTraining[]>('/training/my'),
  myCompliance: () => http.get<Compliance>('/training/my/compliance'),
  compliance: (departmentId?: string) => http.get<Compliance>('/training/compliance', { departmentId: departmentId || undefined }),

  requests: () => http.get<TrainingRequest[]>('/training/requests'),
  request: (b: {
    programId: string | null;
    courseName: string | null;
    provider: string | null;
    preferredDate: string | null;
    cost: number | null;
    reason: string;
  }) => http.post('/training/requests', b),
  approve: (id: string, note: string | null) => http.post(`/training/requests/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/training/requests/${id}/reject`, { note }),
  cancelRequest: (id: string) => http.post(`/training/requests/${id}/cancel`),
  completeRequest: (id: string, completedOn: string) => http.post(`/training/requests/${id}/complete`, { completedOn }),

  report: () => http.get<TrainingReport>('/training/report'),
};
