import type { RecordStamps } from '../types';
import { http } from './client';

export const quizKeys = {
  all: ['training', 'quizzes'] as const,
  one: (id: string) => ['training', 'quizzes', id] as const,
  attempts: (id: string) => ['training', 'quizzes', id, 'attempts'] as const,
  certificates: ['training', 'certificates'] as const,
  certificate: (id: string) => ['training', 'certificates', id] as const,
  logo: ['training', 'certificate-logo'] as const,
};

export interface QuizQuestionInput {
  text: string;
  options: string[];
  correctIndex: number;
}

export interface SaveQuiz {
  programId: string;
  title: string;
  instructions: string | null;
  passPercent: number;
  timeLimitMinutes: number | null;
  maxAttempts: number;
  isActive: boolean;
  questions: QuizQuestionInput[];
}

export interface QuizEdit extends SaveQuiz {
  id: string;
}

export interface QuizListItem {
  id: string;
  programId: string;
  program: string;
  title: string;
  questions: number;
  passPercent: number;
  timeLimitMinutes: number | null;
  maxAttempts: number;
  isActive: boolean;
  attemptsUsed: number;
  bestScore: number | null;
  passed: boolean;
  certificateId: string | null;
  openAttemptId: string | null;
  takers: number;
  passRate: number | null;
  stamps: RecordStamps;
}

/** What the person taking the quiz sees: no right answers. */
export interface QuizPaper {
  attemptId: string;
  quizId: string;
  title: string;
  instructions: string | null;
  program: string;
  passPercent: number;
  startedAtUtc: string;
  deadlineUtc: string | null;
  questions: { id: string; text: string; options: string[] }[];
}

export interface QuizResult {
  attemptId: string;
  correct: number;
  total: number;
  scorePercent: number;
  passPercent: number;
  passed: boolean;
  timedOut: boolean;
  certificateId: string | null;
  certificateCode: string | null;
  review: { questionId: string; text: string; options: string[]; chosen: number | null; correctIndex: number }[];
}

export interface QuizAttemptRow {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  startedAtUtc: string;
  submittedAtUtc: string | null;
  scorePercent: number;
  passed: boolean;
  timedOut: boolean;
}

export interface Certificate {
  id: string;
  code: string;
  employeeName: string;
  courseTitle: string;
  issuedOn: string;
  scorePercent: number;
  companyName: string;
  logoDataUrl: string | null;
  verifyPath: string;
}

export const quizApi = {
  list: () => http.get<QuizListItem[]>('/training/quizzes'),
  get: (id: string) => http.get<QuizEdit>(`/training/quizzes/${id}`),
  create: (body: SaveQuiz) => http.post<QuizEdit>('/training/quizzes', body),
  update: (id: string, body: SaveQuiz) => http.put<QuizEdit>(`/training/quizzes/${id}`, body),
  remove: (id: string) => http.del(`/training/quizzes/${id}`),
  attempts: (id: string) => http.get<QuizAttemptRow[]>(`/training/quizzes/${id}/attempts`),
  draft: (programId: string, count: number, courseText: string | null) =>
    http.post<{ questions: QuizQuestionInput[] }>('/training/quizzes/draft', { programId, count, courseText }),
  start: (id: string) => http.post<QuizPaper>(`/training/quizzes/${id}/start`),
  submit: (attemptId: string, answers: Record<string, number>) =>
    http.post<QuizResult>(`/training/quizzes/attempts/${attemptId}/submit`, { answers }),
  myCertificates: () => http.get<Certificate[]>('/training/certificates/mine'),
  certificate: (id: string) => http.get<Certificate>(`/training/certificates/${id}`),
  logo: () => http.get<{ logoDataUrl: string | null }>('/training/certificate-logo'),
  saveLogo: (logoDataUrl: string | null) => http.put('/training/certificate-logo', { logoDataUrl }),
};
