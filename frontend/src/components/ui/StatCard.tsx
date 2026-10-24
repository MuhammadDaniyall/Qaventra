import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';
import { Card } from './Card';
import { Skeleton } from './Spinner';
import { tx } from '../../lib/i18n';

const iconTones = {
  accent: 'bg-accent-50 text-accent-600 dark:bg-accent-500/10 dark:text-accent-300',
  green: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-300',
  amber: 'bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-300',
  red: 'bg-red-50 text-red-600 dark:bg-red-500/10 dark:text-red-300',
  blue: 'bg-sky-50 text-sky-600 dark:bg-sky-500/10 dark:text-sky-300',
  violet: 'bg-violet-50 text-violet-600 dark:bg-violet-500/10 dark:text-violet-300',
};

export function StatCard({
  label,
  value,
  icon: Icon,
  tone = 'accent',
  hint,
  loading,
}: {
  label: string;
  value: ReactNode;
  icon: LucideIcon;
  tone?: keyof typeof iconTones;
  hint?: ReactNode;
  loading?: boolean;
}) {
  return (
    <Card className="p-4">
      <div className="flex items-start justify-between gap-3">
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
        <div className={cn('hidden size-9 shrink-0 items-center justify-center rounded-lg sm:flex', iconTones[tone])}>
          <Icon className="size-4.5" aria-hidden="true" />
        </div>
      </div>
    </Card>
  );
}
