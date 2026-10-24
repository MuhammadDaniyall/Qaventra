import type {
  CreateLoginRequest,
  EmergencyContact,
  Employee,
  EmployeeListItem,
  EmployeeQuery,
  IssuedLogin,
  IssueLoginsResult,
  JobChange,
  JobChangeKind,
  Lookup,
  ProfileChange,
  PagedResult,
  Salary,
  SaveEmployeeRequest,
  UpdateLoginRequest,
  UserRole,
} from '../types';
import { http } from './client';

export const employeesApi = {
  list: (q: EmployeeQuery) => http.get<PagedResult<EmployeeListItem>>('/employees', { ...q }),
  lookup: () => http.get<Lookup[]>('/employees/lookup'),
  /** Includes people who have left (for experience and relieving letters). */
  lookupAll: () => http.get<Lookup[]>('/employees/lookup', { includeFormer: true }),
  me: () => http.get<Employee>('/employees/me'),
  get: (id: string) => http.get<Employee>(`/employees/${id}`),
  orgChart: () => http.get<OrgChartNode[]>('/employees/org-chart'),
  create: (b: SaveEmployeeRequest) => http.post<Employee>('/employees', b),
  update: (id: string, b: SaveEmployeeRequest) => http.put<Employee>(`/employees/${id}`, b),
  createLogin: (id: string, b: CreateLoginRequest) => http.post(`/employees/${id}/login`, b),
  updateLogin: (id: string, b: UpdateLoginRequest) => http.put(`/employees/${id}/login`, b),
  /** Resolves to null (HTTP 204) when no salary has been set yet. */
  salary: async (id: string) => (await http.get<Salary | undefined>(`/employees/${id}/salary`)) ?? null,
  saveSalary: (id: string, b: Salary) => http.put(`/employees/${id}/salary`, b),
  issueLogins: (employeeIds: string[], role: UserRole = 'Employee') => http.post<IssueLoginsResult>('/employees/logins', { employeeIds, role }),
  resetPassword: (id: string) => http.post<IssuedLogin>(`/employees/${id}/login/reset-password`),
  emergencyContacts: (id: string) => http.get<EmergencyContact[]>(`/employees/${id}/emergency-contacts`),
  saveEmergencyContacts: (id: string, contacts: EmergencyContact[]) => http.put<EmergencyContact[]>(`/employees/${id}/emergency-contacts`, { contacts }),
  myEmergencyContacts: () => http.get<EmergencyContact[]>('/employees/me/emergency-contacts'),
  saveMyEmergencyContacts: (contacts: EmergencyContact[]) => http.put<EmergencyContact[]>('/employees/me/emergency-contacts', { contacts }),
  jobHistory: (id: string) => http.get<JobChange[]>(`/employees/${id}/job-history`),
  myJobHistory: () => http.get<JobChange[]>('/employees/me/job-history'),
  recordJobChange: (
    id: string,
    b: { kind: JobChangeKind; effectiveDate: string; departmentId: string | null; designationId: string | null; managerId: string | null; note: string | null },
  ) => http.post<Employee>(`/employees/${id}/job-changes`, b),
  rehire: (
    id: string,
    b: { joiningDate: string; status: 'Probation' | 'Active'; departmentId: string | null; designationId: string | null; managerId: string | null; shiftId: string | null; note: string | null },
  ) => http.post<Employee>(`/employees/${id}/rehire`, b),
};

export const profileChangesApi = {
  my: () => http.get<ProfileChange[]>('/employees/profile-changes/my'),
  list: (status: string = 'Pending') => http.get<ProfileChange[]>('/employees/profile-changes', { status }),
  submit: (changes: Record<string, string | null>, reason: string | null) => http.post<ProfileChange>('/employees/profile-changes', { changes, reason }),
  approve: (id: string, note: string | null) => http.post(`/employees/profile-changes/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/employees/profile-changes/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/employees/profile-changes/${id}/cancel`),
};

/** One person on the org chart. */
export interface OrgChartNode {
  id: string;
  name: string;
  designation: string | null;
  department: string | null;
  managerId: string | null;
}
