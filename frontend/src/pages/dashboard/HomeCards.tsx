import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  CalendarHeart,
  ChevronRight,
  CircleCheck,
  ClipboardCheck,
  Clock,
  Inbox,
  Palmtree,
  Receipt,
  UserPlus,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { expensesApi } from '../../api/expenses';
import { leaveApi } from '../../api/leave';
import { qk } from '../../api/keys';
import { Card, CardHeader, EmptyState, Skeleton, StatusBadge } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatNumber, monthLabel } from '../../lib/format';
import type { Dashboard, Holiday } from '../../types';

const linkCls = 'text-sm font-medium text-accent-600 hover:underline dark:text-accent-400';

/** Remaining days per leave type, as bars. */
export function LeaveBalanceCard({ className }: { className?: string }) {
  const balances = useQuery({ queryKey: qk.leaveBalances, queryFn: () => leaveApi.balances() });
  const rows = (balances.data ?? []).filter((b) => b.entitled > 0 || b.available > 0).slice(0, 4);

  return (
    <Card className={className}>
      <CardHeader title="Leave balance" actions={<Link to="/me/leave" className={linkCls}>Apply</Link>} />
      <div className="space-y-4 p-5">
        {balances.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : !rows.length ? (
          <p className="text-sm text-zinc-500 dark:text-zinc-400">No leave types assigned yet.</p>
        ) : (
          rows.map((b) => {
            const total = Math.max(b.earned + b.carriedForward + b.adjustment + b.credited, b.available, 1);
            const pct = Math.min(100, Math.round((b.available / total) * 100));
            return (
              <div key={b.leaveTypeId}>
                <div className="flex items-baseline justify-between gap-2 text-sm">
                  <span className="truncate text-zinc-600 dark:text-zinc-300">{b.leaveType}</span>
                  <span className="font-medium text-zinc-900 tabular-nums dark:text-white">
                    {formatNumber(b.available)}
                    <span className="font-normal text-zinc-400"> / {formatNumber(total)}</span>
                  </span>
                </div>
                <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                  <div className="h-full rounded-full bg-accent-500" style={{ width: `${pct}%` }} />
                </div>
              </div>
            );
          })
        )}
      </div>
    </Card>
  );
}

