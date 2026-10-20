import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import { cn } from '../../lib/cn';
import { errorMessage } from '../../api/client';
import { Button } from './Button';

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
      <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-zinc-100 text-zinc-500 ring-1 ring-zinc-200 dark:bg-zinc-800 dark:text-zinc-400 dark:ring-zinc-700">
        <Icon className="size-5" aria-hidden="true" />
      </div>
      <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">{title}</h3>
      {description && <p className="mt-1 max-w-sm text-sm text-zinc-500 dark:text-zinc-400">{description}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <EmptyState
      icon={AlertTriangle}
      title="Couldn't load this"
      description={errorMessage(error)}
      action={
        onRetry && (
          <Button variant="secondary" onClick={onRetry}>
            Try again
          </Button>
        )
      }
    />
  );
}
