import { useSearchParams } from 'react-router-dom';
import { Settings2, CalendarHeart, Clock, Fingerprint, MapPin, Scale } from 'lucide-react';
import { PageHeader, Tabs } from '../../components/ui';
import { ExemptionSettingsCard } from '../attendance/Exemptions';
import { MachinesPage } from '../attendance/MachinesPage';
import { PenaltyRulesCard } from '../attendance/PenaltyRules';
import { HolidaysTab } from './HolidaysTab';
import { OfficesTab } from './OfficesTab';
import { ShiftsTab } from './ShiftsTab';

export type AttendanceSettingsTab = 'shifts' | 'rules' | 'locations' | 'holidays' | 'devices';
const TABS: AttendanceSettingsTab[] = ['shifts', 'rules', 'locations', 'holidays', 'devices'];

/** Everything HR sets up for attendance, in one place: working hours, late rules, check-in places, holidays and machines. */
export function AttendanceSettingsPage() {
  const [params, setParams] = useSearchParams();
  const raw = params.get('tab') as AttendanceSettingsTab | null;
  const tab: AttendanceSettingsTab = raw && TABS.includes(raw) ? raw : 'shifts';

  return (
    <>
      <PageHeader
        icon={Settings2}
        title="Attendance settings"
        description="Working hours, late and absence rules, where people may check in, holidays and attendance machines."
        back={{ to: '/attendance/daily', label: 'Attendance' }}
      />
      <Tabs<AttendanceSettingsTab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        items={[
          { value: 'shifts', label: 'Shifts', icon: <Clock className="size-4" /> },
          { value: 'rules', label: 'Late and penalty rules', icon: <Scale className="size-4" /> },
          { value: 'locations', label: 'Office locations', icon: <MapPin className="size-4" /> },
          { value: 'holidays', label: 'Holidays', icon: <CalendarHeart className="size-4" /> },
          { value: 'devices', label: 'Attendance machines', icon: <Fingerprint className="size-4" /> },
        ]}
      />
      {tab === 'shifts' && <ShiftsTab />}
      {tab === 'rules' && (
        <div className="-mt-5">
          <ExemptionSettingsCard />
          <PenaltyRulesCard />
        </div>
      )}
      {tab === 'locations' && <OfficesTab />}
      {tab === 'holidays' && <HolidaysTab />}
      {tab === 'devices' && <MachinesPage />}
    </>
  );
}
