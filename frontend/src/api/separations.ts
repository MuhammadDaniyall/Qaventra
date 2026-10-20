import type {
  InitiateSeparationRequest,
  SaveSettlementRequest,
  Separation,
  SeparationStatus,
  SeparationSummary,
} from '../types/workflows';
import { http } from './client';

export interface ExitInterviewRequest {
  exitReason: string | null;
  rating: number;
  wouldRecommend: boolean;
  feedback: string | null;
}

export const separationsApi = {
  /** Undefined when the employee never started a separation (204). */
  my: () => http.get<Separation | undefined>('/separations/my'),
  resign: (proposedLastDay: string, reason: string | null) =>
    http.post<Separation>('/separations/resign', { proposedLastDay, reason }),
  list: (status: SeparationStatus | '') => http.get<SeparationSummary[]>('/separations', { status }),
  initiate: (b: InitiateSeparationRequest) => http.post<Separation>('/separations', b),
  get: (id: string) => http.get<Separation>(`/separations/${id}`),
  accept: (id: string, lastWorkingDay: string, noticePeriodDays: number, note: string | null) =>
    http.post<Separation>(`/separations/${id}/accept`, { lastWorkingDay, noticePeriodDays, note }),
  decline: (id: string, note: string | null) => http.post<Separation>(`/separations/${id}/decline`, { note }),
  withdraw: (id: string) => http.post(`/separations/${id}/withdraw`),
  exitInterview: (id: string, b: ExitInterviewRequest) => http.put<Separation>(`/separations/${id}/exit-interview`, b),
  addClearance: (id: string, department: string, title: string) =>
    http.post(`/separations/${id}/clearance`, { department, title }),
  updateClearance: (id: string, itemId: string, isCleared: boolean, note: string | null) =>
    http.put(`/separations/${id}/clearance/${itemId}`, { isCleared, note }),
  deleteClearance: (id: string, itemId: string) => http.del(`/separations/${id}/clearance/${itemId}`),
  saveSettlement: (id: string, b: SaveSettlementRequest) => http.put<Separation>(`/separations/${id}/settlement`, b),
  complete: (id: string) => http.post<Separation>(`/separations/${id}/complete`),
};
