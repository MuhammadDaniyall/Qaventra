import { http } from './client';

export type TripType = 'OneWay' | 'Return' | 'MultiCity';
export type TravelMode = 'Air' | 'Train' | 'Bus' | 'Car' | 'CompanyVehicle' | 'Other';
export type TravelStatus = 'Pending' | 'Approved' | 'Booked' | 'Rejected' | 'Cancelled';
export type SettlementStatus = 'None' | 'Submitted' | 'Returned' | 'Settled';
export type ExpenseCategory = 'Travel' | 'Fuel' | 'Meals' | 'Accommodation' | 'Medical' | 'Mobile' | 'OfficeSupplies' | 'Training' | 'Other';

export const TRIP_LABELS: Record<TripType, string> = { OneWay: 'One way', Return: 'Return', MultiCity: 'Multi-city' };
export const MODE_LABELS: Record<TravelMode, string> = {
  Air: 'Air',
  Train: 'Train',
  Bus: 'Bus',
  Car: 'Own car',
  CompanyVehicle: 'Company vehicle',
  Other: 'Other',
};
export const SETTLEMENT_LABELS: Record<SettlementStatus, string> = {
  None: 'Not settled',
  Submitted: 'Settlement submitted',
  Returned: 'Settlement sent back',
  Settled: 'Settled',
};
export const TRAVEL_EXPENSE_CATEGORIES: ExpenseCategory[] = ['Travel', 'Fuel', 'Meals', 'Accommodation', 'Mobile', 'Other'];
export const CATEGORY_LABELS: Record<ExpenseCategory, string> = {
  Travel: 'Local transport',
  Fuel: 'Fuel',
  Meals: 'Meals',
  Accommodation: 'Accommodation',
  Medical: 'Medical',
  Mobile: 'Mobile / internet',
  OfficeSupplies: 'Office supplies',
  Training: 'Training',
  Other: 'Other',
};

export interface TravelMe {
  isDesk: boolean;
  isHr: boolean;
  minNoticeDays: number;
  perDiemDomestic: number;
  perDiemInternational: number;
  settleWithinDays: number;
}

export interface TravelSettings {
  minNoticeDays: number;
  perDiemDomestic: number;
  perDiemInternational: number;
  settleWithinDays: number;
  deskIds: string[];
  desk: string[];
}

export interface TravelReason {
  id: string;
  name: string;
  isActive: boolean;
  trips: number;
}

export interface LegInput {
  from: string;
  to: string;
  date: string;
  mode: TravelMode;
  preference: string | null;
}

export interface SaveTrip {
  reasonId: string | null;
  purpose: string;
  tripType: TripType;
  isInternational: boolean;
  returnOn: string | null;
  legs: LegInput[];
  urgentReason: string | null;
  needsHotel: boolean;
  hotelCity: string | null;
  checkIn: string | null;
  checkOut: string | null;
  needsPickDrop: boolean;
  pickDropNotes: string | null;
  estimatedCost: number | null;
  advanceRequested: number | null;
}

export interface TripListItem {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  department: string | null;
  route: string;
  destination: string;
  tripType: TripType;
  isInternational: boolean;
  departOn: string;
  returnOn: string;
  purpose: string;
  reason: string | null;
  status: TravelStatus;
  settlement: SettlementStatus;
  urgent: boolean;
  needsHotel: boolean;
  advanceRequested: number | null;
  advancePaid: number | null;
  bookedCost: number;
  settlementOverdue: boolean;
  createdAtUtc: string;
}

export interface TripLeg {
  id: string;
  order: number;
  from: string;
  to: string;
  date: string;
  mode: TravelMode;
  preference: string | null;
  bookingRef: string | null;
  bookingCost: number | null;
}

export interface TripExpense {
  id: string;
  date: string;
  category: ExpenseCategory;
  description: string;
  amount: number;
}

