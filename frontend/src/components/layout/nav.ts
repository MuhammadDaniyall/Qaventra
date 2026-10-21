import {
  Banknote,
  Boxes,
  Building2,
  CalendarCheck,
  CalendarRange,
  CalendarClock,
  CalendarDays,
  DoorOpen,
  FileSignature,
  FolderOpen,
  Headset,
  Inbox,
  Laptop,
  LifeBuoy,
  Megaphone,
  Receipt,
  ReceiptText,
  UserMinus,
  ClipboardCheck,
  FileText,
  LayoutDashboard,
  ScrollText,
  Settings2,
  UserCircle,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { APPROVER_ROLES, COMPANY_ROLES, effectiveRole, HR_ROLES } from '../../auth/roles';
import type { Me, UserRole } from '../../types';

export interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  roles: UserRole[];
  /** Needs a linked employee record (self-service). */
  needsEmployee?: boolean;
  /** Match only the exact path for the active state. */
  end?: boolean;
}

export interface NavSection {
  title?: string;
  items: NavItem[];
}

const SA: UserRole[] = ['SuperAdmin'];

export const NAV: NavSection[] = [
  {
    items: [
      { to: '/platform', label: 'Platform overview', icon: LayoutDashboard, roles: SA, end: true },
      { to: '/platform/companies', label: 'Companies', icon: Building2, roles: SA },
      { to: '/platform/audit-logs', label: 'Audit logs', icon: ScrollText, roles: SA },
      { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard, roles: COMPANY_ROLES },
    ],
  },
  {
    title: 'My workspace',
    items: [
      { to: '/announcements', label: 'Announcements', icon: Megaphone, roles: COMPANY_ROLES },
      { to: '/me/attendance', label: 'My attendance', icon: CalendarClock, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/leave', label: 'My leave', icon: CalendarDays, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/payslips', label: 'My payslips', icon: FileText, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/expenses', label: 'My expenses', icon: Receipt, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/pay', label: 'Pay requests', icon: Banknote, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/documents', label: 'My documents', icon: FolderOpen, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/profile', label: 'My profile', icon: UserCircle, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/assets', label: 'My assets', icon: Laptop, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/helpdesk', label: 'Help desk', icon: LifeBuoy, roles: COMPANY_ROLES, needsEmployee: true, end: true },
      { to: '/me/resignation', label: 'Resignation', icon: DoorOpen, roles: COMPANY_ROLES, needsEmployee: true },
    ],
  },
  {
    title: 'Team',
    items: [
      { to: '/approvals', label: 'Approvals', icon: Inbox, roles: APPROVER_ROLES },
      { to: '/employees', label: 'Employees', icon: Users, roles: APPROVER_ROLES },
      { to: '/attendance/daily', label: 'Daily attendance', icon: CalendarCheck, roles: APPROVER_ROLES },
      { to: '/attendance/roster', label: 'Roster', icon: CalendarRange, roles: APPROVER_ROLES },
      { to: '/attendance/monthly', label: 'Monthly attendance', icon: CalendarDays, roles: APPROVER_ROLES },
      { to: '/leave/approvals', label: 'Leave approvals', icon: ClipboardCheck, roles: APPROVER_ROLES },
      { to: '/expenses/approvals', label: 'Expense approvals', icon: ReceiptText, roles: APPROVER_ROLES },
    ],
  },
  {
    title: 'Administration',
    items: [
      { to: '/payroll', label: 'Payroll', icon: Wallet, roles: HR_ROLES },
      { to: '/helpdesk/queue', label: 'Help desk queue', icon: Headset, roles: HR_ROLES },
      { to: '/assets', label: 'Assets', icon: Boxes, roles: HR_ROLES },
      { to: '/letters', label: 'HR letters', icon: FileSignature, roles: HR_ROLES },
      { to: '/separations', label: 'Separations', icon: UserMinus, roles: HR_ROLES },
      { to: '/settings', label: 'Organization', icon: Settings2, roles: HR_ROLES },
      { to: '/audit-logs', label: 'Audit logs', icon: ScrollText, roles: ['CompanyAdmin'] },
    ],
  },
];

export function navFor(user: Me): NavSection[] {
  const role = effectiveRole(user)!;
  return NAV.map((s) => ({
    ...s,
    // Self-service needs an employee record (the owner inside a company has none).
    items: s.items.filter((i) => i.roles.includes(role) && (!i.needsEmployee || !!user.employeeId)),
  })).filter((s) => s.items.length > 0);
}
