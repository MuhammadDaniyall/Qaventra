import { http } from './client';

export type AiActionKind = 'apply_leave' | 'attendance_request' | 'helpdesk_ticket' | 'decide_approval';

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

export interface AiInsights {
  headline: string;
  findings: { title: string; detail: string; level: 'good' | 'watch' | 'act'; action: string }[];
  madeAtUtc: string;
}

export const aiApi = {
  status: () => http.get<AiStatus>('/ai/status'),
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
      case 'decide_approval':
        return http.post(`/approvals/${String(p.requestType)}/${String(p.subjectId)}/decide`, { approve: p.approve === true, note: p.note ?? null });
    }
  },
};
