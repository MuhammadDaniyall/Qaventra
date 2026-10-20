import type { BillingCycle, Company } from '../types';
import { formatCurrency, toIsoDate } from './format';

export const CYCLE_MONTHS: Record<BillingCycle, number> = { Monthly: 1, Quarterly: 3, HalfYearly: 6, Yearly: 12 };
export const CYCLE_LABELS: Record<BillingCycle, string> = {
  Monthly: 'Monthly',
  Quarterly: 'Quarterly',
  HalfYearly: 'Half-yearly',
  Yearly: 'Yearly',
};

export const PAYMENT_METHODS = ['Cash', 'Bank transfer', 'JazzCash', 'Easypaisa', 'Cheque', 'Other'];

/** Days within which a subscription counts as "expiring soon". */
export const EXPIRING_DAYS = 7;

function parse(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function addDays(iso: string, days: number): string {
  const d = parse(iso);
  d.setDate(d.getDate() + days);
  return toIsoDate(d);
}

/** Adds months like .NET DateOnly.AddMonths: the day is clamped to the end of the target month. */
export function addMonths(iso: string, months: number): string {
  const [y, m, d] = iso.split('-').map(Number);
  const target = new Date(y, m - 1 + months, 1);
  const last = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
  target.setDate(Math.min(d, last));
  return toIsoDate(target);
}

/** Same formula as the backend: base fee + seats x rate x months. Seats = licensed, or headcount if unlimited. */
export function billPerCycle(opts: {
  cycle: BillingCycle | null;
  basePrice: number | null;
  pricePerEmployee: number | null;
  seats: number | null;
  headcount: number;
}): number | null {
  if (!opts.cycle) return null;
  const seats = opts.seats ?? opts.headcount;
  return (opts.basePrice ?? 0) + (opts.pricePerEmployee ?? 0) * seats * CYCLE_MONTHS[opts.cycle];
}

/** "Starts today for one cycle": today + cycle months - 1 day. */
export function oneCycleFrom(today: string, cycle: BillingCycle): string {
  return addDays(addMonths(today, CYCLE_MONTHS[cycle]), -1);
}

/** Mirrors the backend: extend from the day after the current end, or from today if lapsed / never started. */
export function extendSubscription(today: string, currentEnd: string | null, cycle: BillingCycle, periods: number) {
  const from = currentEnd && currentEnd >= today ? addDays(currentEnd, 1) : today;
  const to = addDays(addMonths(from, CYCLE_MONTHS[cycle] * periods), -1);
  return { from, to };
}

export type AccessKind = 'active' | 'expiring' | 'expired' | 'inactive';

export function accessKind(c: Pick<Company, 'access' | 'daysLeft'>): AccessKind {
  if (c.access === 'Inactive') return 'inactive';
  if (c.access === 'Expired') return 'expired';
  if (c.daysLeft != null && c.daysLeft >= 0 && c.daysLeft <= EXPIRING_DAYS) return 'expiring';
  return 'active';
}

export function daysLeftLabel(c: Pick<Company, 'subscriptionEndsOn' | 'daysLeft'>): string {
  if (!c.subscriptionEndsOn || c.daysLeft == null) return 'No expiry';
  if (c.daysLeft < 0) {
    const n = -c.daysLeft;
    return `expired ${n} day${n === 1 ? '' : 's'} ago`;
  }
  if (c.daysLeft === 0) return 'last day today';
  return `${c.daysLeft} day${c.daysLeft === 1 ? '' : 's'} left`;
}

export function planLabel(c: Pick<Company, 'billingCycle' | 'amountPerCycle'>): string {
  if (!c.billingCycle) return 'No plan';
  return `${CYCLE_LABELS[c.billingCycle]} · ${formatCurrency(c.amountPerCycle ?? 0)}`;
}
