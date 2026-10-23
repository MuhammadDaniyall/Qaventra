import type { SettlementStatus, TravelStatus } from '../../api/travel';
import { SETTLEMENT_LABELS } from '../../api/travel';
import { Badge, StatusBadge } from '../../components/ui';

/** The trip's status, or where its settlement is once it has one. */
export function TripStatusBadge({ status, settlement }: { status: TravelStatus; settlement: SettlementStatus }) {
  if ((status === 'Approved' || status === 'Booked') && settlement !== 'None')
    return (
      <Badge tone={settlement === 'Settled' ? 'green' : settlement === 'Returned' ? 'red' : 'amber'} dot>
        {SETTLEMENT_LABELS[settlement]}
      </Badge>
    );
  if (status === 'Booked')
    return (
      <Badge tone="blue" dot>
        Booked
      </Badge>
    );
  return <StatusBadge status={status} />;
}
