import { useNavigate, useSearchParams } from 'react-router-dom';
import { Settings2, CalendarHeart, Palmtree, Scale } from 'lucide-react';
import { Button, PageHeader, Tabs } from '../../components/ui';
import { HolidaysTab } from './HolidaysTab';
import { LeaveTypesTab } from './LeaveTypesTab';

export type LeaveSettingsTab = 'types' | 'holidays';
const TABS: LeaveSettingsTab[] = ['types', 'holidays'];

/** Everything HR sets up for leave: leave types with their quotas and reasons, and the holidays leave skips. */
export function LeaveSettingsPage() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const raw = params.get('tab') as LeaveSettingsTab | null;
  const tab: LeaveSettingsTab = raw && TABS.includes(raw) ? raw : 'types';

  return (
    <>
      <PageHeader
        icon={Settings2}
        title="Leave settings"
        description="Leave types, yearly quotas, carry forward and reasons, and the holidays leave does not count."
        back={{ to: '/leave/approvals', label: 'Leave' }}
        actions={
          <Button variant="secondary" icon={<Scale className="size-4" />} onClick={() => navigate('/leave/opening-balances')}>
            Opening balances
          </Button>
        }
      />
      <Tabs<LeaveSettingsTab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        items={[
          { value: 'types', label: 'Leave types', icon: <Palmtree className="size-4" /> },
          { value: 'holidays', label: 'Holidays', icon: <CalendarHeart className="size-4" /> },
        ]}
      />
      {tab === 'types' && <LeaveTypesTab />}
      {tab === 'holidays' && <HolidaysTab />}
    </>
  );
}
