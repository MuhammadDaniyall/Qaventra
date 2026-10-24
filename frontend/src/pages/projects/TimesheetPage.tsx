import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, ClipboardList, Plus, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { projectsApi, type Timesheet, type TimesheetLine } from '../../api/projects';
import { useUser } from '../../auth/AuthContext';
import { isApprover } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  ErrorState,
  FormError,
  IconButton,
  Input,
  PageHeader,
  Select,
  SkeletonRows,
  StatusBadge,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatDate, toIsoDate } from '../../lib/format';

const mondayOf = (d: Date) => {
  const x = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  x.setDate(x.getDate() - ((x.getDay() + 6) % 7));
  return x;
};
const addDays = (iso: string, n: number) => {
  const [y, m, d] = iso.split('-').map(Number);
  return toIsoDate(new Date(y, m - 1, d + n));
};
const hours = (min: number) => (min ? String(Math.round((min / 60) * 100) / 100) : '');

interface Row {
  projectId: string;
  taskId: string | null;
  note: string;
  /** Hours as typed, per day index 0–6. */
  cells: string[];
}

/** Rows of (project, task, note) with hours per day; saved as one line per filled cell. */
function toRows(sheet: Timesheet): Row[] {
  const map = new Map<string, Row>();
  for (const l of sheet.lines) {
    const k = `${l.projectId}|${l.taskId ?? ''}|${l.note ?? ''}`;
    if (!map.has(k)) map.set(k, { projectId: l.projectId, taskId: l.taskId, note: l.note ?? '', cells: Array(7).fill('') });
    const i = Math.round((Date.parse(l.date) - Date.parse(sheet.weekStart)) / 86400000);
    const row = map.get(k)!;
    row.cells[i] = hours((Number(row.cells[i]) || 0) * 60 + l.minutes);
  }
  return [...map.values()];
}

export function TimesheetPage() {
  const user = useUser();
  const approver = isApprover(user);
  const [tab, setTab] = useState<'mine' | 'approve'>('mine');
  return (
    <>
      <PageHeader title="Timesheet" description="Log your hours on projects each week and submit them for approval." />
      {approver && (
        <Tabs<'mine' | 'approve'>
          className="mb-6"
          value={tab}
          onChange={setTab}
          items={[
            { value: 'mine', label: 'My week' },
            { value: 'approve', label: 'To approve' },
          ]}
        />
      )}
      {tab === 'mine' ? <MyWeek /> : <ToApprove />}
    </>
  );
}

