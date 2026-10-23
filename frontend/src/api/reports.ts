import { ApiError, getToken, http } from './client';

export type ReportFilter = 'period' | 'date' | 'month' | 'year' | 'department' | 'employee' | 'includeLeft';

export interface ReportDefinition {
  key: string;
  category: string;
  name: string;
  description: string;
  filters: ReportFilter[];
  hrOnly: boolean;
}

export interface ReportRequest {
  from?: string | null;
  to?: string | null;
  date?: string | null;
  year?: number | null;
  month?: number | null;
  departmentIds?: string[];
  employeeId?: string | null;
  includeLeft?: boolean;
}

export interface ReportResult {
  title: string;
  subtitle: string | null;
  headers: string[];
  numeric: boolean[];
  rows: string[][];
  totals: string[] | null;
}

export const reportsApi = {
  list: () => http.get<ReportDefinition[]>('/reports'),
  run: (key: string, req: ReportRequest) => http.post<ReportResult>(`/reports/${key}`, req),
  settings: () => http.get<{ signatures: string[] }>('/reports/settings'),
  saveSettings: (signatures: string[]) => http.put('/reports/settings', { signatures }),
  /** The report as an Excel file (the request carries filters, so it is a POST). */
  excel: async (key: string, req: ReportRequest, fileName: string) => {
    const token = getToken();
    const res = await fetch(`/api/reports/${key}/excel`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: JSON.stringify(req),
    });
    if (!res.ok) throw new ApiError(res.status, { status: res.status, title: 'Could not export the report.' });
    const url = URL.createObjectURL(await res.blob());
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};
