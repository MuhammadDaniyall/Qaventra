import { http } from './client';

export interface SalaryTax {
  taxYear: string;
  taxYears: string[];
  monthlySalary: number;
  annualSalary: number;
  annualTax: number;
  monthlyTax: number;
  monthlyTakeHome: number;
  annualTakeHome: number;
  effectiveRate: number;
  slab: { fromAmount: number; toAmount: number | null; fixedTax: number; rate: number } | null;
  surchargeRate: number;
  surchargeThreshold: number;
}

export interface Eobi {
  minimumWage: number;
  employeeRate: number;
  employerRate: number;
  employeeMonthly: number;
  employerMonthly: number;
  totalMonthly: number;
  employeeYearly: number;
  employerYearly: number;
}

export interface Gratuity {
  lastMonthlySalary: number;
  serviceYears: number;
  daysPerYear: number;
  minYears: number;
  eligible: boolean;
  gratuity: number;
}

export interface LeaveEncashment {
  monthlySalary: number;
  days: number;
  dailyRate: number;
  amount: number;
}

export const publicApi = {
  salaryTax: (monthlySalary: number, taxYear?: string) => http.get<SalaryTax>('/public/tools/salary-tax', { monthlySalary, taxYear }),
  eobi: (minimumWage?: number) => http.get<Eobi>('/public/tools/eobi', { minimumWage }),
  gratuity: (q: { lastMonthlySalary: number; serviceYears?: number; daysPerYear?: number; minYears?: number }) =>
    http.get<Gratuity>('/public/tools/gratuity', q),
  leaveEncashment: (monthlySalary: number, days: number) =>
    http.get<LeaveEncashment>('/public/tools/leave-encashment', { monthlySalary, days }),
  verify: (code: string) =>
    http.get<{ employeeName: string; courseTitle: string; issuedOn: string }>(`/public/certificates/${encodeURIComponent(code)}`),
};
