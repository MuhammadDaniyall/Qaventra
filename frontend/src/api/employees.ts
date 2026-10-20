import type {
  CreateLoginRequest,
  Employee,
  EmployeeListItem,
  EmployeeQuery,
  Lookup,
  PagedResult,
  Salary,
  SaveEmployeeRequest,
  UpdateLoginRequest,
} from '../types';
import { http } from './client';

export const employeesApi = {
  list: (q: EmployeeQuery) => http.get<PagedResult<EmployeeListItem>>('/employees', { ...q }),
  lookup: () => http.get<Lookup[]>('/employees/lookup'),
  /** Includes people who have left (for experience and relieving letters). */
  lookupAll: () => http.get<Lookup[]>('/employees/lookup', { includeFormer: true }),
  me: () => http.get<Employee>('/employees/me'),
  get: (id: string) => http.get<Employee>(`/employees/${id}`),
  create: (b: SaveEmployeeRequest) => http.post<Employee>('/employees', b),
  update: (id: string, b: SaveEmployeeRequest) => http.put<Employee>(`/employees/${id}`, b),
  createLogin: (id: string, b: CreateLoginRequest) => http.post(`/employees/${id}/login`, b),
  updateLogin: (id: string, b: UpdateLoginRequest) => http.put(`/employees/${id}/login`, b),
  /** Resolves to null (HTTP 204) when no salary has been set yet. */
  salary: async (id: string) => (await http.get<Salary | undefined>(`/employees/${id}/salary`)) ?? null,
  saveSalary: (id: string, b: Salary) => http.put(`/employees/${id}/salary`, b),
};
