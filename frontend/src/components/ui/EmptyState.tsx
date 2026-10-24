import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import { cn } from '../../lib/cn';
import { errorMessage } from '../../api/client';
import { Button } from './Button';
import { tx } from '../../lib/i18n';

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  className,
}: {
  icon: LucideIcon;
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('flex flex-col items-center justify-center px-6 py-14 text-center', className)}>
      <div className="mb-4 flex size-12 items-center justify-center rounded-full bg-accent-50 text-accent-600 ring-8 ring-accent-50/50 dark:bg-accent-500/10 dark:text-accent-300 dark:ring-accent-500/5">
        <Icon className="size-5" aria-hidden="true" />
      </div>
      <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">{tx(title)}</h3>
      {description && <p className="mt-1 max-w-sm text-sm text-zinc-500 dark:text-zinc-400">{tx(description)}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <EmptyState
      icon={AlertTriangle}
      title={tx("Couldn't load this")}
      description={errorMessage(error)}
      action={
        onRetry && (
          <Button variant="secondary" onClick={onRetry}>
            {tx('Try again')}
          </Button>
        )
      }
    />
  );
}
