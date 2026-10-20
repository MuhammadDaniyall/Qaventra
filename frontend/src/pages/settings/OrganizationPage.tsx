import { useSearchParams } from 'react-router-dom';
import { Briefcase, CalendarHeart, Clock, Layers, MapPin, Palmtree } from 'lucide-react';
import { PageHeader, Tabs } from '../../components/ui';
import { DepartmentsTab } from './DepartmentsTab';
import { DesignationsTab } from './DesignationsTab';
import { HolidaysTab } from './HolidaysTab';
import { LeaveTypesTab } from './LeaveTypesTab';
import { OfficesTab } from './OfficesTab';
import { ShiftsTab } from './ShiftsTab';

type Tab = 'departments' | 'designations' | 'shifts' | 'offices' | 'holidays' | 'leave-types';
const TABS: Tab[] = ['departments', 'designations', 'shifts', 'offices', 'holidays', 'leave-types'];

export function OrganizationPage() {
  const [params, setParams] = useSearchParams();
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && TABS.includes(raw) ? raw : 'departments';

  return (
    <>
      <PageHeader title="Organization" description="Structure, working hours, locations and leave policy." />
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
        ]}
      />
      {tab === 'departments' && <DepartmentsTab />}
      {tab === 'designations' && <DesignationsTab />}
      {tab === 'shifts' && <ShiftsTab />}
      {tab === 'offices' && <OfficesTab />}
      {tab === 'holidays' && <HolidaysTab />}
      {tab === 'leave-types' && <LeaveTypesTab />}
    </>
  );
}
