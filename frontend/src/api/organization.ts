import type {
  Department,
  Designation,
  Holiday,
  LeaveType,
  OfficeLocation,
  SaveDepartmentRequest,
  SaveDesignationRequest,
  SaveHolidayRequest,
  SaveLeaveTypeRequest,
  SaveOfficeLocationRequest,
  SaveShiftRequest,
  Shift,
} from '../types';
import { http } from './client';

export const orgApi = {
  departments: () => http.get<Department[]>('/departments'),
  createDepartment: (b: SaveDepartmentRequest) => http.post<Department>('/departments', b),
  updateDepartment: (id: string, b: SaveDepartmentRequest) => http.put(`/departments/${id}`, b),
  deleteDepartment: (id: string) => http.del(`/departments/${id}`),

  designations: () => http.get<Designation[]>('/designations'),
  createDesignation: (b: SaveDesignationRequest) => http.post<Designation>('/designations', b),
  updateDesignation: (id: string, b: SaveDesignationRequest) => http.put(`/designations/${id}`, b),
  deleteDesignation: (id: string) => http.del(`/designations/${id}`),
};

export const setupApi = {
  offices: () => http.get<OfficeLocation[]>('/setup/office-locations'),
  createOffice: (b: SaveOfficeLocationRequest) => http.post<OfficeLocation>('/setup/office-locations', b),
  updateOffice: (id: string, b: SaveOfficeLocationRequest) => http.put(`/setup/office-locations/${id}`, b),
  deleteOffice: (id: string) => http.del(`/setup/office-locations/${id}`),

  shifts: () => http.get<Shift[]>('/setup/shifts'),
  createShift: (b: SaveShiftRequest) => http.post<Shift>('/setup/shifts', b),
  updateShift: (id: string, b: SaveShiftRequest) => http.put(`/setup/shifts/${id}`, b),
  deleteShift: (id: string) => http.del(`/setup/shifts/${id}`),

  holidays: (year?: number) => http.get<Holiday[]>('/setup/holidays', { year }),
  createHoliday: (b: SaveHolidayRequest) => http.post<Holiday>('/setup/holidays', b),
  updateHoliday: (id: string, b: SaveHolidayRequest) => http.put(`/setup/holidays/${id}`, b),
  deleteHoliday: (id: string) => http.del(`/setup/holidays/${id}`),

  leaveTypes: () => http.get<LeaveType[]>('/setup/leave-types'),
  createLeaveType: (b: SaveLeaveTypeRequest) => http.post<LeaveType>('/setup/leave-types', b),
  updateLeaveType: (id: string, b: SaveLeaveTypeRequest) => http.put(`/setup/leave-types/${id}`, b),
  deleteLeaveType: (id: string) => http.del(`/setup/leave-types/${id}`),
};
