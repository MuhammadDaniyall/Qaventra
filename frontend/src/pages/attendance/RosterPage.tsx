import { useMemo, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarRange, ChevronLeft, ChevronRight, Lock, Search, Users } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useDepartments } from '../../api/hooks';
import { qk } from '../../api/keys';
import { schedulingApi, type Roster, type RosterCell, type RosterRow, type RosterShift } from '../../api/scheduling';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Button,
  Card,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Tabs,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, formatTime, toIsoDate } from '../../lib/format';
import type { DayOfWeek } from '../../types';

const PALETTE = [
  'bg-sky-100 text-sky-800 dark:bg-sky-500/15 dark:text-sky-200',
  'bg-violet-100 text-violet-800 dark:bg-violet-500/15 dark:text-violet-200',
  'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/15 dark:text-emerald-200',
  'bg-orange-100 text-orange-800 dark:bg-orange-500/15 dark:text-orange-200',
  'bg-pink-100 text-pink-800 dark:bg-pink-500/15 dark:text-pink-200',
  'bg-teal-100 text-teal-800 dark:bg-teal-500/15 dark:text-teal-200',
  'bg-indigo-100 text-indigo-800 dark:bg-indigo-500/15 dark:text-indigo-200',
];

const WEEK: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

function startOfWeek(d: Date) {
  const x = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  x.setDate(x.getDate() - ((x.getDay() + 6) % 7));
  return x;
}

function addDays(d: Date, n: number) {
  const x = new Date(d);
  x.setDate(x.getDate() + n);
  return x;
}

/** Short label for a shift: its first letters, e.g. "General (9 to 6)" → "GE", plus the start time. */
function shortName(s: RosterShift) {
  const words = s.name.replace(/\(.*\)/, '').trim().split(/\s+/);
  return (words.length > 1 ? words[0][0] + words[1][0] : words[0].slice(0, 3)).toUpperCase();
}

const isClosed = (roster: Roster, date: string) => {
  const [y, m] = date.split('-').map(Number);
  return roster.closedMonths.includes(`${y}-${m}`);
};

