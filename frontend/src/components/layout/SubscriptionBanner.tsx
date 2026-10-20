import { useState } from 'react';
import { AlarmClock, X } from 'lucide-react';
import { isHr } from '../../auth/roles';
import { EXPIRING_DAYS } from '../../lib/billing';
import { formatDate, todayIso } from '../../lib/format';
import type { Me } from '../../types';

const KEY = 'hrms.subscriptionBanner.dismissed';

function daysUntil(iso: string): number {
  const [y, m, d] = iso.split('-').map(Number);
  const [ty, tm, td] = todayIso().split('-').map(Number);
  return Math.round((Date.UTC(y, m - 1, d) - Date.UTC(ty, tm - 1, td)) / 86_400_000);
}

function readDismissed(): string | null {
  try {
    return sessionStorage.getItem(KEY);
  } catch {
    return null;
  }
}

/** Company Super Admin / HR only (not employees, not the owner acting): subscription ends within 7 days. */
export function SubscriptionBanner({ user }: { user: Me }) {
  const end = user.subscriptionEndsOn;
  const [dismissed, setDismissed] = useState(readDismissed);
  if (!end || user.isActingAsOwner || !isHr(user)) return null;
  const days = daysUntil(end);
  if (days < 0 || days > EXPIRING_DAYS || dismissed === end) return null;

  const dismiss = () => {
    setDismissed(end);
    try {
      sessionStorage.setItem(KEY, end);
    } catch {
      // ignore
    }
  };

  return (
    <div
      role="status"
      className="flex items-center gap-3 border-b border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-900 sm:px-6 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-100"
    >
      <AlarmClock className="size-4 shrink-0" aria-hidden="true" />
      <p className="flex-1">
        Your subscription ends on <strong className="font-semibold">{formatDate(end)}</strong>. Contact your provider to renew.
      </p>
      <button
        type="button"
        onClick={dismiss}
        aria-label="Dismiss"
        className="rounded-md p-1 hover:bg-amber-100 focus-visible:outline-2 focus-visible:outline-amber-600 dark:hover:bg-amber-500/20"
      >
        <X className="size-4" />
      </button>
    </div>
  );
}
