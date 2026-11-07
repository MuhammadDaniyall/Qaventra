import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { cn } from '../../lib/cn';
import { Card } from './Card';
import { Skeleton } from './Spinner';
import { tx } from '../../lib/i18n';

/** Gradient icon tile and a matching soft glow in the corner. */
const iconTones = {
  accent: ['from-indigo-500 to-violet-500 shadow-indigo-500/30', 'bg-indigo-400/20'],
  green: ['from-emerald-500 to-teal-500 shadow-emerald-500/30', 'bg-emerald-400/20'],
  amber: ['from-amber-400 to-orange-500 shadow-amber-500/30', 'bg-amber-400/20'],
  red: ['from-rose-500 to-red-500 shadow-rose-500/30', 'bg-rose-400/20'],
  blue: ['from-sky-500 to-cyan-500 shadow-sky-500/30', 'bg-sky-400/20'],
  violet: ['from-violet-500 to-fuchsia-500 shadow-violet-500/30', 'bg-fuchsia-400/20'],
} as const;

export function StatCard({
  label,
  value,
  icon: Icon,
  tone = 'accent',
  hint,
  loading,
  to,
}: {
  label: string;
  value: ReactNode;
  icon: LucideIcon;
  tone?: keyof typeof iconTones;
  hint?: ReactNode;
  loading?: boolean;
  /** Makes the tile a link to the list behind the number. */
  to?: string;
}) {
  const card = (
    <Card className="group relative overflow-hidden p-4 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-[0_18px_40px_-20px_rgb(79_70_229/0.35)]">
      <div className={cn('pointer-events-none absolute -end-8 -top-10 size-28 rounded-full blur-2xl', iconTones[tone][1])} aria-hidden="true" />
      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-xs font-medium text-zinc-500 dark:text-zinc-400">{tx(label)}</p>
          {loading ? (
            <Skeleton className="mt-2 h-7 w-16" />
          ) : (
            <p className="mt-1 text-lg font-semibold tracking-tight whitespace-nowrap text-zinc-900 tabular-nums sm:text-2xl dark:text-white">
              {value}
            </p>
          )}
          {hint && <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">{tx(hint)}</p>}
        </div>
        <div className={cn('hidden size-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-white shadow-lg ring-1 ring-inset ring-white/20 sm:flex', iconTones[tone][0])}>
          <Icon className="size-4.5" aria-hidden="true" />
        </div>
      </div>
      {to && (
        <ChevronRight
          className="absolute end-3 bottom-3 size-4 text-zinc-300 transition-all group-hover:translate-x-0.5 group-hover:text-indigo-500 rtl:group-hover:-translate-x-0.5 dark:text-zinc-600"
          aria-hidden="true"
        />
      )}
    </Card>
  );
  return to ? (
    <Link to={to} className="block rounded-2xl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500">
      {card}
    </Link>
  ) : (
    card
  );
}
