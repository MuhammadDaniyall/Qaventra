import { Badge } from '../../components/ui';
import { accessKind, daysLeftLabel } from '../../lib/billing';
import { cn } from '../../lib/cn';
import { formatDate } from '../../lib/format';
import type { Company } from '../../types';

export function seatsLabel(c: Company) {
  return `${c.employees} / ${c.maxEmployees ?? '∞'}`;
}

/** Active (green), Expiring in N days (amber), Expired (red), Inactive (gray). */
export function AccessBadge({ company }: { company: Pick<Company, 'access' | 'daysLeft'> }) {
  const kind = accessKind(company);
  if (kind === 'inactive') return <Badge tone="gray" dot>Inactive</Badge>;
  if (kind === 'expired') return <Badge tone="red" dot>Expired</Badge>;
  if (kind === 'expiring') {
    const n = company.daysLeft ?? 0;
    return (
      <Badge tone="amber" dot>
        {n === 0 ? 'Expires today' : `Expiring in ${n} day${n === 1 ? '' : 's'}`}
      </Badge>
    );
  }
  return <Badge tone="green" dot>Active</Badge>;
}

export function ExpiresCell({ company }: { company: Company }) {
  if (!company.subscriptionEndsOn) return <span className="text-zinc-400">No expiry</span>;
  const kind = accessKind(company);
  return (
    <div>
      <p className="text-zinc-900 dark:text-zinc-100">{formatDate(company.subscriptionEndsOn)}</p>
      <p
        className={cn(
          'text-xs',
          company.daysLeft != null && company.daysLeft < 0
            ? 'text-red-600 dark:text-red-400'
            : kind === 'expiring'
              ? 'text-amber-700 dark:text-amber-300'
              : 'text-zinc-500 dark:text-zinc-400',
        )}
      >
        {daysLeftLabel(company)}
      </p>
    </div>
  );
}
