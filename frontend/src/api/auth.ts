import type { ChangePasswordRequest, LoginRequest, Me, Session } from '../types';
import { http, request } from './client';

export const authApi = {
  login: (body: LoginRequest) => request<Session>('/auth/login', { method: 'POST', body, silentAuth: true }),
  /** `token` overrides the active token (used to validate the saved platform session). */
  me: (token?: string) => request<Me>('/auth/me', { silentAuth: true, token }),
  logout: (token?: string) => request<void>('/auth/logout', { method: 'POST', body: {}, silentAuth: true, token }),
  changePassword: (body: ChangePasswordRequest) => http.post('/auth/change-password', body),
};
