import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  Banknote,
  CalendarDays,
  CalendarPlus,
  ChevronDown,
  Clock,
  FileSignature,
  Inbox,
  Plus,
  Receipt,
  type LucideIcon,
} from 'lucide-react';
import type { ApprovalRequestType } from '../../api/approvals';
import { attendanceRequestsApi } from '../../api/attendance';
import { expensesApi } from '../../api/expenses';
import { qk } from '../../api/keys';
import { compOffApi, leaveApi } from '../../api/leave';
import { lettersApi } from '../../api/letters';
import { payApi } from '../../api/pay';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { Button, Card, EmptyState, PageHeader, SkeletonRows, StatusBadge } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatDateTimeUtc, humanize } from '../../lib/format';

/** Where each kind of request is made. */
const NEW_REQUESTS: { label: string; hint: string; icon: LucideIcon; to: string }[] = [
  { label: 'Leave', hint: 'Annual, casual, sick and more', icon: CalendarDays, to: '/me/leave' },
  { label: 'Comp-off', hint: 'For a holiday or off day you worked', icon: CalendarPlus, to: '/me/leave' },
  { label: 'Attendance correction', hint: 'Missed punch, work from home, duty', icon: Clock, to: '/me/attendance' },
  { label: 'Expense claim', hint: 'Fuel, travel, meals with receipts', icon: Receipt, to: '/me/expenses' },
  { label: 'Pay or loan', hint: 'Advance, loan or other pay request', icon: Banknote, to: '/me/pay' },
  { label: 'Letter', hint: 'Salary certificate, experience letter', icon: FileSignature, to: '/me/documents' },
];

type Status = 'all' | 'open' | 'closed';

interface Row {
  key: string;
  type: ApprovalRequestType;
  subjectId: string;
  icon: LucideIcon;
  title: string;
  detail: string;
  status: string;
  at: string;
  to: string;
}

const OPEN = ['Pending', 'AwaitingColleague'];

