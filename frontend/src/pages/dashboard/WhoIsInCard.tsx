import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Download, Users } from 'lucide-react';
import { attendanceApi } from '../../api/attendance';
import { Button, Card, CardHeader, EmptyState, Skeleton } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatClock, todayIso } from '../../lib/format';
import type { Dashboard, DailyRow } from '../../types';

type Tab = 'in' | 'leave' | 'out';

const TABS: { key: Tab; label: string }[] = [
  { key: 'in', label: 'In' },
  { key: 'leave', label: 'On leave' },
  { key: 'out', label: 'Not in' },
];

const timeOf = (iso: string) => formatClock(new Date(iso));

function bucket(r: DailyRow): Tab {
  if (r.record) return 'in';
  if (r.leaveType) return 'leave';
  return 'out';
}

/** Who is available today (managers see their team), with a download of the day and the dashboard numbers. */
export function WhoIsInCard({ d, className, linkPeople }: { d: Dashboard | undefined; className?: string; /** HR: names open the employee file. */ linkPeople?: boolean }) {
  const today = todayIso();
  const daily = useQuery({ queryKey: ['attendance', 'daily', today], queryFn: () => attendanceApi.daily(today) });
  const [tab, setTab] = useState<Tab>('in');
  const rows = daily.data ?? [];
  const shown = rows.filter((r) => bucket(r) === tab);

  const download = () => {
    const esc = (s: string | number | null | undefined) => {
      const v = s == null ? '' : String(s);
      return /[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
    };
    const lines = [
      ['Dashboard', today].join(','),
      ...(d
        ? [
            ['Headcount', d.headcount],
            ['Present today', d.presentToday],
            ['Late today', d.lateToday],
            ['On leave', d.onLeaveToday],
            ['Pending approvals', d.pendingApprovals],
            ['New joiners this month', d.newJoinersThisMonth],
          ].map((x) => x.map(esc).join(','))
        : []),
      '',
      ['Code', 'Name', 'Department', 'Today', 'In', 'Out'].join(','),
      ...rows.map((r) =>
        [
          r.employeeCode,
          r.fullName,
          r.department,
          r.record ? r.record.status : r.leaveType ? `On leave (${r.leaveType})` : 'Not in',
          r.record ? timeOf(r.record.checkIn) : '',
          r.record?.checkOut ? timeOf(r.record.checkOut) : '',
        ]
          .map(esc)
          .join(','),
      ),
    ];
    const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `dashboard-${today}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <Card className={className}>
      <CardHeader
        title="Who is in today"
        description={`${rows.filter((r) => r.record).length} in · ${rows.filter((r) => !r.record && r.leaveType).length} on leave · ${rows.filter((r) => !r.record && !r.leaveType).length} not in`}
        actions={
          <Button size="sm" variant="ghost" icon={<Download className="size-4" />} disabled={daily.isLoading} onClick={download}>
            Download
          </Button>
        }
      />
      <div className="flex gap-1 px-5" role="tablist">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={tab === t.key}
            onClick={() => setTab(t.key)}
            className={cn(
              'rounded-full px-3 py-1 text-xs font-medium',
              tab === t.key ? 'bg-zinc-900 text-white dark:bg-white dark:text-zinc-900' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800',
            )}
          >
            {t.label} {rows.filter((r) => bucket(r) === t.key).length}
          </button>
        ))}
      </div>
      <div className="max-h-72 overflow-y-auto px-5 py-3">
        {daily.isLoading ? (
          <Skeleton className="h-32 w-full" />
        ) : shown.length === 0 ? (
          <EmptyState icon={Users} title="Nobody here" className="py-6" />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {shown.map((r) => (
              <li key={r.employeeId} className="flex items-center justify-between gap-3 py-2 text-sm">
                <span className="min-w-0">
                  {linkPeople ? (
                    <Link to={`/employees/${r.employeeId}`} className="block truncate font-medium text-zinc-900 hover:text-indigo-600 hover:underline dark:text-zinc-100 dark:hover:text-indigo-300">
                      {r.fullName}
                    </Link>
                  ) : (
                    <span className="block truncate font-medium text-zinc-900 dark:text-zinc-100">{r.fullName}</span>
                  )}
                  <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">{r.department ?? r.employeeCode}</span>
                </span>
                <span className="shrink-0 text-xs text-zinc-500 tabular-nums dark:text-zinc-400">
                  {r.record ? `${timeOf(r.record.checkIn)}${r.record.checkOut ? ` – ${timeOf(r.record.checkOut)}` : ''}` : (r.leaveType ?? '')}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Card>
  );
}
