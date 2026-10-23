import { http } from './client';

export type OnboardingTaskCategory = 'Paperwork' | 'ItSetup' | 'Workspace' | 'Training' | 'Introduction' | 'Other';
export type OnboardingAssignee = 'NewJoiner' | 'LineManager' | 'Buddy' | 'Hr' | 'SpecificEmployee';
export type OnboardingStatus = 'InProgress' | 'Completed' | 'Cancelled';

export const TASK_CATEGORY_LABELS: Record<OnboardingTaskCategory, string> = {
  Paperwork: 'Paperwork',
  ItSetup: 'IT setup',
  Workspace: 'Workspace',
  Training: 'Training',
  Introduction: 'Introductions',
  Other: 'Other',
};

export const ASSIGNEE_LABELS: Record<OnboardingAssignee, string> = {
  NewJoiner: 'The new joiner',
  LineManager: 'Their manager',
  Buddy: 'Their buddy',
  Hr: 'HR',
  SpecificEmployee: 'A specific person',
};

export interface TemplateTask {
  id: string | null;
  title: string;
  description: string | null;
  category: OnboardingTaskCategory;
  assigneeKind: OnboardingAssignee;
  assigneeEmployeeId: string | null;
  /** Days from the joining date; negative is before. */
  dueOffsetDays: number;
}

export interface OnboardingTemplate {
  id: string;
  name: string;
  description: string | null;
  isDefault: boolean;
  assetKitId: string | null;
  welcomeMessage: string | null;
  tasks: TemplateTask[];
}

export type SaveOnboardingTemplate = Omit<OnboardingTemplate, 'id'>;

export interface OnboardingTask {
  id: string;
  onboardingId: string;
  title: string;
  description: string | null;
  category: OnboardingTaskCategory;
  assigneeKind: OnboardingAssignee;
  assigneeEmployeeId: string | null;
  assignee: string | null;
  dueOn: string;
  isOverdue: boolean;
  completedAtUtc: string | null;
  completedByName: string | null;
  note: string | null;
  canDo: boolean;
  /** "My tasks" only: who is being onboarded. */
  forEmployee: string | null;
}

export interface OnboardingSummary {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  department: string | null;
  designation: string | null;
  joiningDate: string;
  templateName: string;
  status: OnboardingStatus;
  total: number;
  done: number;
  overdue: number;
  introduced: boolean;
}

export interface OnboardingDetail {
  summary: OnboardingSummary;
  reportingTime: string | null;
  reportingPlace: string | null;
  buddyEmployeeId: string | null;
  buddy: string | null;
  buddyPhone: string | null;
  manager: string | null;
  instructions: string | null;
  startedByName: string | null;
  introduction: Record<string, string | null> | null;
  tasks: OnboardingTask[];
}

export interface Welcome {
  onboarding: OnboardingDetail | null;
  questions: { key: string; question: string }[];
  hasPhoto: boolean;
  profileComplete: boolean;
  emergencyContacts: number;
}

export const onboardingApi = {
  templates: () => http.get<OnboardingTemplate[]>('/onboarding/templates'),
  createTemplate: (b: SaveOnboardingTemplate) => http.post<OnboardingTemplate>('/onboarding/templates', b),
  updateTemplate: (id: string, b: SaveOnboardingTemplate) => http.put<OnboardingTemplate>(`/onboarding/templates/${id}`, b),
  deleteTemplate: (id: string) => http.del(`/onboarding/templates/${id}`),
  list: (status: OnboardingStatus = 'InProgress') => http.get<OnboardingSummary[]>('/onboarding', { status }),
  start: (b: { employeeId: string; templateId: string | null; reportingTime: string | null; reportingPlace: string | null; buddyEmployeeId: string | null; instructions: string | null }) =>
    http.post<OnboardingDetail>('/onboarding', b),
  get: (id: string) => http.get<OnboardingDetail>(`/onboarding/${id}`),
  addTask: (id: string, b: { title: string; category: OnboardingTaskCategory; assigneeKind: OnboardingAssignee; assigneeEmployeeId: string | null; dueOn: string }) =>
    http.post<OnboardingDetail>(`/onboarding/${id}/tasks`, b),
  deleteTask: (taskId: string) => http.del(`/onboarding/tasks/${taskId}`),
  setTask: (taskId: string, done: boolean, note?: string | null) => http.post(`/onboarding/tasks/${taskId}`, { done, note: note ?? null }),
  cancel: (id: string) => http.post(`/onboarding/${id}/cancel`),
  myTasks: (done = false) => http.get<OnboardingTask[]>('/onboarding/my-tasks', { done }),
  welcome: () => http.get<Welcome>('/onboarding/welcome'),
  introduce: (answers: Record<string, string | null>) => http.post('/onboarding/introduce', { answers }),
};
