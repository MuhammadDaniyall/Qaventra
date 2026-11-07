import type { HTMLAttributes, ReactNode, TdHTMLAttributes, ThHTMLAttributes } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '../../lib/cn';
import { Button } from './Button';
import { tx } from '../../lib/i18n';

/** Horizontally scrollable table wrapper; lives inside a Card. */
export function Table({ children, className }: { children: ReactNode; className?: string }) {
  return (
    // relative: keeps absolutely positioned cell content (sr-only labels) inside the scroll box, so it can't widen the page.
    <div className="relative w-full overflow-x-auto overscroll-x-contain [scrollbar-width:thin]">
      <table className={cn('w-full min-w-max text-start text-sm', className)}>{children}</table>
    </div>
  );
}

export function THead({ children }: { children: ReactNode }) {
  return (
    <thead className="border-b border-zinc-200/80 bg-gradient-to-b from-zinc-50 to-zinc-50/40 text-[11px] font-semibold tracking-wide text-zinc-500 uppercase dark:border-white/[0.06] dark:from-white/[0.03] dark:to-transparent dark:text-zinc-400">
      {children}
    </thead>
  );
}

export function TBody({ children }: { children: ReactNode }) {
  return <tbody className="divide-y divide-zinc-100 dark:divide-white/[0.05]">{children}</tbody>;
}

export function Tr({
  className,
  onClick,
  ...rest
}: HTMLAttributes<HTMLTableRowElement> & { onClick?: () => void }) {
  return (
    <tr
      className={cn(
        'transition-colors hover:bg-accent-50/40 dark:hover:bg-white/[0.03]',
        onClick && 'cursor-pointer hover:bg-accent-50/70 focus-visible:bg-accent-50/70 focus-visible:outline-none dark:hover:bg-accent-500/[0.07] dark:focus-visible:bg-accent-500/[0.07]',
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

export function Th({ className, children, ...rest }: ThHTMLAttributes<HTMLTableCellElement>) {
  return (
    <th scope="col" className={cn('px-4 py-3 font-semibold whitespace-nowrap', className)} {...rest}>
      {tx(children)}
    </th>
  );
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
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-zinc-100/80 px-4 py-3 text-xs text-zinc-500 dark:border-white/[0.06] dark:text-zinc-400">
      <span className="tabular-nums">
        <span className="font-medium text-zinc-700 dark:text-zinc-200">
          {from}–{to}
        </span>{' '}
        of {total.toLocaleString()}
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
        <span className="rounded-lg bg-zinc-100 px-2.5 py-1 font-medium text-zinc-700 tabular-nums dark:bg-white/[0.06] dark:text-zinc-200">
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
