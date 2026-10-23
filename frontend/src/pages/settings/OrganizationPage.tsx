import { useSearchParams } from 'react-router-dom';
import { Briefcase, CalendarHeart, Clock, GitBranch, Layers, ListPlus, MapPin, Palmtree } from 'lucide-react';
import { PageHeader, Tabs } from '../../components/ui';
import { ApprovalFlowsTab } from './ApprovalFlowsTab';
import { CustomFieldsTab } from './CustomFieldsTab';
import { DepartmentsTab } from './DepartmentsTab';
import { DesignationsTab } from './DesignationsTab';
import { HolidaysTab } from './HolidaysTab';
import { LeaveTypesTab } from './LeaveTypesTab';
import { OfficesTab } from './OfficesTab';
import { ShiftsTab } from './ShiftsTab';
import { ExemptionSettingsCard } from '../attendance/Exemptions';

type Tab = 'departments' | 'designations' | 'shifts' | 'offices' | 'holidays' | 'leave-types' | 'approvals' | 'custom-fields';
const TABS: Tab[] = ['departments', 'designations', 'shifts', 'offices', 'holidays', 'leave-types', 'approvals', 'custom-fields'];

export function OrganizationPage() {
  const [params, setParams] = useSearchParams();
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && TABS.includes(raw) ? raw : 'departments';

  return (
    <>
      <PageHeader title="Organization" description="Structure, working hours, locations, leave policy and who approves what." />
      <Tabs<Tab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        items={[
          { value: 'departments', label: 'Departments', icon: <Layers className="size-4" /> },
          { value: 'designations', label: 'Designations', icon: <Briefcase className="size-4" /> },
          { value: 'shifts', label: 'Shifts', icon: <Clock className="size-4" /> },
          { value: 'offices', label: 'Office locations', icon: <MapPin className="size-4" /> },
          { value: 'holidays', label: 'Holidays', icon: <CalendarHeart className="size-4" /> },
          { value: 'leave-types', label: 'Leave types', icon: <Palmtree className="size-4" /> },
          { value: 'approvals', label: 'Approval chains', icon: <GitBranch className="size-4" /> },
          { value: 'custom-fields', label: 'Custom fields', icon: <ListPlus className="size-4" /> },
        ]}
      />
      {tab === 'departments' && <DepartmentsTab />}
      {tab === 'designations' && <DesignationsTab />}
      {tab === 'shifts' && (
        <>
          <ShiftsTab />
          <ExemptionSettingsCard />
        </>
      )}
      {tab === 'offices' && <OfficesTab />}
      {tab === 'holidays' && <HolidaysTab />}
      {tab === 'leave-types' && <LeaveTypesTab />}
      {tab === 'approvals' && <ApprovalFlowsTab />}
      {tab === 'custom-fields' && <CustomFieldsTab />}
    </>
  );
}
