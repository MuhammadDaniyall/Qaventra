import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';
import { humanize } from '../../lib/format';

export type Tone = 'green' | 'amber' | 'red' | 'gray' | 'blue' | 'accent' | 'violet';

const tones: Record<Tone, string> = {
  green: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-400/20',
  amber: 'bg-amber-50 text-amber-800 ring-amber-600/20 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-400/20',
  red: 'bg-red-50 text-red-700 ring-red-600/20 dark:bg-red-500/10 dark:text-red-300 dark:ring-red-400/20',
  gray: 'bg-zinc-100 text-zinc-600 ring-zinc-500/20 dark:bg-zinc-500/10 dark:text-zinc-300 dark:ring-zinc-400/20',
  blue: 'bg-sky-50 text-sky-700 ring-sky-600/20 dark:bg-sky-500/10 dark:text-sky-300 dark:ring-sky-400/20',
  accent: 'bg-accent-50 text-accent-700 ring-accent-600/20 dark:bg-accent-500/10 dark:text-accent-300 dark:ring-accent-400/20',
  violet: 'bg-violet-50 text-violet-700 ring-violet-600/20 dark:bg-violet-500/10 dark:text-violet-300 dark:ring-violet-400/20',
};

const dots: Record<Tone, string> = {
  green: 'bg-emerald-500',
  amber: 'bg-amber-500',
  red: 'bg-red-500',
  gray: 'bg-zinc-400',
  blue: 'bg-sky-500',
  accent: 'bg-accent-500',
  violet: 'bg-violet-500',
};

export function Badge({
  tone = 'gray',
  dot,
  children,
  className,
}: {
  tone?: Tone;
  dot?: boolean;
  children: ReactNode;
  className?: string;
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset',
        tones[tone],
        className,
      )}
    >
      {dot && <span className={cn('size-1.5 rounded-full', dots[tone])} aria-hidden="true" />}
      {children}
    </span>
  );
}

const STATUS_TONES: Record<string, Tone> = {
  // attendance
  Present: 'green',
  Late: 'amber',
  HalfDay: 'amber',
  ShortDay: 'amber',
  Absent: 'red',
  OnLeave: 'blue',
  // leave
  Approved: 'green',
  Pending: 'amber',
  Rejected: 'red',
  Cancelled: 'gray',
  AwaitingColleague: 'blue',
  Declined: 'red',
  // employment
  Active: 'green',
  Probation: 'amber',
  Resigned: 'gray',
  Terminated: 'red',
  // payroll
  Draft: 'amber',
  Finalized: 'green',
  // expenses
  Paid: 'violet',
  // separation
  Requested: 'amber',
  InProgress: 'blue',
  Completed: 'green',
  Withdrawn: 'gray',
  // audit
  Auth: 'violet',
  Request: 'blue',
  Data: 'accent',
};

/** Badge colored by the meaning of a status enum value. */
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  return (
    <Badge tone={STATUS_TONES[status] ?? 'gray'} dot>
      {label ?? humanize(status)}
    </Badge>
  );
}
