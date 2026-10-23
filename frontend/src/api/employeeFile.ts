import { useQuery } from '@tanstack/react-query';
import { http } from './client';
import { qk } from './keys';

export type LookupKind = 'Division' | 'Grade' | 'CostCenter' | 'Region' | 'EmployeeGroup' | 'SubDepartment' | 'ExitType';

export const LOOKUP_KINDS: { kind: LookupKind; label: string; plural: string; hint: string }[] = [
  { kind: 'Division', label: 'Division', plural: 'Divisions', hint: 'Business units above departments, e.g. Retail, Corporate.' },
  { kind: 'SubDepartment', label: 'Sub-department', plural: 'Sub-departments', hint: 'Teams inside a department, e.g. Payroll inside Finance.' },
  { kind: 'Grade', label: 'Grade', plural: 'Grades', hint: 'Pay or job bands, e.g. G1–G10, M1.' },
  { kind: 'CostCenter', label: 'Cost center', plural: 'Cost centers', hint: 'Where salary cost is booked in accounts.' },
  { kind: 'Region', label: 'Region / station', plural: 'Regions and stations', hint: 'Cities, branches or stations people work from.' },
  { kind: 'EmployeeGroup', label: 'Employee group', plural: 'Employee groups', hint: 'Any other grouping, e.g. Staff, Workers, Management.' },
  { kind: 'ExitType', label: 'Exit type', plural: 'Exit types', hint: 'Why people leave: resignation, termination, retirement, contract end.' },
];

export const lookupLabel = (k: LookupKind) => LOOKUP_KINDS.find((x) => x.kind === k)?.label ?? k;

export interface OrgLookup {
  id: string;
  kind: LookupKind;
  name: string;
  code: string | null;
  parentId: string | null;
  headEmployeeId: string | null;
  isActive: boolean;
  employees: number;
}

export type SaveOrgLookup = Omit<OrgLookup, 'id' | 'employees'>;

export interface EmployeeFile {
  fatherName: string | null;
  placeOfBirth: string | null;
  nationality: string | null;
  religion: string | null;
  cnicIssuedOn: string | null;
  cnicExpiresOn: string | null;
  passportNo: string | null;
  passportIssuedOn: string | null;
  passportExpiresOn: string | null;
  visaNo: string | null;
  visaType: string | null;
  visaExpiresOn: string | null;
  drivingLicenseNo: string | null;
  drivingLicenseExpiresOn: string | null;
  ntn: string | null;
  eobiNo: string | null;
  socialSecurityNo: string | null;
  insuranceProvider: string | null;
  insurancePolicyNo: string | null;
  insuranceExpiresOn: string | null;
  bankBranchCode: string | null;
  bankAccountTitle: string | null;
  divisionId: string | null;
  subDepartmentId: string | null;
  gradeId: string | null;
  costCenterId: string | null;
  regionId: string | null;
  employeeGroupId: string | null;
  confirmationDate: string | null;
  contractEndDate: string | null;
  punchCode: string | null;
  exitTypeId: string | null;
  exitReason: string | null;
  isBlacklisted: boolean;
  blacklistReason: string | null;
  allowWebCheckIn: boolean;
  allowMobileCheckIn: boolean;
  checkInAnywhere: boolean;
  allowedIps: string | null;
  ownLatitude: number | null;
  ownLongitude: number | null;
  ownRadiusMeters: number | null;
  paymentMethod: 'BankTransfer' | 'Cash' | 'Cheque';
  providentFundMember: boolean;
}

export interface Dependant {
  name: string;
  relation: string;
  cnic: string | null;
  dateOfBirth: string | null;
  gender: 'Male' | 'Female' | 'Other' | null;
  isNominee: boolean;
  nomineeShare: number | null;
  coveredByInsurance: boolean;
}

export interface Qualification {
  degree: string;
  institute: string;
  field: string | null;
  grade: string | null;
  fromYear: number | null;
  toYear: number | null;
  isHighest: boolean;
}

export interface WorkExperience {
  organization: string;
  designation: string;
  fromDate: string | null;
  toDate: string | null;
  lastSalary: number | null;
  reasonForLeaving: string | null;
}

export interface EmployeeRecords {
  dependants: Dependant[];
  qualifications: Qualification[];
  experience: WorkExperience[];
}

export const employeeFileApi = {
  lookups: () => http.get<OrgLookup[]>('/lookups'),
  addLookup: (b: SaveOrgLookup) => http.post<OrgLookup>('/lookups', b),
  updateLookup: (id: string, b: SaveOrgLookup) => http.put(`/lookups/${id}`, b),
  deleteLookup: (id: string) => http.del(`/lookups/${id}`),
  file: (employeeId?: string) => http.get<EmployeeFile>(employeeId ? `/employees/${employeeId}/file` : '/employees/me/file'),
  saveFile: (employeeId: string, b: EmployeeFile) => http.put<EmployeeFile>(`/employees/${employeeId}/file`, b),
  records: (employeeId?: string) => http.get<EmployeeRecords>(employeeId ? `/employees/${employeeId}/records` : '/employees/me/records'),
  saveRecords: (employeeId: string | undefined, b: EmployeeRecords) =>
    http.put<EmployeeRecords>(employeeId ? `/employees/${employeeId}/records` : '/employees/me/records', b),
};

export const useLookups = () => useQuery({ queryKey: qk.lookups, queryFn: employeeFileApi.lookups, staleTime: 5 * 60 * 1000 });