/** Every request the employee has made, newest first, with where it stands in the approval chain. */
export function MyRequestsPage() {
  const [status, setStatus] = useState<Status>('open');
  const [open, setOpen] = useState<string | null>(null);

  const leave = useQuery({ queryKey: qk.leaveMy, queryFn: leaveApi.my });
  const compOff = useQuery({ queryKey: qk.compOffMy, queryFn: compOffApi.my });
  const attendance = useQuery({ queryKey: qk.attendanceRequestsMy, queryFn: attendanceRequestsApi.my });
  const expenses = useQuery({ queryKey: qk.myExpenses, queryFn: expensesApi.my });
  const pay = useQuery({ queryKey: qk.payMy, queryFn: payApi.myRequests });
  const letters = useQuery({ queryKey: ['letters', 'requests', 'mine'], queryFn: lettersApi.myRequests });
  const loading = [leave, compOff, attendance, expenses, pay, letters].some((q) => q.isLoading);

  const rows: Row[] = [
    ...(leave.data ?? []).map((l) => ({
      key: `l-${l.id}`, type: 'Leave' as const, subjectId: l.id, icon: CalendarDays, title: l.leaveType,
      detail: `${l.fromDate === l.toDate ? formatDate(l.fromDate) : `${formatDate(l.fromDate)} – ${formatDate(l.toDate)}`} · ${l.days} ${l.days === 1 ? 'day' : 'days'}`,
      status: l.status, at: l.createdAtUtc, to: '/me/leave',
    })),
    ...(compOff.data ?? []).map((c) => ({
      key: `c-${c.id}`, type: 'CompOff' as const, subjectId: c.id, icon: CalendarPlus, title: 'Comp-off',
      detail: `Worked on ${formatDate(c.workedOn)} · ${c.days} ${c.days === 1 ? 'day' : 'days'}`,
      status: c.status, at: c.createdAtUtc, to: '/me/leave',
    })),
    ...(attendance.data ?? []).map((a) => ({
      key: `a-${a.id}`, type: 'Attendance' as const, subjectId: a.id, icon: Clock, title: humanize(a.kind),
      detail: formatDate(a.date), status: a.status, at: a.createdAtUtc, to: '/me/attendance',
    })),
    ...(expenses.data ?? []).map((e) => ({
      key: `e-${e.id}`, type: 'Expense' as const, subjectId: e.id, icon: Receipt, title: e.title,
      detail: `${formatCurrency(e.total)} · ${e.itemCount} ${e.itemCount === 1 ? 'item' : 'items'}`,
      status: e.status, at: e.createdAtUtc, to: '/me/expenses',
    })),
    ...(pay.data ?? []).map((p) => ({
      key: `p-${p.id}`, type: 'PayRequest' as const, subjectId: p.id, icon: Banknote, title: p.title,
      detail: formatCurrency(p.amount), status: p.status, at: p.createdAtUtc, to: '/me/pay',
    })),
    ...(letters.data ?? []).map((l) => ({
      key: `t-${l.id}`, type: 'Letter' as const, subjectId: l.id, icon: FileSignature, title: l.subject,
      detail: l.referenceNo, status: l.status ?? 'Pending', at: l.issuedOn, to: '/me/documents',
    })),
  ].sort((a, b) => b.at.localeCompare(a.at));

  const isOpen = (r: Row) => OPEN.includes(r.status);
  const visible = rows.filter((r) => status === 'all' || (status === 'open') === isOpen(r));
  const openCount = rows.filter(isOpen).length;

  return (
    <>
      <PageHeader
        title="Requests"
        description={openCount ? `${openCount} waiting for a decision.` : 'Everything you have asked for, in one place.'}
        actions={<NewRequestMenu />}
      />

      <div className="mb-4 flex flex-wrap gap-2" role="tablist" aria-label="Filter requests">
        {(
          [
            ['open', `Waiting${openCount ? ` · ${openCount}` : ''}`],
            ['closed', 'Decided'],
            ['all', 'All'],
          ] as [Status, string][]
        ).map(([v, label]) => (
          <button
            key={v}
            type="button"
            role="tab"
            aria-selected={status === v}
            onClick={() => setStatus(v)}
            className={cn(
              'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
              status === v
                ? 'border-accent-200 bg-accent-50 text-accent-700 dark:border-accent-500/30 dark:bg-accent-500/10 dark:text-accent-300'
                : 'border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      <Card>
        {loading ? (
          <SkeletonRows rows={5} cols={3} />
        ) : !visible.length ? (
          <EmptyState
            icon={Inbox}
            title={status === 'open' ? 'Nothing waiting' : 'No requests yet'}
            description={status === 'open' ? 'Requests that need a decision show up here.' : 'Use New request to ask for leave, expenses and more.'}
          />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {visible.map((r) => {
              const expanded = open === r.key;
              return (
                <li key={r.key}>
                  <button
                    type="button"
                    aria-expanded={expanded}
                    onClick={() => setOpen(expanded ? null : r.key)}
                    className="flex w-full items-center gap-3 px-4 py-3.5 text-left transition-colors hover:bg-zinc-50 focus-visible:outline-2 focus-visible:outline-accent-500 sm:px-5 dark:hover:bg-zinc-800/50"
                  >
                    <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
                      <r.icon className="size-4" aria-hidden="true" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{r.title}</span>
                      <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">{r.detail}</span>
                    </span>
                    <span className="hidden text-xs text-zinc-400 sm:block">{r.type === 'Letter' ? formatDate(r.at) : formatDateTimeUtc(r.at)}</span>
                    <StatusBadge status={r.status} />
                    <ChevronDown className={cn('size-4 shrink-0 text-zinc-400 transition-transform', expanded && 'rotate-180')} aria-hidden="true" />
                  </button>
                  {expanded && (
                    <div className="border-t border-zinc-100 bg-zinc-50/60 px-5 py-4 dark:border-zinc-800 dark:bg-zinc-900/40">
                      <ApprovalTimeline type={r.type} subjectId={r.subjectId} />
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </>
  );
}

/** "New request" button with every kind of request and where it is made. */
export function NewRequestMenu({ variant = 'primary' }: { variant?: 'primary' | 'secondary' }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <Button variant={variant} icon={<Plus className="size-4" />} aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        New request
      </Button>
      {open && (
        <div
          role="menu"
          className="absolute right-0 z-40 mt-2 w-72 animate-pop-in overflow-hidden rounded-xl border border-zinc-200 bg-white py-1.5 shadow-lg dark:border-zinc-700 dark:bg-zinc-900"
        >
          {NEW_REQUESTS.map((r) => (
            <button
              key={r.label}
              type="button"
              role="menuitem"
              onClick={() => (setOpen(false), navigate(r.to))}
              className="flex w-full items-start gap-3 px-3.5 py-2 text-left hover:bg-zinc-50 focus-visible:bg-zinc-50 focus-visible:outline-none dark:hover:bg-zinc-800 dark:focus-visible:bg-zinc-800"
            >
              <r.icon className="mt-0.5 size-4 shrink-0 text-zinc-400" aria-hidden="true" />
              <span>
                <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">{r.label}</span>
                <span className="block text-xs text-zinc-500 dark:text-zinc-400">{r.hint}</span>
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
