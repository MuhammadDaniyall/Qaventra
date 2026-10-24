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
  BriefcaseBusiness,
  Plane,
  GraduationCap,
  BellRing,
  Armchair,
  Factory,
  Calculator,
  ChartLine,
  FileBarChart,
  Target,
  Rocket,
  Laptop,
  LifeBuoy,
  Megaphone,
  Network,
  Receipt,
  ReceiptText,
  UserMinus,
  ClipboardCheck,
  ClipboardList,
  Lightbulb,
  ShieldCheck,
  FolderKanban,
  FileText,
  House,
  LayoutDashboard,
  ScrollText,
  Send,
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
  /** Access-template module: HR users with a template see it only when included; others see it when included. */
  module?: string;
}

export interface NavSection {
  title?: string;
  /** Shown on the collapsible group header. */
  icon?: LucideIcon;
  items: NavItem[];
}

const SA: UserRole[] = ['SuperAdmin'];

export const NAV: NavSection[] = [
  {
    items: [
      { to: '/platform', label: 'Platform overview', icon: LayoutDashboard, roles: SA, end: true },
      { to: '/platform/companies', label: 'Companies', icon: Building2, roles: SA },
      { to: '/platform/audit-logs', label: 'Audit logs', icon: ScrollText, roles: SA },
      { to: '/dashboard', label: 'Home', icon: House, roles: COMPANY_ROLES },
      { to: '/me/requests', label: 'Requests', icon: Send, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/approvals', label: 'Approvals', icon: Inbox, roles: APPROVER_ROLES },
      { to: '/announcements', label: 'Announcements', icon: Megaphone, roles: COMPANY_ROLES },
    ],
  },
  {
    title: 'Time',
    icon: CalendarClock,
    items: [
      { to: '/me/attendance', label: 'My attendance', icon: CalendarClock, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/leave', label: 'My leave', icon: CalendarDays, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/timesheets', label: 'Timesheet', icon: ClipboardList, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/projects', label: 'Projects', icon: FolderKanban, roles: COMPANY_ROLES },
      { to: '/piece-work', label: 'Piece work', icon: Factory, roles: COMPANY_ROLES },
    ],
  },
  {
    title: 'Pay',
    icon: Wallet,
    items: [
      { to: '/me/payslips', label: 'My payslips', icon: FileText, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/expenses', label: 'My expenses', icon: Receipt, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/pay', label: 'Pay requests', icon: Banknote, roles: COMPANY_ROLES, needsEmployee: true },
    ],
  },
  {
    title: 'Growth',
    icon: Target,
    items: [
      { to: '/performance', label: 'Performance', icon: Target, roles: COMPANY_ROLES },
      { to: '/training', label: 'Training', icon: GraduationCap, roles: COMPANY_ROLES },
      { to: '/recruitment', label: 'Recruitment', icon: BriefcaseBusiness, roles: COMPANY_ROLES },
      { to: '/suggestions', label: 'Suggestions', icon: Lightbulb, roles: COMPANY_ROLES, needsEmployee: true },
    ],
  },
  {
    title: 'Workplace',
    icon: Building2,
    items: [
      { to: '/org-chart', label: 'Org chart', icon: Network, roles: COMPANY_ROLES },
      { to: '/desks', label: 'Desks and rooms', icon: Armchair, roles: COMPANY_ROLES },
      { to: '/travel', label: 'Travel', icon: Plane, roles: COMPANY_ROLES },
      { to: '/helpdesk', label: 'Help desk', icon: LifeBuoy, roles: COMPANY_ROLES, needsEmployee: true, end: true },
    ],
  },
  {
    title: 'Me',
    icon: UserCircle,
    items: [
      { to: '/me/profile', label: 'My profile', icon: UserCircle, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/documents', label: 'My documents', icon: FolderOpen, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/assets', label: 'My assets', icon: Laptop, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/clearances', label: 'Exit clearances', icon: ClipboardCheck, roles: COMPANY_ROLES, needsEmployee: true },
      { to: '/me/resignation', label: 'Resignation', icon: DoorOpen, roles: COMPANY_ROLES, needsEmployee: true },
    ],
  },
  {
    title: 'Team',
    icon: Users,
    items: [
      { to: '/employees', label: 'Employees', icon: Users, roles: APPROVER_ROLES, module: 'employees' },
      { to: '/attendance/daily', label: 'Daily attendance', icon: CalendarCheck, roles: APPROVER_ROLES, module: 'attendance' },
      { to: '/attendance/roster', label: 'Roster', icon: CalendarRange, roles: APPROVER_ROLES, module: 'attendance' },
      { to: '/attendance/monthly', label: 'Monthly attendance', icon: CalendarDays, roles: APPROVER_ROLES, module: 'attendance' },
      { to: '/leave/approvals', label: 'Leave approvals', icon: ClipboardCheck, roles: APPROVER_ROLES, module: 'leave' },
      { to: '/expenses/approvals', label: 'Expense approvals', icon: ReceiptText, roles: APPROVER_ROLES, module: 'expenses' },
      { to: '/reports', label: 'Reports', icon: FileBarChart, roles: APPROVER_ROLES, module: 'reports' },
    ],
  },
  {
    title: 'Administration',
    icon: Settings2,
    items: [
      { to: '/payroll', label: 'Payroll', icon: Wallet, roles: HR_ROLES, module: 'payroll' },
      { to: '/helpdesk/queue', label: 'Help desk queue', icon: Headset, roles: HR_ROLES, module: 'helpdesk' },
      { to: '/assets', label: 'Assets', icon: Boxes, roles: HR_ROLES, module: 'assets' },
      { to: '/onboarding', label: 'Onboarding', icon: Rocket, roles: HR_ROLES, module: 'onboarding' },
      { to: '/letters', label: 'HR letters', icon: FileSignature, roles: HR_ROLES, module: 'letters' },
      { to: '/separations', label: 'Separations', icon: UserMinus, roles: HR_ROLES, module: 'separations' },
      { to: '/budget', label: 'Manpower budget', icon: Calculator, roles: HR_ROLES, module: 'budget' },
      { to: '/insights', label: 'Workforce insights', icon: ChartLine, roles: HR_ROLES, module: 'reports' },
      { to: '/alerts', label: 'Alerts and reports', icon: BellRing, roles: HR_ROLES, module: 'reports' },
      { to: '/settings', label: 'Organization', icon: Settings2, roles: HR_ROLES, module: 'organization' },
      { to: '/settings/access', label: 'Access', icon: ShieldCheck, roles: ['CompanyAdmin'] },
      { to: '/audit-logs', label: 'Audit logs', icon: ScrollText, roles: ['CompanyAdmin'] },
    ],
  },
];

/** Bottom tab bar on phones: the screens people open every day. */
export const MOBILE_TABS: { to: string; label: string; icon: LucideIcon; needsEmployee?: boolean; approver?: boolean; end?: boolean }[] = [
  { to: '/dashboard', label: 'Home', icon: House },
  { to: '/me/attendance', label: 'Attendance', icon: CalendarClock, needsEmployee: true },
  { to: '/me/requests', label: 'Requests', icon: Send, needsEmployee: true },
  { to: '/approvals', label: 'Approvals', icon: Inbox, approver: true },
  { to: '/me/payslips', label: 'Pay', icon: Wallet, needsEmployee: true },
];

export function navFor(user: Me): NavSection[] {
  const role = effectiveRole(user)!;
  return NAV.map((s) => ({
    ...s,
    // Self-service needs an employee record (the owner inside a company has none).
    items: s.items.filter((i) => {
      if (i.needsEmployee && !user.employeeId) return false;
      const mods = user.modules;
      // HR users on a template only see its modules; others see a module's screens when their template includes it.
      if (i.roles.includes(role)) return !(mods && role === 'HrManager' && i.module && !mods.includes(i.module));
      return !!(mods && i.module && mods.includes(i.module));
    }),
  })).filter((s) => s.items.length > 0);
}

/** The access-template module a screen belongs to (longest matching menu path), for opening granted screens. */
export function moduleForPath(path: string): string | undefined {
  let best: NavItem | undefined;
  for (const item of NAV.flatMap((s) => s.items))
    if (item.module && (path === item.to || path.startsWith(item.to + '/')) && (!best || item.to.length > best.to.length)) best = item;
  return best?.module;
}
