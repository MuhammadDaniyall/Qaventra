import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarCheck, ChevronLeft, ChevronRight, Pencil, Plus, Search } from 'lucide-react';
import { attendanceApi } from '../../api/attendance';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { ExportRangeButton, ImportButton } from '../../components/excel/ExcelButtons';
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDate, formatMinutes, formatTime, timeFromInput, toIsoDate, todayIso } from '../../lib/format';
import type { DailyRow } from '../../types';

function shiftDate(iso: string, days: number) {
  const [y, m, d] = iso.split('-').map(Number);
  return toIsoDate(new Date(y, m - 1, d + days));
}

export function DailyAttendancePage() {
  const user = useUser();
  const hr = isHr(user);
  const [date, setDate] = useState(todayIso());
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<DailyRow | null>(null);
  const daily = useQuery({ queryKey: qk.attendanceDaily(date), queryFn: () => attendanceApi.daily(date) });

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase();
    const list = daily.data ?? [];
    return s ? list.filter((r) => r.fullName.toLowerCase().includes(s) || r.employeeCode.toLowerCase().includes(s)) : list;
  }, [daily.data, search]);

  const counts = useMemo(() => {
    const list = daily.data ?? [];
    return {
      total: list.length,
      present: list.filter((r) => r.record).length,
      late: list.filter((r) => r.record?.status === 'Late').length,
      leave: list.filter((r) => !r.record && r.leaveType).length,
      missing: list.filter((r) => !r.record && !r.leaveType).length,
    };
  }, [daily.data]);

  return (
    <>
      <PageHeader
        title="Daily attendance"
        description={hr ? "Everyone's status for the day." : "Your team's status for the day."}
        actions={
          hr && (
            <>
              <ImportButton kind="attendance" invalidate={[qk.attendance, qk.dashboard]} />
              <ExportRangeButton path="/exports/attendance" name="attendance" />
            </>
          )
        }
      />

      <Card>
        <div className="flex flex-col gap-3 border-b border-zinc-100 p-4 lg:flex-row lg:items-center lg:justify-between dark:border-zinc-800">
          <div className="flex items-center gap-1">
            <IconButton label="Previous day" onClick={() => setDate((d) => shiftDate(d, -1))}>
              <ChevronLeft className="size-4" />
            </IconButton>
            <Input
              type="date"
              aria-label="Date"
              value={date}
              max={todayIso()}
              onChange={(e) => e.target.value && setDate(e.target.value)}
              className="w-40"
            />
            <IconButton label="Next day" onClick={() => setDate((d) => shiftDate(d, 1))} disabled={date >= todayIso()}>
              <ChevronRight className="size-4" />
            </IconButton>
            {date !== todayIso() && (
              <Button variant="ghost" size="sm" onClick={() => setDate(todayIso())}>
                Today
              </Button>
            )}
          </div>
          {daily.data && (
            <div className="flex flex-wrap gap-2 text-xs">
              <Badge tone="gray">{counts.total} people</Badge>
              <Badge tone="green" dot>{counts.present} checked in</Badge>
              <Badge tone="amber" dot>{counts.late} late</Badge>
              <Badge tone="blue" dot>{counts.leave} on leave</Badge>
              <Badge tone="red" dot>{counts.missing} not in</Badge>
            </div>
          )}
          <div className="relative lg:w-64">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
            <Input aria-label="Search" placeholder="Search name or code" className="pl-9" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
        </div>

        {daily.isLoading ? (
          <SkeletonRows rows={8} cols={6} />
        ) : daily.isError ? (
          <ErrorState error={daily.error} onRetry={() => daily.refetch()} />
        ) : rows.length === 0 ? (
          <EmptyState icon={CalendarCheck} title={search ? 'No one matches' : 'No employees for this day'} description={`${formatDate(date)}`} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Department</Th>
                <Th>Status</Th>
                <Th>Check in</Th>
                <Th>Check out</Th>
                <Th>Worked</Th>
                <Th>Office</Th>
                {hr && <Th className="text-right">Manual entry</Th>}
              </tr>
            </THead>
            <TBody>
              {rows.map((r) => (
                <Tr key={r.employeeId}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.fullName}</p>
                    <p className="font-mono text-xs text-zinc-500">{r.employeeCode}</p>
                  </Td>
                  <Td>{r.department ?? '—'}</Td>
                  <Td>
                    {r.record ? (
                      <StatusBadge status={r.record.status} />
                    ) : r.leaveType ? (
                      <StatusBadge status="OnLeave" label={`On leave · ${r.leaveType}`} />
                    ) : (
                      <StatusBadge status="Absent" label="Not checked in" />
                    )}
                  </Td>
                  <Td className="tabular-nums">{formatTime(r.record?.checkIn)}</Td>
                  <Td className="tabular-nums">{formatTime(r.record?.checkOut)}</Td>
                  <Td className="tabular-nums">{formatMinutes(r.record?.workedMinutes)}</Td>
                  <Td>{r.record?.office ?? (r.record?.source === 'Manual' ? 'Manual' : '—')}</Td>
                  {hr && (
                    <Td className="text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        icon={r.record ? <Pencil className="size-3.5" /> : <Plus className="size-3.5" />}
                        onClick={() => setEditing(r)}
                      >
                        {r.record ? 'Edit' : 'Add'}
                      </Button>
                    </Td>
                  )}
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {editing && <ManualEntryModal row={editing} date={date} onClose={() => setEditing(null)} />}
    </>
  );
}

function ManualEntryModal({ row, date, onClose }: { row: DailyRow; date: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [checkIn, setCheckIn] = useState(row.record ? formatTime24(row.record.checkIn) : '09:00');
  const [checkOut, setCheckOut] = useState(row.record?.checkOut ? formatTime24(row.record.checkOut) : '');
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () =>
      attendanceApi.manual({
        employeeId: row.employeeId,
        date,
        checkIn: timeFromInput(checkIn),
        checkOut: checkOut ? timeFromInput(checkOut) : null,
      }),
    onSuccess: () => {
      toast.success('Attendance saved', `${row.fullName} · ${formatDate(date)}`);
      qc.invalidateQueries({ queryKey: qk.attendance });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? errorMessage(err) : String(err)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={row.record ? 'Edit attendance' : 'Add attendance'}
      description={`${row.fullName} · ${formatDate(date)}`}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!checkIn) return setError('Check-in time is required.');
              if (checkOut && checkOut <= checkIn) return setError('Check-out must be after check-in.');
              save.mutate();
            }}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Check in" required>
            <Input type="time" value={checkIn} onChange={(e) => setCheckIn(e.target.value)} />
          </Field>
          <Field label="Check out">
            <Input type="time" value={checkOut} onChange={(e) => setCheckOut(e.target.value)} />
          </Field>
        </div>
        <p className="text-xs text-zinc-500 dark:text-zinc-400">
          Times are in company-local time. Status (present/late) is worked out from the employee's shift.
        </p>
      </div>
    </Modal>
  );
}

/** "2026-10-20T09:05:00" -> "09:05". */
function formatTime24(localDateTime: string) {
  const m = /T(\d{2}:\d{2})/.exec(localDateTime);
  return m ? m[1] : '';
}
