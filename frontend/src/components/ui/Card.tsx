import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

export function Card({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-zinc-200/70 bg-white/90 shadow-[0_1px_2px_rgb(16_24_40/0.04),0_12px_32px_-18px_rgb(79_70_229/0.18)]',
        'dark:border-white/[0.07] dark:bg-zinc-900/75 dark:shadow-[0_1px_0_rgb(255_255_255/0.04)_inset,0_16px_40px_-24px_rgb(0_0_0/0.6)]',
        className,
      )}
      {...rest}
    />
  );
}

export function CardHeader({
  title,
  description,
  actions,
  className,
}: {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        'flex flex-wrap items-start justify-between gap-3 border-b border-zinc-100/80 px-5 py-4 dark:border-white/[0.06]',
        className,
      )}
    >
      <div className="min-w-0">
        <h2 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">{tx(title)}</h2>
        {description && <p className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">{tx(description)}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

export function CardBody({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('p-5', className)} {...rest} />;
}
