import { http } from './client';

export type NotificationKind = 'ApprovalNeeded' | 'RequestDecided' | 'Separation' | 'Letter' | 'Payslip' | 'Celebration';

export interface AppNotification {
  id: string;
  kind: NotificationKind;
  title: string;
  body: string | null;
  link: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationFeed {
  unread: number;
  emailEnabled: boolean;
  items: AppNotification[];
}

export const notificationsApi = {
  feed: () => http.get<NotificationFeed>('/notifications'),
  read: (id: string) => http.post(`/notifications/${id}/read`),
  readAll: () => http.post('/notifications/read-all'),
  setEmail: (emailEnabled: boolean) => http.put('/notifications/preferences', { emailEnabled }),
  pushKey: () => http.get<{ publicKey: string }>('/notifications/push-key'),
  subscribePush: (b: { endpoint: string; p256dh: string; auth: string }) => http.post('/notifications/push-subscriptions', b),
  unsubscribePush: (endpoint: string) => http.del(`/notifications/push-subscriptions?endpoint=${encodeURIComponent(endpoint)}`),
};
