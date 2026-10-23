import type {
  IssuedLetter,
  IssueLetterRequest,
  LetterCategory,
  LetterDocument,
  LetterPlaceholder,
  LetterPreview,
  LetterTemplate,
  SaveLetterTemplateRequest,
} from '../types/workflows';
import { http } from './client';

export const lettersApi = {
  placeholders: () => http.get<LetterPlaceholder[]>('/letters/placeholders'),
  templates: () => http.get<LetterTemplate[]>('/letters/templates'),
  createTemplate: (b: SaveLetterTemplateRequest) => http.post<LetterTemplate>('/letters/templates', b),
  updateTemplate: (id: string, b: SaveLetterTemplateRequest) => http.put(`/letters/templates/${id}`, b),
  deleteTemplate: (id: string) => http.del(`/letters/templates/${id}`),
  preview: (employeeId: string, subject: string, body: string) => http.post<LetterPreview>('/letters/preview', { employeeId, subject, body }),
  list: (search: string, category: LetterCategory | '') => http.get<IssuedLetter[]>('/letters', { search, category }),
  my: () => http.get<IssuedLetter[]>('/letters/my'),
  selfService: () => http.get<{ id: string; name: string; category: string }[]>('/letters/self-service'),
  request: (templateId: string, reason: string | null) => http.post<IssuedLetter>('/letters/request', { templateId, reason }),
  myRequests: () => http.get<IssuedLetter[]>('/letters/requests/my'),
  pending: () => http.get<IssuedLetter[]>('/letters/pending'),
  approve: (id: string, note: string | null) => http.post(`/letters/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/letters/${id}/reject`, { note }),
  candidate: (applicationId: string) => http.get<CandidateLetter | undefined>(`/letters/candidate/${applicationId}`),
  issue: (b: IssueLetterRequest) => http.post<IssuedLetter>('/letters', b),
  get: (id: string) => http.get<LetterDocument>(`/letters/${id}`),
  setSharing: (id: string, shared: boolean) => http.put(`/letters/${id}/sharing?shared=${shared}`),
  remove: (id: string) => http.del(`/letters/${id}`),
};

export interface CandidateLetter {
  id: string;
  applicationId: string;
  candidateName: string;
  referenceNo: string;
  subject: string;
  body: string;
  issuedOn: string;
  issuedByName: string | null;
  marginTopMm: number;
  marginSideMm: number;
  footer: string | null;
  companyName: string;
  companyAddress: string | null;
  companyPhone: string | null;
}
