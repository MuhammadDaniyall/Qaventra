import type { ChangePasswordRequest, LoginRequest, Me, Session } from '../types';
import { request } from './client';

export const authApi = {
  login: (body: LoginRequest) => request<Session>('/auth/login', { method: 'POST', body, silentAuth: true }),
  /** `token` overrides the active token (used to validate the saved platform session). */
  me: (token?: string) => request<Me>('/auth/me', { silentAuth: true, token }),
  logout: (token?: string) => request<void>('/auth/logout', { method: 'POST', body: {}, silentAuth: true, token }),
  /** Ends every other session; the returned session replaces this one. */
  changePassword: (body: ChangePasswordRequest) => request<Session>('/auth/change-password', { method: 'POST', body }),
};
