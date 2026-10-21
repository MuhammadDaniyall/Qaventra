import { http, openProtectedFile } from './client';

export type AttachmentOwner = 'Leave' | 'Separation' | 'EmployeeDocument' | 'Announcement';
export type DocumentCategory = 'Cnic' | 'Passport' | 'Degree' | 'ExperienceLetter' | 'Contract' | 'Medical' | 'Photo' | 'Other';

export interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  category: DocumentCategory | null;
  expiresOn: string | null;
  note: string | null;
  uploadedByName: string | null;
  createdAtUtc: string;
  canDelete: boolean;
}

export interface AttachmentList {
  canAdd: boolean;
  items: Attachment[];
}

export const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;
export const ATTACHMENT_ACCEPT = '.pdf,.jpg,.jpeg,.png,.webp,.doc,.docx,.xls,.xlsx';

export const DOCUMENT_CATEGORIES: Array<{ value: DocumentCategory; label: string }> = [
  { value: 'Cnic', label: 'CNIC' },
  { value: 'Passport', label: 'Passport' },
  { value: 'Degree', label: 'Degree / certificate' },
  { value: 'ExperienceLetter', label: 'Experience letter' },
  { value: 'Contract', label: 'Contract' },
  { value: 'Medical', label: 'Medical' },
  { value: 'Photo', label: 'Photo' },
  { value: 'Other', label: 'Other' },
];

export const attachmentsApi = {
  list: (owner: AttachmentOwner, ownerId: string) => http.get<AttachmentList>(`/attachments/${owner}/${ownerId}`),
  upload: (owner: AttachmentOwner, ownerId: string, file: File, extra?: { category?: DocumentCategory; expiresOn?: string; note?: string }) => {
    const form = new FormData();
    form.append('file', file);
    if (extra?.category) form.append('category', extra.category);
    if (extra?.expiresOn) form.append('expiresOn', extra.expiresOn);
    if (extra?.note) form.append('note', extra.note);
    return http.upload<Attachment>(`/attachments/${owner}/${ownerId}`, form);
  },
  open: (a: Pick<Attachment, 'id' | 'fileName'>) => openProtectedFile(`/attachments/${a.id}/file`, a.fileName),
  remove: (id: string) => http.del(`/attachments/${id}`),
};

/** Checks type and size before uploading, so people get the message without waiting. Null when fine. */
export function attachmentProblem(file: File): string | null {
  const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
  if (!ATTACHMENT_ACCEPT.split(',').includes(ext)) return `${file.name}: use a PDF, an image or a Word/Excel file.`;
  if (file.size > MAX_ATTACHMENT_BYTES) return `${file.name} is larger than 5 MB.`;
  return null;
}

export function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${Math.round(n / 1024)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}
