import type { Dashboard } from '../types';
import { http } from './client';

export const dashboardApi = {
  get: () => http.get<Dashboard>('/dashboard'),
  trends: () => http.get<MonthTrend[]>('/dashboard/trends'),
};

/** One month of workforce numbers for the insights page. */
export interface MonthTrend {
  year: number;
  month: number;
  headcount: number;
  joiners: number;
  leavers: number;
  turnoverPercent: number;
  leaveDays: number;
  lateArrivals: number;
  averageWorkedHours: number;
}
