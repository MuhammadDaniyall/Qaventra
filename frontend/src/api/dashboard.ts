import type { Dashboard } from '../types';
import { http } from './client';

export const dashboardApi = {
  get: () => http.get<Dashboard>('/dashboard'),
  trends: () => http.get<MonthTrend[]>('/dashboard/trends'),
  celebrations: () => http.get<Celebration[]>('/dashboard/celebrations'),
  wish: (employeeId: string, kind: Celebration['kind']) => http.post(`/dashboard/celebrations/${employeeId}/wish`, { kind }),
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

/** A birthday or work anniversary in the coming week. */
export interface Celebration {
  employeeId: string;
  name: string;
  designation: string | null;
  department: string | null;
  kind: 'Birthday' | 'Anniversary';
  date: string;
  years: number | null;
  isToday: boolean;
  isMe: boolean;
  wishedByMe: boolean;
}
