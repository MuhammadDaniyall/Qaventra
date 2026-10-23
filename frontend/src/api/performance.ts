import { http } from './client';

export type ReviewSectionKind = 'Goals' | 'Competencies';
export type ReviewStage = 'Goals' | 'GoalsSubmitted' | 'SelfReview' | 'ManagerReview' | 'Calibration' | 'SignOff' | 'Done';
export type ReviewCycleStatus = 'Draft' | 'Active' | 'Closed';

export const STAGE_LABELS: Record<ReviewStage, string> = {
  Goals: 'Writing goals',
  GoalsSubmitted: 'Goals waiting for approval',
  SelfReview: 'Self review',
  ManagerReview: 'Manager review',
  Calibration: 'With HR',
  SignOff: 'Waiting for sign-off',
  Done: 'Done',
};

export const STAGE_ORDER: ReviewStage[] = ['Goals', 'GoalsSubmitted', 'SelfReview', 'ManagerReview', 'Calibration', 'SignOff', 'Done'];
export const POTENTIAL_LABELS = ['', 'Low', 'Medium', 'High'];

export interface ReviewSection {
  name: string;
  weight: number;
  kind: ReviewSectionKind;
  items: { title: string; description: string | null }[];
}

export interface PerformanceSettings {
  ratingLabels: string[];
  bellCurve: number[];
  raiseSlabs: { minScore: number; raisePercent: number }[];
}

export interface PerformanceTemplate {
  id: string;
  name: string;
  description: string | null;
  sections: ReviewSection[];
  selfReview: boolean;
  cycles: number;
}

export interface ReviewCycle {
  id: string;
  name: string;
  periodFrom: string;
  periodTo: string;
  templateId: string;
  template: string;
  departmentIds: string[];
  departments: string[];
  goalsDue: string | null;
  selfReviewDue: string | null;
  managerReviewDue: string | null;
  status: ReviewCycleStatus;
  reviews: number;
  byStage: Partial<Record<ReviewStage, number>>;
}

export interface SaveCycle {
  name: string;
  periodFrom: string;
  periodTo: string;
  templateId: string;
  departmentIds: string[];
  goalsDue: string | null;
  selfReviewDue: string | null;
  managerReviewDue: string | null;
}

export interface ReviewItem {
  id: string;
  sectionIndex: number;
  title: string;
  description: string | null;
  target: string | null;
  weight: number;
  selfScore: number | null;
  selfComment: string | null;
  managerScore: number | null;
  managerComment: string | null;
}

export interface Review {
  id: string;
  cycleId: string;
  cycle: string;
  periodFrom: string;
  periodTo: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  designation: string | null;
  department: string | null;
  reviewerEmployeeId: string | null;
  reviewer: string | null;
  stage: ReviewStage;
  sections: ReviewSection[];
  selfReviewEnabled: boolean;
  items: ReviewItem[];
  selfScore: number | null;
  selfComment: string | null;
  managerScore: number | null;
  managerComment: string | null;
  strengths: string | null;
  improvements: string | null;
  developmentPlan: string | null;
  potential: number | null;
  finalRating: number | null;
  finalLabel: string | null;
  calibrationNote: string | null;
  employeeAgrees: boolean | null;
  employeeSignOffComment: string | null;
  due: string | null;
  isEmployee: boolean;
  isReviewer: boolean;
  isHr: boolean;
}

export interface ReviewListItem {
  id: string;
  cycleId: string;
  cycle: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  department: string | null;
  reviewer: string | null;
  stage: ReviewStage;
  selfScore: number | null;
  managerScore: number | null;
  finalRating: number | null;
  potential: number | null;
  employeeAgrees: boolean | null;
  suggestedRaise: number | null;
}

export interface Calibration {
  reviews: ReviewListItem[];
  bellTarget: number[];
  bellActual: number[];
  labels: string[];
}

export type ScoreInput = { itemId: string; score: number | null; comment: string | null };

export const performanceApi = {
  settings: () => http.get<PerformanceSettings>('/performance/settings'),
  saveSettings: (b: PerformanceSettings) => http.put<PerformanceSettings>('/performance/settings', b),
  templates: () => http.get<PerformanceTemplate[]>('/performance/templates'),
  createTemplate: (b: { name: string; description: string | null; sections: ReviewSection[]; selfReview: boolean }) => http.post('/performance/templates', b),
  updateTemplate: (id: string, b: { name: string; description: string | null; sections: ReviewSection[]; selfReview: boolean }) => http.put(`/performance/templates/${id}`, b),
  deleteTemplate: (id: string) => http.del(`/performance/templates/${id}`),
  cycles: () => http.get<ReviewCycle[]>('/performance/cycles'),
  createCycle: (b: SaveCycle) => http.post('/performance/cycles', b),
  updateCycle: (id: string, b: SaveCycle) => http.put(`/performance/cycles/${id}`, b),
  deleteCycle: (id: string) => http.del(`/performance/cycles/${id}`),
  launch: (id: string) => http.post<{ reviews: number }>(`/performance/cycles/${id}/launch`),
  closeCycle: (id: string) => http.post(`/performance/cycles/${id}/close`),
  release: (id: string, reviewIds?: string[]) => http.post<{ released: number }>(`/performance/cycles/${id}/release`, { reviewIds: reviewIds ?? null }),
  calibration: (id: string) => http.get<Calibration>(`/performance/cycles/${id}/calibration`),
  my: () => http.get<ReviewListItem[]>('/performance/reviews/my'),
  team: () => http.get<ReviewListItem[]>('/performance/reviews/team'),
  get: (id: string) => http.get<Review>(`/performance/reviews/${id}`),
  saveGoals: (id: string, goals: { sectionIndex: number; title: string; description: string | null; target: string | null; weight: number }[]) =>
    http.put<Review>(`/performance/reviews/${id}/goals`, { goals }),
  submitGoals: (id: string) => http.post<Review>(`/performance/reviews/${id}/goals/submit`),
  decideGoals: (id: string, approve: boolean, note: string | null) => http.post<Review>(`/performance/reviews/${id}/goals/decide`, { approve, note }),
  saveSelf: (id: string, b: { scores: ScoreInput[]; comment: string | null; submit: boolean }) => http.put<Review>(`/performance/reviews/${id}/self`, b),
  saveManager: (
    id: string,
    b: { scores: ScoreInput[]; comment: string | null; strengths: string | null; improvements: string | null; developmentPlan: string | null; potential: number | null; submit: boolean },
  ) => http.put<Review>(`/performance/reviews/${id}/manager`, b),
  calibrate: (id: string, finalRating: number, note: string | null) => http.post(`/performance/reviews/${id}/calibrate`, { finalRating, note }),
  signOff: (id: string, agree: boolean, comment: string | null) => http.post<Review>(`/performance/reviews/${id}/signoff`, { agree, comment }),
  revert: (id: string) => http.post(`/performance/reviews/${id}/revert`),
};