export interface Trip {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  reasonId: string | null;
  reason: string | null;
  purpose: string;
  tripType: TripType;
  isInternational: boolean;
  departOn: string;
  returnOn: string;
  urgentReason: string | null;
  needsHotel: boolean;
  hotelCity: string | null;
  checkIn: string | null;
  checkOut: string | null;
  needsPickDrop: boolean;
  pickDropNotes: string | null;
  estimatedCost: number | null;
  advanceRequested: number | null;
  status: TravelStatus;
  decisionNote: string | null;
  hotelName: string | null;
  hotelRef: string | null;
  hotelCost: number | null;
  bookingNotes: string | null;
  bookedByName: string | null;
  bookedAtUtc: string | null;
  advancePaid: number | null;
  advancePaidOn: string | null;
  advancePaidVia: string | null;
  settlement: SettlementStatus;
  perDiemDays: number | null;
  perDiemRate: number | null;
  perDiemRateNow: number;
  settlementNote: string | null;
  settlementDecisionNote: string | null;
  settledAtUtc: string | null;
  claimed: number;
  net: number;
  bookedCost: number;
  settleBy: string;
  legs: TripLeg[];
  expenses: TripExpense[];
  isMine: boolean;
  isDesk: boolean;
  isHr: boolean;
  createdAtUtc: string;
}

export interface TravelReportRow {
  name: string;
  trips: number;
  booked: number;
  claimed: number;
}

export interface TravelReport {
  trips: number;
  upcoming: number;
  toBook: number;
  settlementsWaiting: number;
  settlementsOverdue: number;
  advancesOutstanding: number;
  bookedCost: number;
  claimedCost: number;
  byDepartment: TravelReportRow[];
  byDestination: TravelReportRow[];
  byMonth: TravelReportRow[];
}

export type DeskFilter = 'book' | 'advance' | 'settle' | 'unsettled' | 'pending' | 'all';

export const travelApi = {
  me: () => http.get<TravelMe>('/travel/me'),
  settings: () => http.get<TravelSettings>('/travel/settings'),
  saveSettings: (b: Omit<TravelSettings, 'desk'>) => http.put('/travel/settings', b),
  reasons: () => http.get<TravelReason[]>('/travel/reasons'),
  addReason: (b: { name: string; isActive: boolean }) => http.post('/travel/reasons', b),
  updateReason: (id: string, b: { name: string; isActive: boolean }) => http.put(`/travel/reasons/${id}`, b),
  deleteReason: (id: string) => http.del(`/travel/reasons/${id}`),

  my: () => http.get<TripListItem[]>('/travel/my'),
  all: (filter: DeskFilter, search: string) =>
    http.get<TripListItem[]>('/travel/all', { filter: filter === 'all' ? undefined : filter, search: search || undefined }),
  get: (id: string) => http.get<Trip>(`/travel/${id}`),
  create: (b: SaveTrip) => http.post<Trip>('/travel', b),
  approve: (id: string, note: string | null) => http.post(`/travel/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/travel/${id}/reject`, { note }),
  cancel: (id: string, note: string | null) => http.post(`/travel/${id}/cancel`, { note }),
  book: (
    id: string,
    b: {
      legs: Array<{ legId: string; bookingRef: string | null; bookingCost: number | null }>;
      hotelName: string | null;
      hotelRef: string | null;
      hotelCost: number | null;
      notes: string | null;
    },
  ) => http.post(`/travel/${id}/booking`, b),
  advance: (id: string, b: { amount: number; paidOn: string; via: string | null }) => http.post(`/travel/${id}/advance`, b),
  settle: (id: string, b: { expenses: Array<Omit<TripExpense, 'id'>>; perDiemDays: number; note: string | null }) => http.post(`/travel/${id}/settlement`, b),
  approveSettlement: (id: string, note: string | null) => http.post(`/travel/${id}/settlement/approve`, { note }),
  returnSettlement: (id: string, note: string | null) => http.post(`/travel/${id}/settlement/return`, { note }),
  report: () => http.get<TravelReport>('/travel/report'),
};
