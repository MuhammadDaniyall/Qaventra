import { http } from './client';

export type ExitQuestionKind = 'Rating' | 'YesNo' | 'Text';

export interface ClearanceTemplateItem {
  department: string;
  title: string;
  level: number;
  responsibleEmployeeId: string | null;
}
export interface ClearanceTemplate {
  id: string;
  name: string;
  isDefault: boolean;
  items: ClearanceTemplateItem[];
}

export interface ExitQuestion {
  id: string | null;
  text: string;
  category: string | null;
  kind: ExitQuestionKind;
}
export interface ExitTemplate {
  id: string;
  name: string;
  isDefault: boolean;
  questions: ExitQuestion[];
}

export interface ExitAnswer {
  questionId: string;
  question: string;
  kind: ExitQuestionKind;
  category: string | null;
  answer: string | null;
}

export interface SeparationExtras {
  separationId: string;
  clearance: { itemId: string; level: number; responsibleEmployeeId: string | null; responsible: string | null; open: boolean }[];
  exitTemplateId: string | null;
  answers: ExitAnswer[];
  interviewerIds: string[];
  interviewScheduledUtc: string | null;
  interviewNotes: string | null;
  settlementSubmittedAtUtc: string | null;
  settlementApprovedAtUtc: string | null;
  settlementApprovalNote: string | null;
  settlementPaidAtUtc: string | null;
  settlementPaidVia: string | null;
}

export interface MyClearance {
  separationId: string;
  itemId: string;
  employeeName: string;
  employeeCode: string;
  lastWorkingDay: string | null;
  department: string;
  title: string;
  level: number;
  isCleared: boolean;
  note: string | null;
}

const base = '/separation-setup';
export const separationSetupApi = {
  clearanceTemplates: () => http.get<ClearanceTemplate[]>(`${base}/clearance-templates`),
  addClearanceTemplate: (b: Omit<ClearanceTemplate, 'id'>) => http.post(`${base}/clearance-templates`, b),
  updateClearanceTemplate: (id: string, b: Omit<ClearanceTemplate, 'id'>) => http.put(`${base}/clearance-templates/${id}`, b),
  deleteClearanceTemplate: (id: string) => http.del(`${base}/clearance-templates/${id}`),
  exitTemplates: () => http.get<ExitTemplate[]>(`${base}/exit-templates`),
  addExitTemplate: (b: Omit<ExitTemplate, 'id'>) => http.post(`${base}/exit-templates`, b),
  updateExitTemplate: (id: string, b: Omit<ExitTemplate, 'id'>) => http.put(`${base}/exit-templates/${id}`, b),
  deleteExitTemplate: (id: string) => http.del(`${base}/exit-templates/${id}`),
  extras: (id: string) => http.get<SeparationExtras>(`${base}/separations/${id}`),
  answer: (id: string, answers: { questionId: string; answer: string | null }[]) => http.put(`${base}/separations/${id}/answers`, { answers }),
  panel: (id: string, b: { templateId: string | null; interviewerIds: string[]; scheduledUtc: string | null; notes: string | null }) =>
    http.put(`${base}/separations/${id}/panel`, b),
  setOwner: (id: string, itemId: string, level: number, responsibleEmployeeId: string | null) =>
    http.put(`${base}/separations/${id}/clearance/${itemId}/owner`, { level, responsibleEmployeeId }),
  myClearances: () => http.get<MyClearance[]>(`${base}/my-clearances`),
  clearMine: (id: string, itemId: string, isCleared: boolean, note: string | null) =>
    http.put(`${base}/separations/${id}/clearance/${itemId}/mine`, { isCleared, note }),
  submitSettlement: (id: string) => http.post(`${base}/separations/${id}/settlement/submit`),
  settlementPaid: (id: string, via: string | null) => http.post(`${base}/separations/${id}/settlement/paid`, { via }),
};
