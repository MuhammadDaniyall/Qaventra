import type { Attendance, DailyRow, ManualAttendanceRequest, PunchRequest, Today } from '../types';
import { http } from './client';

export const attendanceApi = {
  today: () => http.get<Today>('/attendance/today'),
  checkIn: (b: PunchRequest) => http.post<Attendance>('/attendance/check-in', b),
  checkOut: (b: PunchRequest) => http.post<Attendance>('/attendance/check-out', b),
  my: (year: number, month: number) => http.get<Attendance[]>('/attendance/my', { year, month }),
  daily: (date: string) => http.get<DailyRow[]>('/attendance/daily', { date }),
  manual: (b: ManualAttendanceRequest) => http.post<Attendance>('/attendance/manual', b),
};
