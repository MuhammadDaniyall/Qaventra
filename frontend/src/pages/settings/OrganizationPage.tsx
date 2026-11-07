import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { Settings2, Briefcase, GitBranch, Layers, FolderOpen, ListPlus, Mail, ShieldCheck, Tags } from 'lucide-react';
import { useUser } from '../../auth/AuthContext';
import { isCompanyAdmin } from '../../auth/roles';
import { EmailTab } from './EmailTab';
import { DocumentsTab } from './DocumentsTab';
import { SecurityTab } from './SecurityTab';
import { PageHeader, Tabs } from '../../components/ui';
import { navFor } from '../../components/layout/nav';
import { tx } from '../../lib/i18n';
import { ApprovalFlowsTab } from './ApprovalFlowsTab';
import { CustomFieldsTab } from './CustomFieldsTab';
import { DepartmentsTab } from './DepartmentsTab';
import { DesignationsTab } from './DesignationsTab';
import { LookupsTab } from './LookupsTab';

type Tab = 'departments' | 'designations' | 'lists' | 'approvals' | 'custom-fields' | 'email' | 'documents' | 'security';
const TABS: Tab[] = ['departments', 'designations', 'lists', 'approvals', 'custom-fields', 'email', 'documents', 'security'];

/** Tabs that moved into their module's own settings; old links and bookmarks land there. */
const MOVED: Record<string, string> = {
  shifts: '/attendance/settings?tab=shifts',
  offices: '/attendance/settings?tab=locations',
  holidays: '/attendance/settings?tab=holidays',
  'leave-types': '/leave/settings?tab=types',
};

/** Shortcuts to each module's own settings; `nav` is the menu entry that shows the module is in this company's package. */
const MODULE_SETTINGS: { label: string; to: string; nav: string }[] = [
  { label: 'Attendance', to: '/attendance/settings', nav: '/attendance/daily' },
  { label: 'Leave', to: '/leave/settings', nav: '/leave/approvals' },
  { label: 'Payroll', to: '/payroll/settings', nav: '/payroll' },
  { label: 'Expenses', to: '/expenses/setup', nav: '/expenses/approvals' },
  { label: 'Performance', to: '/performance?tab=settings', nav: '/performance' },
  { label: 'Recruitment', to: '/recruitment?tab=templates', nav: '/recruitment' },
  { label: 'Help desk', to: '/helpdesk/queue?tab=categories', nav: '/helpdesk/queue' },
  { label: 'Assets', to: '/assets?tab=types', nav: '/assets' },
  { label: 'Onboarding', to: '/onboarding?tab=templates', nav: '/onboarding' },
  { label: 'Separations', to: '/separations/setup', nav: '/separations' },
  { label: 'Travel', to: '/travel?tab=settings', nav: '/travel' },
];

export function OrganizationPage() {
  const user = useUser();
  const inMenu = new Set(navFor(user).flatMap((sec) => sec.items.map((i) => i.to)));
  const shortcuts = MODULE_SETTINGS.filter((m) => inMenu.has(m.nav));
  const [params, setParams] = useSearchParams();
  const admin = isCompanyAdmin(user);
  const raw = params.get('tab') as Tab | null;
  const moved = raw ? MOVED[raw] : undefined;
  const tab: Tab = raw && TABS.includes(raw) && (raw !== 'security' || admin) ? raw : 'departments';

  if (moved) return <Navigate to={moved} replace />;

  return (
    <>
      <PageHeader
        title="Organization"
        description="Company structure, lists, approval chains, email and documents. Module settings (attendance, leave, payroll…) are inside each module."
      />
      {shortcuts.length > 0 && (
        <div className="mb-6 flex flex-wrap items-center gap-2">
          <span className="me-1 inline-flex items-center gap-1.5 text-sm font-medium text-zinc-500 dark:text-zinc-400">
            <Settings2 className="size-4" aria-hidden="true" />
            {tx('Module settings')}:
          </span>
          {shortcuts.map((m) => (
            <Link
              key={m.to}
              to={m.to}
              className="rounded-full border border-zinc-200 bg-white px-3 py-1 text-sm text-zinc-700 transition hover:border-indigo-300 hover:text-indigo-600 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:border-indigo-500 dark:hover:text-indigo-400"
            >
              {tx(m.label)}
            </Link>
          ))}
        </div>
      )}
      <Tabs<Tab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        items={[
          { value: 'departments', label: 'Departments', icon: <Layers className="size-4" /> },
          { value: 'designations', label: 'Designations', icon: <Briefcase className="size-4" /> },
          { value: 'lists', label: 'Grades and groups', icon: <Tags className="size-4" /> },
          { value: 'approvals', label: 'Approval chains', icon: <GitBranch className="size-4" /> },
          { value: 'custom-fields', label: 'Custom fields', icon: <ListPlus className="size-4" /> },
          { value: 'email', label: 'Email', icon: <Mail className="size-4" /> },
          { value: 'documents', label: 'Documents', icon: <FolderOpen className="size-4" /> },
          ...(admin ? [{ value: 'security' as const, label: 'Security', icon: <ShieldCheck className="size-4" /> }] : []),
        ]}
      />
      {tab === 'departments' && <DepartmentsTab />}
      {tab === 'designations' && <DesignationsTab />}
      {tab === 'lists' && <LookupsTab />}
      {tab === 'approvals' && <ApprovalFlowsTab />}
      {tab === 'custom-fields' && <CustomFieldsTab />}
      {tab === 'email' && <EmailTab />}
      {tab === 'documents' && <DocumentsTab />}
      {tab === 'security' && <SecurityTab />}
    </>
  );
}