function MyWeek() {
  const toast = useToast();
  const qc = useQueryClient();
  const [start, setStart] = useState(() => toIsoDate(mondayOf(new Date())));
  const week = useQuery({ queryKey: qk.timesheet(start), queryFn: () => projectsApi.week(start) });
  const projects = useQuery({ queryKey: [...qk.projects, false], queryFn: () => projectsApi.list(false) });
  const [rows, setRows] = useState<Row[] | null>(null);
  const loaded = useMemo(() => (week.data ? toRows(week.data) : []), [week.data]);
  const v = rows ?? loaded;
  const locked = week.data?.status === 'Submitted' || week.data?.status === 'Approved';
  const days = Array.from({ length: 7 }, (_, i) => addDays(start, i));

  const lines = (): TimesheetLine[] =>
    v.flatMap((r) =>
      r.cells
        .map((c, i) => ({ date: days[i], projectId: r.projectId, taskId: r.taskId, minutes: Math.round((Number(c) || 0) * 60), note: r.note.trim() || null }))
        .filter((l) => l.minutes > 0 && l.projectId),
    );
  const after = (t: Timesheet) => {
    qc.setQueryData(qk.timesheet(start), t);
    setRows(null);
  };
  const save = useMutation({ mutationFn: () => projectsApi.saveWeek(start, lines()), onSuccess: (t) => (after(t), toast.success('Saved')) });
  const submit = useMutation({
    mutationFn: async () => {
      if (rows) await projectsApi.saveWeek(start, lines());
      return projectsApi.submit(start);
    },
    onSuccess: (t) => (after(t), toast.success('Submitted for approval')),
  });
  const withdraw = useMutation({ mutationFn: () => projectsApi.withdraw(start), onSuccess: after, onError: (e) => toast.error(e) });
  const move = (n: number) => {
    setRows(null);
    setStart(addDays(start, n * 7));
  };
  const edit = (i: number, patch: Partial<Row>) => setRows(v.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const dayTotal = (i: number) => v.reduce((t, r) => t + (Number(r.cells[i]) || 0), 0);
  const total = days.reduce((t, _, i) => t + dayTotal(i), 0);
  const project = (id: string) => projects.data?.find((p) => p.id === id);
  const error = save.error ?? submit.error;

  return (
    <Card>
      <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <IconButton label="Previous week" onClick={() => move(-1)}>
          <ChevronLeft className="size-4" />
        </IconButton>
        <span className="text-sm font-medium">
          {formatDate(start)} – {formatDate(days[6])}
        </span>
        <IconButton label="Next week" onClick={() => move(1)}>
          <ChevronRight className="size-4" />
        </IconButton>
        {week.data && <StatusBadge status={week.data.status} label={week.data.status === 'Rejected' ? 'Sent back' : undefined} />}
        <span className="ms-auto text-sm tabular-nums text-zinc-500 dark:text-zinc-400">{Math.round(total * 100) / 100} h this week</span>
        {locked ? (
          week.data?.status === 'Submitted' && (
            <Button size="sm" variant="secondary" loading={withdraw.isPending} onClick={() => withdraw.mutate()}>
              Withdraw
            </Button>
          )
        ) : (
          <>
            <Button size="sm" variant="secondary" disabled={!rows} loading={save.isPending} onClick={() => save.mutate()}>
              Save
            </Button>
            <Button size="sm" disabled={!total} loading={submit.isPending} onClick={() => submit.mutate()}>
              Submit
            </Button>
          </>
        )}
      </div>
      {week.data?.decisionNote && (
        <p className="border-b border-zinc-100 px-5 py-2 text-sm text-amber-700 dark:border-zinc-800 dark:text-amber-300">“{week.data.decisionNote}”</p>
      )}
      <FormError message={error ? errorMessage(error) : null} />
      {week.isLoading ? (
        <SkeletonRows rows={4} cols={8} />
      ) : week.isError ? (
        <ErrorState error={week.error} onRetry={() => week.refetch()} />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[56rem] text-sm">
            <thead>
              <tr className="text-start text-xs text-zinc-500 dark:text-zinc-400">
                <th className="px-4 py-2 font-medium">Project and task</th>
                {days.map((d) => (
                  <th key={d} className="w-20 px-1 py-2 text-center font-medium">
                    {new Date(d + 'T00:00').toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric' })}
                    {!!week.data?.attendanceMinutes[d] && <div className="font-normal text-zinc-500 dark:text-zinc-400">at work {hours(week.data.attendanceMinutes[d])}h</div>}
                  </th>
                ))}
                <th className="w-10" />
              </tr>
            </thead>
            <tbody>
              {v.map((r, i) => {
                const p = project(r.projectId);
                return (
                  <tr key={i} className="border-t border-zinc-100 align-top dark:border-zinc-800">
                    <td className="space-y-1.5 px-4 py-2">
                      <Select aria-label="Project" disabled={locked} value={r.projectId} onChange={(e) => edit(i, { projectId: e.target.value, taskId: null })}>
                        <option value="">Choose project…</option>
                        {(projects.data ?? []).map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.name}
                          </option>
                        ))}
                      </Select>
                      {!!p?.tasks.length && (
                        <Select aria-label="Task" disabled={locked} value={r.taskId ?? ''} onChange={(e) => edit(i, { taskId: e.target.value || null })}>
                          <option value="">No task</option>
                          {p.tasks
                            .filter((t) => !t.isDone || t.id === r.taskId)
                            .map((t) => (
                              <option key={t.id} value={t.id}>
                                {t.name}
                              </option>
                            ))}
                        </Select>
                      )}
                      <Textarea
                        rows={1}
                        disabled={locked}
                        placeholder="What you did (optional)"
                        value={r.note}
                        maxLength={500}
                        onChange={(e) => edit(i, { note: e.target.value })}
                      />
                    </td>
                    {r.cells.map((c, j) => (
                      <td key={j} className="px-1 py-2">
                        <Input
                          aria-label={`Hours on ${days[j]}`}
                          className="text-center"
                          type="number"
                          min={0}
                          max={24}
                          step={0.25}
                          disabled={locked}
                          value={c}
                          onChange={(e) => edit(i, { cells: r.cells.map((x, k) => (k === j ? e.target.value : x)) })}
                        />
                      </td>
                    ))}
                    <td className="py-2">
                      {!locked && (
                        <IconButton label="Remove row" onClick={() => setRows(v.filter((_, j) => j !== i))}>
                          <Trash2 className="size-4" />
                        </IconButton>
                      )}
                    </td>
                  </tr>
                );
              })}
              <tr className="border-t border-zinc-200 text-xs text-zinc-500 dark:text-zinc-400 dark:border-zinc-700">
                <td className="px-4 py-2">
                  {!locked && (
                    <Button
                      size="sm"
                      variant="ghost"
                      icon={<Plus className="size-4" />}
                      disabled={!projects.data?.length}
                      onClick={() => setRows([...v, { projectId: projects.data?.[0]?.id ?? '', taskId: null, note: '', cells: Array(7).fill('') }])}
                    >
                      Add row
                    </Button>
                  )}
                  {!projects.data?.length && <span>You are not on any project yet.</span>}
                </td>
                {days.map((d, i) => (
                  <td key={d} className="py-2 text-center tabular-nums">
                    {dayTotal(i) ? `${Math.round(dayTotal(i) * 100) / 100} h` : ''}
                  </td>
                ))}
                <td />
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function ToApprove() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: qk.timesheetApprovals, queryFn: () => projectsApi.approvals() });
  const projects = useQuery({ queryKey: [...qk.projects, true], queryFn: () => projectsApi.list(true) });
  const [notes, setNotes] = useState<Record<string, string>>({});
  const decide = useMutation({
    mutationFn: ({ id, approve }: { id: string; approve: boolean }) =>
      approve ? projectsApi.approve(id, notes[id] || null) : projectsApi.reject(id, notes[id] || null),
    onSuccess: (_, { approve }) => {
      toast.success(approve ? 'Approved' : 'Sent back');
      qc.invalidateQueries({ queryKey: qk.timesheetApprovals });
      qc.invalidateQueries({ queryKey: qk.approvals });
    },
    onError: (e) => toast.error(e),
  });
  const pname = (id: string) => projects.data?.find((p) => p.id === id)?.name ?? '';
  if (list.isLoading) return <SkeletonRows rows={4} cols={4} />;
  if (!list.data?.length) return <EmptyState icon={ClipboardList} title="No timesheets waiting" />;
  return (
    <div className="space-y-4">
      {list.data.map((t) => {
        const byProject = Object.entries(
          t.lines.reduce<Record<string, number>>((acc, l) => ({ ...acc, [l.projectId]: (acc[l.projectId] ?? 0) + l.minutes }), {}),
        );
        const atWork = Object.values(t.attendanceMinutes).reduce((a, b) => a + b, 0);
        return (
          <Card key={t.id}>
            <CardHeader
              title={`${t.employeeName} · week of ${formatDate(t.weekStart)}`}
              description={`${hours(t.totalMinutes)} h logged${atWork ? ` · ${hours(atWork)} h at work by attendance` : ''}`}
            />
            <div className="space-y-3 px-5 pb-4">
              <div className="flex flex-wrap gap-2">
                {byProject.map(([pid, min]) => (
                  <Badge key={pid}>
                    {pname(pid)}: {hours(min)} h
                  </Badge>
                ))}
              </div>
              <ul className="text-sm text-zinc-600 dark:text-zinc-300">
                {t.lines.map((l, i) => (
                  <li key={i}>
                    {formatDate(l.date)} · {pname(l.projectId)} · {hours(l.minutes)} h{l.note ? ` · ${l.note}` : ''}
                  </li>
                ))}
              </ul>
              <div className="flex flex-wrap items-center gap-2">
                <Input
                  className="flex-1"
                  placeholder="Note (optional)"
                  value={notes[t.id!] ?? ''}
                  onChange={(e) => setNotes({ ...notes, [t.id!]: e.target.value })}
                />
                <Button variant="secondary" onClick={() => decide.mutate({ id: t.id!, approve: false })}>
                  Send back
                </Button>
                <Button onClick={() => decide.mutate({ id: t.id!, approve: true })}>Approve</Button>
              </div>
            </div>
          </Card>
        );
      })}
    </div>
  );
}