/** The signed-in employee's latest leave and expense requests with their status. */
export function MyRequestsCard({ className }: { className?: string }) {
  const leave = useQuery({ queryKey: qk.leaveMy, queryFn: leaveApi.my });
  const expenses = useQuery({ queryKey: qk.myExpenses, queryFn: expensesApi.my });
  const loading = leave.isLoading || expenses.isLoading;

  const items = [
    ...(leave.data ?? []).map((l) => ({
      key: `l-${l.id}`,
      icon: Palmtree,
      title: l.leaveType,
      detail: l.fromDate === l.toDate ? formatDate(l.fromDate) : `${formatDate(l.fromDate)} – ${formatDate(l.toDate)}`,
      status: l.status,
      at: l.createdAtUtc,
      to: '/me/leave',
    })),
    ...(expenses.data ?? []).map((e) => ({
      key: `e-${e.id}`,
      icon: Receipt,
      title: e.title,
      detail: formatCurrency(e.total),
      status: e.status,
      at: e.createdAtUtc,
      to: `/me/expenses`,
    })),
  ]
    .sort((a, b) => b.at.localeCompare(a.at))
    .slice(0, 5);

  return (
    <Card className={className}>
      <CardHeader title="Your requests" description="Latest leave and expense requests" actions={<Link to="/me/requests" className={linkCls}>See all</Link>} />
      {loading ? (
        <div className="p-5">
          <Skeleton className="h-24 w-full" />
        </div>
      ) : !items.length ? (
        <EmptyState
          icon={Inbox}
          title="No requests yet"
          description="Leave and expense requests you make show up here."
          className="py-8"
          action={<Link to="/me/leave" className={linkCls}>Apply for leave</Link>}
        />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {items.map((i) => (
            <li key={i.key}>
              <Link
                to={i.to}
                className="flex items-center gap-3 px-5 py-3 transition-colors hover:bg-zinc-50 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:bg-zinc-800/50"
              >
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
                  <i.icon className="size-4" aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{i.title}</span>
                  <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">{i.detail}</span>
                </span>
                <StatusBadge status={i.status} />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

interface Task {
  icon: LucideIcon;
  tone: 'amber' | 'violet' | 'accent' | 'green';
  title: string;
  detail: string;
  to: string;
}

const taskTones = {
  amber: 'bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-300',
  violet: 'bg-violet-50 text-violet-600 dark:bg-violet-500/10 dark:text-violet-300',
  accent: 'bg-accent-50 text-accent-600 dark:bg-accent-500/10 dark:text-accent-300',
  green: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-300',
};

/** What needs attention today, built from the dashboard numbers. */
export function ActionCenter({ d, hr, className }: { d: Dashboard | undefined; hr: boolean; className?: string }) {
  const tasks: Task[] = [];
  if (d) {
    if (d.pendingApprovals > 0)
      tasks.push({
        icon: ClipboardCheck,
        tone: 'violet',
        title: `${d.pendingApprovals} ${d.pendingApprovals === 1 ? 'request waits' : 'requests wait'} for you`,
        detail: 'Leave, expenses and other approvals',
        to: '/approvals',
      });
    if (d.lateToday > 0)
      tasks.push({
        icon: Clock,
        tone: 'amber',
        title: `${d.lateToday} late today`,
        detail: 'See who came in after shift start',
        to: '/attendance/daily',
      });
    if (hr && !d.lastPayroll)
      tasks.push({ icon: Wallet, tone: 'accent', title: 'Run your first payroll', detail: 'Once salaries are set up', to: '/payroll' });
    else if (hr && d.lastPayroll && d.lastPayroll.status !== 'Approved')
      tasks.push({
        icon: Wallet,
        tone: 'accent',
        title: `Finish ${monthLabel(d.lastPayroll.year, d.lastPayroll.month)} payroll`,
        detail: `Currently ${d.lastPayroll.status.toLowerCase()}`,
        to: `/payroll/runs/${d.lastPayroll.id}`,
      });
    if (hr && d.newJoinersThisMonth > 0)
      tasks.push({
        icon: UserPlus,
        tone: 'green',
        title: `${d.newJoinersThisMonth} new ${d.newJoinersThisMonth === 1 ? 'joiner' : 'joiners'} this month`,
        detail: 'Check their onboarding',
        to: '/onboarding',
      });
  }

  return (
    <Card className={className}>
      <CardHeader title="Needs your attention" />
      {!d ? (
        <div className="p-5">
          <Skeleton className="h-24 w-full" />
        </div>
      ) : !tasks.length ? (
        <div className="flex items-center gap-3 px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">
          <CircleCheck className="size-5 text-emerald-500" aria-hidden="true" />
          All clear. Nothing waits for you right now.
        </div>
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {tasks.map((t) => (
            <li key={t.title}>
              <Link
                to={t.to}
                className="group flex items-center gap-3 px-5 py-3 transition-colors hover:bg-zinc-50 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:bg-zinc-800/50"
              >
                <span className={cn('flex size-8 shrink-0 items-center justify-center rounded-lg', taskTones[t.tone])}>
                  <t.icon className="size-4" aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{t.title}</span>
                  <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">{t.detail}</span>
                </span>
                <ChevronRight className="size-4 text-zinc-300 transition-transform group-hover:translate-x-0.5 dark:text-zinc-600" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

export function HolidaysCard({ holidays, loading, className }: { holidays: Holiday[] | undefined; loading: boolean; className?: string }) {
  return (
    <Card className={className}>
      <CardHeader title="Upcoming holidays" />
      {loading ? (
        <div className="p-5">
          <Skeleton className="h-24 w-full" />
        </div>
      ) : !holidays?.length ? (
        <EmptyState icon={CalendarHeart} title="No upcoming holidays" className="py-8" />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {holidays.map((h) => (
            <li key={h.id} className="flex items-center gap-3 px-5 py-3">
              <div className="flex size-10 shrink-0 flex-col items-center justify-center rounded-lg bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300">
                <span className="text-[10px] leading-none font-semibold uppercase">{formatDate(h.date).split(' ')[1]}</span>
                <span className="text-sm leading-tight font-semibold">{formatDate(h.date).split(' ')[0]}</span>
              </div>
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{h.name}</p>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">{formatDate(h.date)}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
