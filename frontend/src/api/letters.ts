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
  preview: (employeeId: string, subject: string, body: string) =>
    http.post<LetterPreview>('/letters/preview', { employeeId, subject, body }),
  list: (search: string, category: LetterCategory | '') => http.get<IssuedLetter[]>('/letters', { search, category }),
  my: () => http.get<IssuedLetter[]>('/letters/my'),
  issue: (b: IssueLetterRequest) => http.post<IssuedLetter>('/letters', b),
  get: (id: string) => http.get<LetterDocument>(`/letters/${id}`),
  setSharing: (id: string, shared: boolean) => http.put(`/letters/${id}/sharing?shared=${shared}`),
  remove: (id: string) => http.del(`/letters/${id}`),
};