export function RosterPage() {
  const user = useUser();
  const hr = isHr(user);
  const departments = useDepartments();
  const [start, setStart] = useState(() => startOfWeek(new Date()));
  const [span, setSpan] = useState<'7' | '14'>('7');
  const [departmentId, setDepartmentId] = useState('');
  const [editing, setEditing] = useState<{ row: RosterRow; cell: RosterCell } | null>(null);
  const [assigning, setAssigning] = useState(false);
  const from = toIsoDate(start);
  const to = toIsoDate(addDays(start, Number(span) - 1));
  const roster = useQuery({
    queryKey: qk.roster(from, to, departmentId),
    queryFn: () => schedulingApi.roster(from, to, departmentId || undefined),
    placeholderData: keepPreviousData,
  });
  const colors = useMemo(() => new Map((roster.data?.shifts ?? []).map((s, i) => [s.id, PALETTE[i % PALETTE.length]])), [roster.data?.shifts]);
  const shiftById = useMemo(() => new Map((roster.data?.shifts ?? []).map((s) => [s.id, s])), [roster.data?.shifts]);
  const today = toIsoDate(new Date());

  return (
    <>
      <PageHeader
        title="Roster"
        description={hr ? 'Who works which shift each day. Click a day to change it, or plan many people at once.' : 'Who in your team works which shift each day.'}
        actions={
          hr && (
            <Button icon={<CalendarRange className="size-4" />} onClick={() => setAssigning(true)}>
              Assign shifts
            </Button>
          )
        }
      />
      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 p-4 dark:border-zinc-800">
          <div className="flex items-center gap-1">
            <IconButton label="Previous" onClick={() => setStart((s) => addDays(s, -Number(span)))}>
              <ChevronLeft className="size-4" />
            </IconButton>
            <p className="min-w-44 text-center text-sm font-medium text-zinc-900 dark:text-white">
              {formatDate(from)} – {formatDate(to)}
            </p>
            <IconButton label="Next" onClick={() => setStart((s) => addDays(s, Number(span)))}>
              <ChevronRight className="size-4" />
            </IconButton>
            <Button variant="ghost" size="sm" onClick={() => setStart(startOfWeek(new Date()))}>
              This week
            </Button>
          </div>
          <Tabs<'7' | '14'>
            className="border-0"
            value={span}
            onChange={setSpan}
            items={[
              { value: '7', label: '1 week' },
              { value: '14', label: '2 weeks' },
            ]}
          />
          <div className="ml-auto w-56">
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
        {roster.data && (
          <div className="flex flex-wrap items-center gap-2 border-b border-zinc-100 px-4 py-2.5 text-xs dark:border-zinc-800">
            {roster.data.shifts.map((s) => (
              <span key={s.id} className={cn('rounded-md px-2 py-0.5 font-medium', colors.get(s.id))}>
                {shortName(s)} · {s.name} {formatTime(s.startTime)}–{formatTime(s.endTime)}
              </span>
            ))}
            <span className="rounded-md bg-zinc-100 px-2 py-0.5 text-zinc-500 dark:bg-zinc-800">Off</span>
            <span className="rounded-md bg-amber-50 px-2 py-0.5 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300">Holiday</span>
            <span className="text-zinc-500">
              <span className="mr-1 inline-block size-1.5 rounded-full bg-accent-500 align-middle" /> changed on the roster
            </span>
          </div>
        )}
        {roster.isLoading ? (
          <SkeletonRows rows={6} cols={8} />
        ) : roster.isError ? (
          <ErrorState error={roster.error} onRetry={() => roster.refetch()} />
        ) : !roster.data?.rows.length ? (
          <EmptyState icon={Users} title="Nobody to schedule" description="No current employees match this filter." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-separate border-spacing-0 text-sm">
              <thead>
                <tr>
                  <th className="sticky left-0 z-10 min-w-48 border-b border-zinc-100 bg-white px-4 py-2 text-left text-xs font-medium text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900">
                    Employee
                  </th>
                  {roster.data.rows[0].days.map((c) => {
                    const d = new Date(c.date + 'T00:00:00');
                    return (
                      <th
                        key={c.date}
                        className={cn(
                          'min-w-16 border-b border-zinc-100 px-1 py-2 text-center text-xs font-medium dark:border-zinc-800',
                          c.date === today ? 'text-accent-600 dark:text-accent-400' : 'text-zinc-500',
                        )}
                      >
                        <span className="block">{d.toLocaleDateString(undefined, { weekday: 'short' })}</span>
                        <span className="block text-zinc-900 tabular-nums dark:text-zinc-100">{d.getDate()}</span>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {roster.data.rows.map((row) => (
                  <tr key={row.employeeId} className="group">
                    <td className="sticky left-0 z-10 border-b border-zinc-100 bg-white px-4 py-2 group-hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:group-hover:bg-zinc-800/60">
                      <p className="truncate font-medium text-zinc-900 dark:text-zinc-100">{row.name}</p>
                      <p className="truncate text-xs text-zinc-500">{row.department ?? row.employeeCode}</p>
                    </td>
                    {row.days.map((c) => {
                      const shift = c.shiftId ? shiftById.get(c.shiftId) : undefined;
                      const locked = isClosed(roster.data!, c.date);
                      const label = c.isOff ? 'Off' : shift ? shortName(shift) : '—';
                      const title = `${row.name} · ${formatDate(c.date)} · ${c.isOff ? 'Day off' : shift ? `${shift.name} ${formatTime(shift.startTime)}–${formatTime(shift.endTime)}` : 'No shift'}${c.isHoliday ? ' · Holiday' : ''}${c.rostered ? ` · set by ${c.source === 'Roster' ? 'roster' : c.source === 'Swap' ? 'swap' : 'request'}` : ''}${locked ? ' · month closed' : ''}`;
                      return (
                        <td key={c.date} className="border-b border-zinc-100 px-1 py-1.5 text-center group-hover:bg-zinc-50 dark:border-zinc-800 dark:group-hover:bg-zinc-800/60">
                          <button
                            type="button"
                            title={title}
                            aria-label={title}
                            disabled={!hr || locked}
                            onClick={() => setEditing({ row, cell: c })}
                            className={cn(
                              'relative inline-flex h-8 w-full min-w-12 items-center justify-center rounded-md text-xs font-semibold transition',
                              c.isHoliday && !c.isOff ? 'bg-amber-50 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300'
                                : c.isOff ? 'bg-zinc-100 text-zinc-400 dark:bg-zinc-800 dark:text-zinc-500'
                                : shift ? colors.get(shift.id) : 'text-zinc-400',
                              hr && !locked && 'hover:ring-2 hover:ring-accent-400/60',
                              locked && 'opacity-60',
                            )}
                          >
                            {label}
                            {c.rostered && <span className="absolute top-1 right-1 size-1.5 rounded-full bg-accent-500" aria-hidden="true" />}
                            {locked && <Lock className="absolute right-0.5 bottom-0.5 size-2.5 opacity-60" aria-hidden="true" />}
                          </button>
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
      {editing && roster.data && <DayModal {...editing} shifts={roster.data.shifts} onClose={() => setEditing(null)} />}
      {assigning && roster.data && <AssignModal shifts={roster.data.shifts} defaultFrom={from} defaultTo={to} onClose={() => setAssigning(false)} />}
    </>
  );
}

type Choice = { kind: 'shift'; id: string } | { kind: 'off' } | { kind: 'reset' };

function useAssign(onDone: (days: number) => void) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: schedulingApi.assign,
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: qk.attendance });
      onDone(r.days);
    },
  });
}

function toBody(choice: Choice) {
  return { shiftId: choice.kind === 'shift' ? choice.id : null, off: choice.kind === 'off', clear: choice.kind === 'reset' };
}

function ChoicePicker({ shifts, value, onChange, showReset }: { shifts: RosterShift[]; value: Choice | null; onChange: (c: Choice) => void; showReset: boolean }) {
  const opt = (key: string, label: string, hint: string, choice: Choice) => {
    const active = value && JSON.stringify(value) === JSON.stringify(choice);
    return (
      <button
        key={key}
        type="button"
        onClick={() => onChange(choice)}
        aria-pressed={!!active}
        className={cn(
          'rounded-lg border px-3 py-2 text-left text-sm transition',
          active ? 'border-accent-500 bg-accent-50 ring-2 ring-accent-500/30 dark:bg-accent-500/10' : 'border-zinc-200 hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800',
        )}
      >
        <span className="block font-medium text-zinc-900 dark:text-zinc-100">{label}</span>
        <span className="block text-xs text-zinc-500">{hint}</span>
      </button>
    );
  };
  return (
    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
      {shifts.map((s) => opt(s.id, s.name, `${formatTime(s.startTime)} – ${formatTime(s.endTime)}`, { kind: 'shift', id: s.id }))}
      {opt('off', 'Day off', 'Not working', { kind: 'off' })}
      {showReset && opt('reset', 'Usual shift', 'Back to their own shift and off days', { kind: 'reset' })}
    </div>
  );
}

function DayModal({ row, cell, shifts, onClose }: { row: RosterRow; cell: RosterCell; shifts: RosterShift[]; onClose: () => void }) {
  const toast = useToast();
  const [choice, setChoice] = useState<Choice | null>(cell.isOff ? { kind: 'off' } : cell.shiftId ? { kind: 'shift', id: cell.shiftId } : null);
  const [error, setError] = useState<string | null>(null);
  const save = useAssign(() => {
    toast.success('Roster updated', `${row.name} · ${formatDate(cell.date)}`);
    onClose();
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`${row.name} · ${formatDate(cell.date)}`}
      description={cell.isHoliday ? 'This day is a company holiday.' : cell.rostered ? 'Changed on the roster.' : 'Following their usual shift.'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            disabled={!choice}
            onClick={() =>
              save.mutate(
                { employeeIds: [row.employeeId], from: cell.date, to: cell.date, weekdays: null, ...toBody(choice!) },
                { onError: (err) => setError(errorMessage(err)) },
              )
            }
          >
            Save
          </Button>
        </>
      }
    >
      <FormError message={error} />
      <ChoicePicker shifts={shifts} value={choice} onChange={setChoice} showReset={cell.rostered} />
    </Modal>
  );
}

function AssignModal({ shifts, defaultFrom, defaultTo, onClose }: { shifts: RosterShift[]; defaultFrom: string; defaultTo: string; onClose: () => void }) {
  const toast = useToast();
  const departments = useDepartments();
  const [departmentId, setDepartmentId] = useState('');
  const [search, setSearch] = useState('');
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(defaultTo);
  const [days, setDays] = useState<Set<DayOfWeek>>(new Set());
  const [choice, setChoice] = useState<Choice | null>(null);
  const [error, setError] = useState<string | null>(null);
  // The roster for the chosen range doubles as the list of people to pick from.
  const people = useQuery({
    queryKey: qk.roster(defaultFrom, defaultFrom, departmentId),
    queryFn: () => schedulingApi.roster(defaultFrom, defaultFrom, departmentId || undefined),
  });
  const shown = (people.data?.rows ?? []).filter((r) => !search.trim() || r.name.toLowerCase().includes(search.trim().toLowerCase()));
  const save = useAssign((n) => {
    toast.success('Roster updated', `${n} day${n === 1 ? '' : 's'} planned`);
    onClose();
  });
  const toggle = (id: string) =>
    setPicked((p) => {
      const n = new Set(p);
      if (n.has(id)) n.delete(id);
      else n.add(id);
      return n;
    });

  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title="Assign shifts"
      description="Pick people, the days and what they work. Days you leave out keep their current plan."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            disabled={!picked.size || !choice}
            onClick={() => {
              setError(null);
              save.mutate(
                { employeeIds: [...picked], from, to, weekdays: days.size ? [...days] : null, ...toBody(choice!) },
                { onError: (err) => setError(errorMessage(err)) },
              );
            }}
          >
            Assign to {picked.size || ''} {picked.size === 1 ? 'person' : 'people'}
          </Button>
        </>
      }
    >
      <div className="grid gap-5 md:grid-cols-2">
        <div className="space-y-3">
          <FormError message={error} />
          <div className="flex gap-2">
            <div className="relative flex-1">
              <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
              <Input className="pl-9" placeholder="Search" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Search people" />
            </div>
            <Select className="w-40" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)} aria-label="Department">
              <option value="">All departments</option>
              {departments.data?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
          </div>
          {shown.length > 0 && (
            <Checkbox
              label={`Select all ${shown.length} shown`}
              checked={shown.every((r) => picked.has(r.employeeId))}
              onChange={(e) =>
                setPicked((p) => {
                  const n = new Set(p);
                  shown.forEach((r) => (e.target.checked ? n.add(r.employeeId) : n.delete(r.employeeId)));
                  return n;
                })
              }
            />
          )}
          <ul className="max-h-72 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {shown.map((r) => (
              <li key={r.employeeId} className="px-3 py-2">
                <Checkbox label={r.name} description={r.department ?? r.employeeCode} checked={picked.has(r.employeeId)} onChange={() => toggle(r.employeeId)} />
              </li>
            ))}
          </ul>
        </div>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="From">
              <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
            </Field>
            <Field label="To">
              <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
            </Field>
          </div>
          <Field label="Only on" hint={days.size ? undefined : 'Every day in the range'}>
            <div className="flex flex-wrap gap-1.5">
              {WEEK.map((d) => (
                <button
                  key={d}
                  type="button"
                  aria-pressed={days.has(d)}
                  onClick={() =>
                    setDays((s) => {
                      const n = new Set(s);
                      if (n.has(d)) n.delete(d);
                      else n.add(d);
                      return n;
                    })
                  }
                  className={cn(
                    'rounded-md border px-2.5 py-1 text-xs font-medium',
                    days.has(d) ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300' : 'border-zinc-200 text-zinc-600 dark:border-zinc-700 dark:text-zinc-300',
                  )}
                >
                  {d.slice(0, 3)}
                </button>
              ))}
            </div>
          </Field>
          <ChoicePicker shifts={shifts} value={choice} onChange={setChoice} showReset />
        </div>
      </div>
    </Modal>
  );
}
