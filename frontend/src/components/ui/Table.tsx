import type { HTMLAttributes, ReactNode, TdHTMLAttributes, ThHTMLAttributes } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '../../lib/cn';
import { Button } from './Button';

/** Horizontally scrollable table wrapper; lives inside a Card. */
export function Table({ children, className }: { children: ReactNode; className?: string }) {
  return (
    // relative: keeps absolutely positioned cell content (sr-only labels) inside the scroll box, so it can't widen the page.
    <div className="relative w-full overflow-x-auto overscroll-x-contain [scrollbar-width:thin]">
      <table className={cn('w-full min-w-max text-left text-sm', className)}>{children}</table>
    </div>
  );
}

export function THead({ children }: { children: ReactNode }) {
  return (
    <thead className="border-b border-zinc-200 bg-zinc-50/70 text-[11px] font-semibold tracking-wide text-zinc-500 uppercase dark:border-zinc-800 dark:bg-zinc-900/60 dark:text-zinc-400">
      {children}
    </thead>
  );
}

export function TBody({ children }: { children: ReactNode }) {
  return <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">{children}</tbody>;
}

export function Tr({
  className,
  onClick,
  ...rest
}: HTMLAttributes<HTMLTableRowElement> & { onClick?: () => void }) {
  return (
    <tr
      className={cn(
        'transition-colors hover:bg-zinc-50/60 dark:hover:bg-zinc-800/30',
        onClick && 'cursor-pointer hover:bg-zinc-50 focus-visible:bg-zinc-50 focus-visible:outline-none dark:hover:bg-zinc-800/50 dark:focus-visible:bg-zinc-800/50',
        className,
      )}
      onClick={onClick}
      tabIndex={onClick ? 0 : undefined}
      onKeyDown={
        onClick
          ? (e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
      {...rest}
    />
  );
}

export function Th({ className, ...rest }: ThHTMLAttributes<HTMLTableCellElement>) {
  return <th scope="col" className={cn('px-4 py-2.5 font-medium whitespace-nowrap', className)} {...rest} />;
}

export function Td({ className, ...rest }: TdHTMLAttributes<HTMLTableCellElement>) {
  return <td className={cn('px-4 py-3 whitespace-nowrap text-zinc-700 dark:text-zinc-300', className)} {...rest} />;
}

export function Pagination({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-zinc-100 px-4 py-3 text-xs text-zinc-500 dark:border-zinc-800 dark:text-zinc-400">
      <span>
        {from}–{to} of {total.toLocaleString()}
      </span>
      <div className="flex items-center gap-2">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => onPage(page - 1)}
          disabled={page <= 1}
          icon={<ChevronLeft className="size-3.5" />}
        >
          Previous
        </Button>
        <span className="tabular-nums">
          Page {page} of {pages}
        </span>
        <Button variant="secondary" size="sm" onClick={() => onPage(page + 1)} disabled={page >= pages}>
          Next
          <ChevronRight className="size-3.5" />
        </Button>
      </div>
    </div>
  );
}
