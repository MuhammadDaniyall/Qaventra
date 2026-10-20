import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft } from 'lucide-react';

export function PageHeader({
  title,
  description,
  actions,
  back,
}: {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  back?: { to: string; label: string };
}) {
  return (
    <div className="mb-6">
      {back && (
        <Link
          to={back.to}
          className="mb-2 inline-flex items-center gap-1 rounded text-xs font-medium text-zinc-500 hover:text-zinc-900 focus-visible:outline-2 focus-visible:outline-accent-500 dark:text-zinc-400 dark:hover:text-white"
        >
          <ChevronLeft className="size-3.5" />
          {back.label}
        </Link>
      )}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-xl font-semibold tracking-tight text-zinc-900 sm:text-2xl dark:text-white">{title}</h1>
          {description && <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}
