import { useMemo, useState } from 'react';
import { ExcuseButton, MyExemptions } from './Exemptions';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeftRight, CalendarClock, ChevronLeft, ChevronRight, ClipboardEdit, Repeat } from 'lucide-react';
import { attendanceApi } from '../../api/attendance';
import { qk } from '../../api/keys';
import { setupApi } from '../../api/organization';
import {
  Button,
  Card,
  CardHeader,
  EmptyState,
  ErrorState,
  IconButton,
  PageHeader,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, formatMinutes, formatTime, monthLabel, pad, todayIso } from '../../lib/format';
import type { Attendance, AttendanceStatus } from '../../types';
import { MyAttendanceRequests, RequestAttendanceModal } from './AttendanceRequests';
import { CheckInCard } from './CheckInCard';
import { MyShiftRequests, ShiftRequestModal } from './ShiftRequests';

const DOT: Record<AttendanceStatus, string> = {
  Present: 'bg-emerald-500',
  Late: 'bg-amber-500',
  HalfDay: 'bg-amber-400',
  Absent: 'bg-red-500',
  OnLeave: 'bg-sky-500',
};

export function MyAttendancePage() {
  const now = new Date();
  const [ym, setYm] = useState({ y: now.getFullYear(), m: now.getMonth() + 1 });
  const [requesting, setRequesting] = useState(false);
  const [shiftRequest, setShiftRequest] = useState<'Change' | 'Swap' | null>(null);
  const records = useQuery({ queryKey: qk.attendanceMy(ym.y, ym.m), queryFn: () => attendanceApi.my(ym.y, ym.m) });
  const holidays = useQuery({ queryKey: qk.holidays(ym.y), queryFn: () => setupApi.holidays(ym.y), staleTime: 300000 });

  const shift = (delta: number) =>
    setYm(({ y, m }) => {
      const d = new Date(y, m - 1 + delta, 1);
      return { y: d.getFullYear(), m: d.getMonth() + 1 };
    });
  const isCurrent = ym.y === now.getFullYear() && ym.m === now.getMonth() + 1;

  const summary = useMemo(() => {
    const list = records.data ?? [];
    return {
      days: list.length,
      late: list.filter((r) => r.status === 'Late').length,
      minutes: list.reduce((t, r) => t + r.workedMinutes, 0),
    };
  }, [records.data]);

  return (
    <>
      <PageHeader
        title="My attendance"
        description="Check in when you arrive and check out when you leave."
        actions={
          <>
            <Button variant="secondary" icon={<Repeat className="size-4" />} onClick={() => setShiftRequest('Change')}>
              Change shift
            </Button>
            <Button variant="secondary" icon={<ArrowLeftRight className="size-4" />} onClick={() => setShiftRequest('Swap')}>
              Swap shift
            </Button>
            <Button variant="secondary" icon={<ClipboardEdit className="size-4" />} onClick={() => setRequesting(true)}>
              Request correction
            </Button>
          </>
        }
      />
      <CheckInCard className="mb-6" />

      <Card>
        <CardHeader
          title={monthLabel(ym.y, ym.m)}
          description={
            records.data
              ? `${summary.days} days recorded · ${summary.late} late · ${formatMinutes(summary.minutes)} worked`
              : undefined
          }
          actions={
            <div className="flex items-center gap-1">
              <IconButton label="Previous month" onClick={() => shift(-1)}>
                <ChevronLeft className="size-4" />
              </IconButton>
              <IconButton label="Next month" onClick={() => shift(1)} disabled={isCurrent}>
                <ChevronRight className="size-4" />
              </IconButton>
            </div>
          }
        />
        {records.isLoading ? (
          <SkeletonRows rows={6} cols={5} />
        ) : records.isError ? (
          <ErrorState error={records.error} onRetry={() => records.refetch()} />
        ) : (
          <>
            <MonthCalendar
              year={ym.y}
              month={ym.m}
              records={records.data ?? []}
              holidays={new Map((holidays.data ?? []).map((h) => [h.date, h.name]))}
            />
            {(records.data ?? []).length === 0 ? (
              <EmptyState icon={CalendarClock} title="No attendance this month" description="Your check-ins will appear here." className="border-t border-zinc-100 dark:border-zinc-800" />
            ) : (
              <div className="border-t border-zinc-100 dark:border-zinc-800">
                <Table>
                  <THead>
                    <tr>
                      <Th>Date</Th>
                      <Th>Status</Th>
                      <Th>Check in</Th>
                      <Th>Check out</Th>
                      <Th>Worked</Th>
                      <Th>Office</Th>
                      <Th>Source</Th>
                      <Th />
                    </tr>
                  </THead>
                  <TBody>
                    {[...(records.data ?? [])].reverse().map((r) => (
                      <Tr key={r.id}>
                        <Td className="font-medium text-zinc-900 dark:text-zinc-100">{formatDate(r.date)}</Td>
                        <Td>
                          <StatusBadge status={r.status} />
                        </Td>
                        <Td className="tabular-nums">{formatTime(r.checkIn)}</Td>
                        <Td className="tabular-nums">{formatTime(r.checkOut)}</Td>
                        <Td className="tabular-nums">{formatMinutes(r.workedMinutes)}</Td>
                        <Td>{r.office ?? '—'}</Td>
                        <Td title={r.note ?? undefined}>{r.source === 'Request' ? 'Approved request' : r.source}</Td>
                        <Td className="text-right">
                          <ExcuseButton date={r.date} flag={r.status} />
                        </Td>
                      </Tr>
                    ))}
                  </TBody>
                </Table>
              </div>
            )}
          </>
        )}
      </Card>
      <MyExemptions />
      <MyShiftRequests />
      <MyAttendanceRequests />
      {requesting && <RequestAttendanceModal onClose={() => setRequesting(false)} />}
      {shiftRequest && <ShiftRequestModal kind={shiftRequest} onClose={() => setShiftRequest(null)} />}
    </>
  );
}

