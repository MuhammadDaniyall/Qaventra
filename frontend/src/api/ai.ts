import { http } from './client';

export type AiActionKind =
  | 'apply_leave'
  | 'attendance_request'
  | 'helpdesk_ticket'
  | 'decide_approval'
  | 'expense_claim'
  | 'loan_request'
  | 'letter_request'
  | 'asset_request'
  | 'training_request';

export interface AiAction {
  kind: AiActionKind;
  summary: string;
  payload: Record<string, unknown>;
}

export interface AiTurn {
  role: 'user' | 'assistant';
  text: string;
}

export interface AiReply {
  reply: string;
  actions: AiAction[];
  remaining: number;
}

export interface AiStatus {
  enabled: boolean;
  limit: number;
  used: number;
}

export interface AiCvScreen {
  score: number;
  summary: string;
  strengths: string[];
  gaps: string[];
  questions: string[];
  fromCv: boolean;
}

export interface AiInsights {
  headline: string;
  findings: { title: string; detail: string; level: 'good' | 'watch' | 'act'; action: string }[];
  madeAtUtc: string;
}

export const aiApi = {
  status: () => http.get<AiStatus>('/ai/status'),
  screenCv: (applicationId: string, refresh = false) => http.post<AiCvScreen>(`/ai/cv/${applicationId}?refresh=${refresh}`),
  insights: (refresh = false) => http.get<AiInsights>('/ai/insights', { refresh }),
  ask: (messages: AiTurn[], voice: boolean) => http.post<AiReply>('/ai/ask', { messages, voice }),
  /** Sends a prepared action through the normal endpoint, as if the person had filled the form themselves. */
  confirm: (a: AiAction) => {
    const p = a.payload;
    switch (a.kind) {
      case 'apply_leave':
        return http.post('/leave', p);
      case 'attendance_request':
        return http.post('/attendance/requests', p);
      case 'helpdesk_ticket':
        return http.post('/helpdesk/tickets', p);
      case 'expense_claim':
        return http.post('/expenses', p);
      case 'loan_request':
        return http.post('/pay/loans', p);
      case 'letter_request':
        return http.post('/letters/request', p);
      case 'asset_request':
        return http.post('/assets/requests/new', p);
      case 'training_request':
        return http.post('/training/requests', p);
      case 'decide_approval':
        return http.post(`/approvals/${String(p.requestType)}/${String(p.subjectId)}/decide`, { approve: p.approve === true, note: p.note ?? null });
    }
  },
};
