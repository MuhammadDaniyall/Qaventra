import type { Dashboard } from '../types';
import { http } from './client';

export const dashboardApi = {
  get: () => http.get<Dashboard>('/dashboard'),
};
