import { fetchBlob, http } from './client';

export type ImportKind = 'employees' | 'attendance';

export interface ImportRowResult {
  row: number;
  label: string;
  errors: string[];
  notes: string[];
}

export interface ImportResult {
  committed: boolean;
  total: number;
  valid: number;
  invalid: number;
  created: number;
  updated: number;
  rows: ImportRowResult[];
}

/** Saves a protected file under the given name (the bearer token cannot go in a plain link). */
export async function downloadFile(path: string, fileName: string): Promise<void> {
  const url = URL.createObjectURL(await fetchBlob(path));
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
}

export const excelApi = {
  template: (kind: ImportKind) => downloadFile(`/imports/${kind}/template`, `${kind}-import-template.xlsx`),
  /** commit=false only checks; commit=true imports when every row is valid. */
  upload: (kind: ImportKind, file: File, commit: boolean, createMissing = false) => {
    const form = new FormData();
    form.append('file', file);
    form.append('createMissing', String(createMissing));
    return http.upload<ImportResult>(`/imports/${kind}?commit=${commit}`, form);
  },
};