function MonthCalendar({
  year,
  month,
  records,
  holidays,
}: {
  year: number;
  month: number;
  records: Attendance[];
  holidays: Map<string, string>;
}) {
  const byDate = new Map(records.map((r) => [r.date, r]));
  const first = new Date(year, month - 1, 1);
  const daysInMonth = new Date(year, month, 0).getDate();
  const offset = (first.getDay() + 6) % 7; // Monday first
  const today = todayIso();
  const cells: Array<number | null> = [...Array(offset).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => i + 1)];

  return (
    <div className="p-4 sm:p-5">
      <div className="grid grid-cols-7 gap-1 text-center text-[11px] font-medium text-zinc-400 uppercase">
        {['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map((d) => (
          <div key={d} className="py-1">
            {d}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7 gap-1">
        {cells.map((day, i) => {
          if (day === null) return <div key={`e${i}`} />;
          const iso = `${year}-${pad(month)}-${pad(day)}`;
          const r = byDate.get(iso);
          const holiday = holidays.get(iso);
          const isToday = iso === today;
          return (
            <div
              key={iso}
              title={[formatDate(iso), r ? r.status : null, holiday].filter(Boolean).join(' · ')}
              className={cn(
                'flex aspect-square min-h-10 flex-col items-center justify-center gap-1 rounded-lg text-sm sm:aspect-auto sm:h-14',
                isToday ? 'ring-2 ring-accent-500' : '',
                holiday ? 'bg-sky-50 dark:bg-sky-500/10' : r ? 'bg-zinc-50 dark:bg-zinc-800/50' : '',
              )}
            >
              <span className={cn('tabular-nums', iso > today ? 'text-zinc-300 dark:text-zinc-600' : 'text-zinc-700 dark:text-zinc-300')}>
                {day}
              </span>
              {r ? (
                <span className={cn('size-1.5 rounded-full', DOT[r.status])} aria-label={r.status} />
              ) : holiday ? (
                <span className="hidden max-w-full truncate px-1 text-[10px] text-sky-700 sm:block dark:text-sky-300">{holiday}</span>
              ) : null}
            </div>
          );
        })}
      </div>
      <div className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-xs text-zinc-500 dark:text-zinc-400">
        {(['Present', 'Late', 'HalfDay', 'OnLeave'] as AttendanceStatus[]).map((s) => (
          <span key={s} className="inline-flex items-center gap-1.5">
            <span className={cn('size-1.5 rounded-full', DOT[s])} />
            {s === 'HalfDay' ? 'Half day' : s === 'OnLeave' ? 'On leave' : s}
          </span>
        ))}
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-sky-100 dark:bg-sky-500/20" /> Holiday
        </span>
      </div>
    </div>
  );
}
