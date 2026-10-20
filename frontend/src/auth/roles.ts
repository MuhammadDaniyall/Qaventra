import type { Me, UserRole } from '../types';

export const HR_ROLES: UserRole[] = ['CompanyAdmin', 'HrManager'];
export const APPROVER_ROLES: UserRole[] = ['CompanyAdmin', 'HrManager', 'Manager'];
export const COMPANY_ROLES: UserRole[] = ['CompanyAdmin', 'HrManager', 'Manager', 'Employee'];

/**
 * The role the UI should treat the user as. The owner (SuperAdmin) inside a company has full
 * Company Super Admin rights there, so guards and navigation treat them as CompanyAdmin.
 */
export function effectiveRole(me: Me | null | undefined): UserRole | null {
  if (!me) return null;
  return me.role === 'SuperAdmin' && me.isActingAsOwner ? 'CompanyAdmin' : me.role;
}

export const isHr = (me?: Me | null) => HR_ROLES.includes(effectiveRole(me) as UserRole);
export const isApprover = (me?: Me | null) => APPROVER_ROLES.includes(effectiveRole(me) as UserRole);
export const isCompanyAdmin = (me?: Me | null) => effectiveRole(me) === 'CompanyAdmin';
/** Owner in the platform area (not inside a company). */
export const isPlatformOwner = (me?: Me | null) => effectiveRole(me) === 'SuperAdmin';
