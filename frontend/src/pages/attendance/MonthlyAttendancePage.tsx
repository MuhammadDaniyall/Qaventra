import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Lock, LockOpen, Users } from 'lucide-react';
import { useDepartments } from '../../api/hooks';
import { qk } from '../../api/keys';
import { schedulingApi } from '../../api/scheduling';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  IconButton,
  PageHeader,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, formatDateTimeUtc, formatMinutes, monthLabel } from '../../lib/format';

/** Per-person totals for a month, and closing the month so its attendance can no longer change. */
export function MonthlyAttendancePage() {
  const user = useUser();
  const hr = isHr(user);
  const toast = useToast();
  const qc = useQueryClient();
  const departments = useDepartments();
  const now = new Date();
  const [ym, setYm] = useState(() => {
    const d = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    return { y: d.getFullYear(), m: d.getMonth() + 1 };
  });
  const [departmentId, setDepartmentId] = useState('');
  const [confirm, setConfirm] = useState<'close' | 'reopen' | null>(null);
  const data = useQuery({
    queryKey: qk.monthlyAttendance(ym.y, ym.m, departmentId),
    queryFn: () => schedulingApi.monthly(ym.y, ym.m, departmentId || undefined),
    placeholderData: keepPreviousData,
  });
  const act = useMutation({
    mutationFn: (what: 'close' | 'reopen') => (what === 'close' ? schedulingApi.close(ym.y, ym.m) : schedulingApi.reopen(ym.y, ym.m)),
    onSuccess: (_, what) => {
      toast.success(what === 'close' ? `${monthLabel(ym.y, ym.m)} closed` : `${monthLabel(ym.y, ym.m)} reopened`);
      setConfirm(null);
      qc.invalidateQueries({ queryKey: qk.attendance });
    },
    onError: (err) => {
      setConfirm(null);
      toast.error(err);
    },
  });
  const shift = (d: number) =>
    setYm(({ y, m }) => {
      const x = new Date(y, m - 1 + d, 1);
      return { y: x.getFullYear(), m: x.getMonth() + 1 };
    });
  const m = data.data;
  const totals = m?.rows.reduce(
    (t, r) => ({ late: t.late + r.late, absent: t.absent + r.absent, half: t.half + r.halfDay, penalty: t.penalty + r.penaltyDays }),
    { late: 0, absent: 0, half: 0, penalty: 0 },
  );

  return (
    <>
      <PageHeader
        title="Monthly attendance"
        description="Totals per person. Close a month once it is checked, so nothing in it changes before payroll."
        back={{ to: '/attendance/daily', label: 'Daily attendance' }}
        actions={
          hr &&
          m &&
          (m.closed ? (
            <Button variant="secondary" icon={<LockOpen className="size-4" />} onClick={() => setConfirm('reopen')}>
              Reopen month
            </Button>
          ) : (
            <Button icon={<Lock className="size-4" />} disabled={!m.canClose} title={m.canClose ? undefined : 'A month can be closed once it has ended'} onClick={() => setConfirm('close')}>
              Close month
            </Button>
          ))
        }
      />
      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 p-4 dark:border-zinc-800">
          <div className="flex items-center gap-1">
            <IconButton label="Previous month" onClick={() => shift(-1)}>
              <ChevronLeft className="size-4" />
            </IconButton>
            <p className="min-w-36 text-center font-medium text-zinc-900 dark:text-white">{monthLabel(ym.y, ym.m)}</p>
            <IconButton label="Next month" onClick={() => shift(1)}>
              <ChevronRight className="size-4" />
            </IconButton>
          </div>
          {m &&
            (m.closed ? (
              <Badge tone="gray">
                <Lock className="size-3" /> Closed {m.closedByName ? `by ${m.closedByName}` : ''} · {formatDateTimeUtc(m.closedAtUtc)}
              </Badge>
            ) : (
              <Badge tone="amber">Open{m.canClose ? '' : ` · counted to ${formatDate(m.countedTo)}`}</Badge>
            ))}
          <div className="ms-auto w-56">
            <Select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} aria-label="Department">
              <option value="">All departments</option>
              {departments.data?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
          </div>
        </div>
        {totals && (
          <p className="border-b border-zinc-100 px-5 py-2.5 text-sm text-zinc-600 dark:border-zinc-800 dark:text-zinc-300">
            {m!.rows.length} people · {totals.absent} absent days · {totals.late} late · {totals.half} half days · {totals.penalty} penalty days
          </p>
        )}
        {data.isLoading ? (
          <SkeletonRows rows={6} cols={9} />
        ) : data.isError ? (
          <ErrorState error={data.error} onRetry={() => data.refetch()} />
        ) : !m?.rows.length ? (
          <EmptyState icon={Users} title="Nobody this month" />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th className="text-end">Working days</Th>
                <Th className="text-end">Present</Th>
                <Th className="text-end">Late</Th>
                <Th className="text-end">Half day</Th>
                <Th className="text-end">Leave</Th>
                <Th className="text-end">Absent</Th>
                <Th className="text-end">Hours</Th>
                <Th className="text-end">Penalty days</Th>
              </tr>
            </THead>
            <TBody>
              {m.rows.map((r) => (
                <Tr key={r.employeeId}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.name}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {r.employeeCode}
                      {r.department ? ` · ${r.department}` : ''}
                    </p>
                  </Td>
                  <Td className="text-end tabular-nums">{r.workingDays}</Td>
                  <Td className="text-end tabular-nums">{r.present}</Td>
                  <Td className={cn('text-end tabular-nums', r.late > 0 && 'text-amber-700 dark:text-amber-300')}>{r.late}</Td>
                  <Td className={cn('text-end tabular-nums', r.halfDay > 0 && 'text-amber-700 dark:text-amber-300')}>{r.halfDay}</Td>
                  <Td className="text-end tabular-nums">{r.leave}</Td>
                  <Td className={cn('text-end tabular-nums', r.absent > 0 && 'font-medium text-red-600 dark:text-red-400')}>{r.absent}</Td>
                  <Td className="text-end tabular-nums">{formatMinutes(r.workedMinutes)}</Td>
                  <Td className={cn('text-end tabular-nums', r.penaltyDays > 0 && 'font-medium text-red-600 dark:text-red-400')}>{r.penaltyDays || '—'}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      <ConfirmDialog
        open={!!confirm}
        title={confirm === 'close' ? `Close ${monthLabel(ym.y, ym.m)}?` : `Reopen ${monthLabel(ym.y, ym.m)}?`}
        message={
          confirm === 'close'
            ? 'Punches, corrections, attendance requests and roster changes for this month will be refused until it is reopened. Finalizing its payroll closes it for good.'
            : 'Attendance for this month can be changed again.'
        }
        confirmLabel={confirm === 'close' ? 'Close month' : 'Reopen'}
        loading={act.isPending}
        onConfirm={() => confirm && act.mutate(confirm)}
        onClose={() => setConfirm(null)}
      />
    </>
  );
}
