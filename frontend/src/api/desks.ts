import { http } from './client';

export type SpaceKind = 'Desk' | 'MeetingRoom' | 'Cabin';
export type BookingStatus = 'Booked' | 'CheckedIn' | 'Cancelled' | 'Released';

export const KIND_LABELS: Record<SpaceKind, string> = { Desk: 'Desk', MeetingRoom: 'Meeting room', Cabin: 'Cabin' };

export interface DeskSettings {
  maxDaysAhead: number;
  requireCheckIn: boolean;
  releaseAfterMinutes: number;
  dayStartsAt: number;
}

export interface Floor {
  id: string;
  officeLocationId: string;
  office: string;
  name: string;
  order: number;
  desks: number;
  rooms: number;
}

export interface Space {
  id: string;
  floorId: string;
  name: string;
  kind: SpaceKind;
  seats: number;
  features: string | null;
  assignedEmployeeId: string | null;
  assignedTo: string | null;
  isActive: boolean;
}

export interface Slot {
  bookingId: string;
  employeeId: string;
  employeeName: string;
  from: string | null;
  to: string | null;
  title: string | null;
  status: BookingStatus;
  mine: boolean;
}

export interface SpaceDay {
  id: string;
  name: string;
  kind: SpaceKind;
  seats: number;
  features: string | null;
  assignedEmployeeId: string | null;
  assignedTo: string | null;
  bookings: Slot[];
  free: boolean;
}

export interface FloorDay {
  date: string;
  floorId: string;
  floor: string;
  office: string;
  spaces: SpaceDay[];
  desksFree: number;
  desksTotal: number;
}

export interface MyBooking {
  id: string;
  spaceId: string;
  space: string;
  kind: SpaceKind;
  floor: string;
  office: string;
  date: string;
  from: string | null;
  to: string | null;
  title: string | null;
  attendees: number;
  status: BookingStatus;
  canCheckIn: boolean;
}

export interface InOffice {
  employeeId: string;
  name: string;
  department: string | null;
  space: string;
  floor: string;
}

export interface Utilisation {
  workingDays: number;
  bookings: number;
  checkedIn: number;
  noShows: number;
  deskUsePercent: number;
  spaces: Array<{ name: string; floor: string; kind: SpaceKind; bookings: number; checkedIn: number; released: number; usedPercent: number }>;
}

export const desksApi = {
  settings: () => http.get<DeskSettings>('/desks/settings'),
  saveSettings: (b: DeskSettings) => http.put('/desks/settings', b),
  floors: () => http.get<Floor[]>('/desks/floors'),
  addFloor: (b: { officeLocationId: string; name: string; order: number }) => http.post('/desks/floors', b),
  updateFloor: (id: string, b: { officeLocationId: string; name: string; order: number }) => http.put(`/desks/floors/${id}`, b),
  deleteFloor: (id: string) => http.del(`/desks/floors/${id}`),
  spaces: (floorId: string) => http.get<Space[]>(`/desks/floors/${floorId}/spaces`),
  addSpace: (b: Omit<Space, 'id' | 'assignedTo'>) => http.post('/desks/spaces', b),
  updateSpace: (id: string, b: Omit<Space, 'id' | 'assignedTo'>) => http.put(`/desks/spaces/${id}`, b),
  deleteSpace: (id: string) => http.del(`/desks/spaces/${id}`),
  bulk: (b: { floorId: string; prefix: string; from: number; to: number; features: string | null }) =>
    http.post<{ added: number; skipped: number }>('/desks/spaces/bulk', b),

  day: (floorId: string, date: string) => http.get<FloorDay>(`/desks/floors/${floorId}/day`, { date }),
  book: (b: { spaceId: string; date: string; from: string | null; to: string | null; title: string | null; attendees: number }) =>
    http.post('/desks/bookings', b),
  cancel: (id: string) => http.post(`/desks/bookings/${id}/cancel`),
  checkIn: (id: string) => http.post(`/desks/bookings/${id}/check-in`),
  my: () => http.get<MyBooking[]>('/desks/bookings/my'),
  inOffice: (date: string) => http.get<InOffice[]>('/desks/in-office', { date }),
  utilisation: () => http.get<Utilisation>('/desks/utilisation'),
};
