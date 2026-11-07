import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ChevronRight, CircleCheck, Inbox, LayoutGrid } from 'lucide-react';
import { approvalsApi } from '../../api/approvals';
import { qk } from '../../api/keys';
import { navFor } from '../../components/layout/nav';
import { Avatar, Card, CardHeader, Skeleton } from '../../components/ui';
import { formatDateTimePk, humanize } from '../../lib/format';
import { cn } from '../../lib/cn';
import { tx, txf } from '../../lib/i18n';
import type { Me } from '../../types';

const linkCls = 'inline-flex items-center gap-0.5 text-sm font-medium text-accent-600 hover:underline dark:text-accent-400';

/** The newest requests waiting for this approver, each opening the approvals inbox. */
export function ApprovalsPreview({ className }: { className?: string }) {
  const inbox = useQuery({ queryKey: qk.approvalInbox, queryFn: approvalsApi.inbox });
  const all = inbox.data ?? [];
  const items = all.slice(0, 5);
  return (
    <Card className={cn('flex flex-col', className)}>
      <CardHeader
        title="Waiting for your approval"
        actions={
          <Link to="/approvals" className={linkCls}>
            {tx('See all')} <ChevronRight className="size-3.5" />
          </Link>
        }
      />
      {inbox.isLoading ? (
        <div className="p-5">
          <Skeleton className="h-28 w-full" />
        </div>
      ) : !items.length ? (
        <div className="flex items-center gap-3 px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">
          <CircleCheck className="size-5 text-emerald-500" aria-hidden="true" />
          {tx('Nothing waits for your approval.')}
        </div>
      ) : (
        <ul className="flex-1 divide-y divide-zinc-100 dark:divide-white/[0.05]">
          {items.map((i) => (
            <li key={`${i.requestType}-${i.subjectId}`}>
              <Link
                to="/approvals"
                className="group flex items-center gap-3 px-5 py-3 transition-colors hover:bg-indigo-50/50 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent-500 dark:hover:bg-white/[0.03]"
              >
                <Avatar name={i.employeeName} />
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{i.employeeName}</span>
                    <span className="shrink-0 rounded-md bg-indigo-50 px-1.5 py-0.5 text-[10px] font-semibold text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300">
                      {tx(humanize(i.requestType))}
                    </span>
                  </span>
                  <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">
                    {i.summary} · {formatDateTimePk(i.createdAtUtc)}
                  </span>
                </span>
                <ChevronRight className="size-4 shrink-0 text-zinc-300 transition-transform group-hover:translate-x-0.5 rtl:group-hover:-translate-x-0.5 dark:text-zinc-600" aria-hidden="true" />
              </Link>
            </li>
          ))}
        </ul>
      )}
      {/* Pinned to the bottom, so the card ends on the same line as its neighbour whatever its height. */}
      <Link
        to="/approvals"
        className="mt-auto flex items-center justify-between gap-2 border-t border-zinc-100 bg-zinc-50/60 px-5 py-3 text-sm font-medium text-accent-600 transition-colors hover:bg-indigo-50/60 dark:border-white/[0.06] dark:bg-white/[0.02] dark:text-accent-400 dark:hover:bg-white/[0.04]"
      >
        {txf('Open the approvals inbox ({n} waiting)', { n: all.length })}
        <ChevronRight className="size-4" aria-hidden="true" />
      </Link>
    </Card>
  );
}

/** Every module this user can open, grouped as in the sidebar: the whole product one click away from home. */
export function ModuleOverview({ user, className }: { user: Me; className?: string }) {
  const sections = navFor(user).filter((s) => s.title && s.items.length);
  if (!sections.length) return null;
  return (
    <section className={className} aria-labelledby="modules-title">
      <div className="mb-3 flex items-center gap-2">
        <LayoutGrid className="size-4 text-indigo-500" aria-hidden="true" />
        <h2 id="modules-title" className="text-base font-semibold text-zinc-900 dark:text-white">
          {tx('Everything in one place')}
        </h2>
        <span className="text-sm text-zinc-500 dark:text-zinc-400">· {tx('Every module your company uses, one click away.')}</span>
      </div>
      <div className="grid grid-flow-row-dense gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {sections.map((s) => {
          const Icon = s.icon ?? Inbox;
          return (
            <Card key={s.title} className={cn('h-full p-4', s.items.length > 10 && 'sm:col-span-2')}>
              <div className="mb-3 flex items-center gap-2.5">
                <span className="flex size-9 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-fuchsia-500 text-white shadow-md shadow-indigo-500/25 ring-1 ring-inset ring-white/20">
                  <Icon className="size-4" aria-hidden="true" />
                </span>
                <p className="font-semibold text-zinc-900 dark:text-white">{tx(s.title!)}</p>
                <span className="ms-auto text-xs text-zinc-400 tabular-nums">{s.items.length}</span>
              </div>
              <div className="flex flex-wrap gap-1.5">
                {s.items.map((item) => (
                  <Link
                    key={item.to}
                    to={item.to}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-zinc-200/80 bg-white/70 px-2.5 py-1.5 text-[13px] text-zinc-700 transition-colors hover:border-indigo-300 hover:bg-indigo-50 hover:text-indigo-700 focus-visible:outline-2 focus-visible:outline-accent-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-zinc-300 dark:hover:border-indigo-400/30 dark:hover:bg-indigo-500/10 dark:hover:text-indigo-200"
                  >
                    <item.icon className="size-3.5 shrink-0 text-indigo-400" aria-hidden="true" />
                    {tx(item.label)}
                  </Link>
                ))}
              </div>
            </Card>
          );
        })}
      </div>
    </section>
  );
}
