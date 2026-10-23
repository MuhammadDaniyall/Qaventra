import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Circle, FolderKanban, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { projectsApi, type Project, type ProjectStatus, type SaveProject, type SaveTask } from '../../api/projects';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  Drawer,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  PageHeader,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, formatNumber } from '../../lib/format';

const STATUS_TONE: Record<ProjectStatus, 'green' | 'amber' | 'gray'> = { Active: 'green', OnHold: 'amber', Closed: 'gray' };

/** Projects people log hours against: team, tasks, hours against budget and cost. */
export function ProjectsPage() {
  const user = useUser();
  const hr = isHr(user);
  const navigate = useNavigate();
  const [showClosed, setShowClosed] = useState(false);
  const list = useQuery({ queryKey: [...qk.projects, showClosed], queryFn: () => projectsApi.list(showClosed) });
  const [open, setOpen] = useState<string | 'new' | null>(null);
  const current = list.data?.find((p) => p.id === open) ?? null;

  return (
    <>
      <PageHeader
        title="Projects"
        description="Client and internal work. Teams log their hours on weekly timesheets."
        actions={
          <>
            <Button variant="secondary" onClick={() => navigate('/timesheets')}>
              My timesheet
            </Button>
            {hr && (
              <Button icon={<Plus className="size-4" />} onClick={() => setOpen('new')}>
                New project
              </Button>
            )}
          </>
        }
      />
      <Card>
        <div className="flex items-center justify-end border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
          <Checkbox label="Show closed" checked={showClosed} onChange={(e) => setShowClosed(e.target.checked)} />
        </div>
        {list.isLoading ? (
          <SkeletonRows rows={5} cols={6} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !list.data?.length ? (
          <EmptyState icon={FolderKanban} title="No projects yet" description={hr ? 'Add a project, its team and tasks.' : 'You are not on any project.'} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Project</Th>
                <Th>Client</Th>
                <Th>Manager</Th>
                <Th>Tasks</Th>
                <Th className="w-56">Hours</Th>
                <Th>Status</Th>
              </tr>
            </THead>
            <TBody>
              {list.data.map((p) => {
                const pct = p.budgetHours ? Math.min(100, (p.loggedHours / p.budgetHours) * 100) : 0;
                return (
                  <Tr key={p.id} className="cursor-pointer" onClick={() => setOpen(p.id)}>
                    <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                      {p.name} {p.code && <span className="font-mono text-xs text-zinc-500">{p.code}</span>}
                    </Td>
                    <Td>{p.client}</Td>
                    <Td>{p.manager}</Td>
                    <Td className="tabular-nums">
                      {p.tasks.filter((t) => t.isDone).length}/{p.tasks.length}
                    </Td>
                    <Td>
                      <div className="text-sm tabular-nums">
                        {formatNumber(p.loggedHours)} h{p.budgetHours ? ` of ${formatNumber(p.budgetHours)}` : ''}
                      </div>
                      {!!p.budgetHours && (
                        <div className="mt-1 h-1.5 rounded-full bg-zinc-100 dark:bg-zinc-800">
                          <div
                            className={cn('h-1.5 rounded-full', pct >= 100 ? 'bg-red-500' : pct > 80 ? 'bg-amber-500' : 'bg-emerald-500')}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      )}
                    </Td>
                    <Td>
                      <Badge tone={STATUS_TONE[p.status]}>{p.status === 'OnHold' ? 'On hold' : p.status}</Badge>
                    </Td>
                  </Tr>
                );
              })}
            </TBody>
          </Table>
        )}
      </Card>
      {(open === 'new' || current) && (
        <ProjectDrawer key={open} project={open === 'new' ? null : current} onClose={() => setOpen(null)} onCreated={(id) => setOpen(id)} />
      )}
    </>
  );
}

function ProjectDrawer({ project, onClose, onCreated }: { project: Project | null; onClose: () => void; onCreated: (id: string) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const user = useUser();
  const hr = isHr(user);
  const people = useEmployeeLookup();
  const editable = !project || project.canManage;
  const [v, setV] = useState<SaveProject>(
    project
      ? {
          name: project.name,
          code: project.code,
          client: project.client,
          description: project.description,
          managerEmployeeId: project.managerEmployeeId,
          startDate: project.startDate,
          endDate: project.endDate,
          budgetHours: project.budgetHours,
          billable: project.billable,
          openToAll: project.openToAll,
          status: project.status,
          members: project.members,
        }
      : {
          name: '',
          code: null,
          client: null,
          description: null,
          managerEmployeeId: null,
          startDate: null,
          endDate: null,
          budgetHours: null,
          billable: true,
          openToAll: false,
          status: 'Active',
          members: [],
        },
  );
  const [addMember, setAddMember] = useState('');
  const set = (p: Partial<SaveProject>) => setV((x) => ({ ...x, ...p }));
  const txt = (s: string) => (s.trim() === '' ? null : s);
  const save = useMutation({
    mutationFn: () => (project ? projectsApi.update(project.id, v) : projectsApi.add(v)),
    onSuccess: (p) => {
      toast.success('Project saved');
      qc.invalidateQueries({ queryKey: qk.projects });
      if (!project) onCreated(p.id);
    },
  });
  const remove = useMutation({
    mutationFn: () => projectsApi.remove(project!.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.projects });
      onClose();
    },
    onError: (e) => toast.error(e),
  });
  const nameOf = (id: string) => people.data?.find((p) => p.id === id)?.name ?? '';

  return (
    <Drawer
      open
      onClose={onClose}
      title={project ? project.name : 'New project'}
      description={project ? `${formatNumber(project.loggedHours)} hours logged${project.cost ? ` · cost ${formatNumber(project.cost)}` : ''}` : undefined}
      footer={
        editable && (
          <div className="flex w-full items-center justify-between">
            {project && hr ? (
              <Button variant="ghost" onClick={() => remove.mutate()} loading={remove.isPending}>
                Delete
              </Button>
            ) : (
              <span />
            )}
            <Button loading={save.isPending} disabled={!v.name.trim()} onClick={() => save.mutate()}>
              {project ? 'Save' : 'Create project'}
            </Button>
          </div>
        )
      }
    >
      <fieldset disabled={!editable} className="space-y-4">
        <FormError message={save.isError ? errorMessage(save.error) : null} />
        <div className="grid grid-cols-[1fr_8rem] gap-3">
          <Field label="Name" required>
            <Input value={v.name} maxLength={150} onChange={(e) => set({ name: e.target.value })} />
          </Field>
          <Field label="Code">
            <Input value={v.code ?? ''} maxLength={30} onChange={(e) => set({ code: txt(e.target.value) })} />
          </Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Client">
            <Input value={v.client ?? ''} maxLength={150} onChange={(e) => set({ client: txt(e.target.value) })} />
          </Field>
          <Field label="Manager">
            <Select value={v.managerEmployeeId ?? ''} disabled={!hr} onChange={(e) => set({ managerEmployeeId: e.target.value || null })}>
              <option value="">No one</option>
              {(people.data ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Starts">
            <Input type="date" value={v.startDate ?? ''} onChange={(e) => set({ startDate: txt(e.target.value) })} />
          </Field>
          <Field label="Ends">
            <Input type="date" value={v.endDate ?? ''} onChange={(e) => set({ endDate: txt(e.target.value) })} />
          </Field>
          <Field label="Budget (hours)">
            <Input type="number" min={0} value={v.budgetHours ?? ''} onChange={(e) => set({ budgetHours: e.target.value ? Number(e.target.value) : null })} />
          </Field>
          <Field label="Status">
            <Select value={v.status} onChange={(e) => set({ status: e.target.value as ProjectStatus })}>
              <option value="Active">Active</option>
              <option value="OnHold">On hold</option>
              <option value="Closed">Closed</option>
            </Select>
          </Field>
        </div>
        <Field label="Description">
          <Textarea rows={2} value={v.description ?? ''} maxLength={2000} onChange={(e) => set({ description: txt(e.target.value) })} />
        </Field>
        <div className="flex flex-wrap gap-4">
          <Checkbox label="Billable" checked={v.billable} onChange={(e) => set({ billable: e.target.checked })} />
          <Checkbox label="Anyone can log hours (not just the team)" checked={v.openToAll} onChange={(e) => set({ openToAll: e.target.checked })} />
        </div>
        <div>
          <h3 className="mb-2 text-sm font-medium">Team</h3>
          <div className="space-y-2">
            {v.members.map((m, i) => (
              <div key={m.employeeId} className="flex items-center gap-2">
                <span className="min-w-0 flex-1 truncate text-sm">{m.name ?? nameOf(m.employeeId)}</span>
                <div className="w-32 shrink-0">
                  <Input
                    type="number"
                    min={0}
                    placeholder="Rate / hour"
                    aria-label="Hourly rate"
                    value={m.hourlyRate ?? ''}
                    onChange={(e) =>
                      set({ members: v.members.map((x, j) => (j === i ? { ...x, hourlyRate: e.target.value ? Number(e.target.value) : null } : x)) })
                    }
                  />
                </div>
                <IconButton label="Remove" onClick={() => set({ members: v.members.filter((_, j) => j !== i) })}>
                  <Trash2 className="size-4" />
                </IconButton>
              </div>
            ))}
            {editable && (
              <Select
                aria-label="Add to team"
                value={addMember}
                onChange={(e) => {
                  const id = e.target.value;
                  setAddMember('');
                  if (id && !v.members.some((m) => m.employeeId === id))
                    set({ members: [...v.members, { employeeId: id, name: nameOf(id), hourlyRate: null }] });
                }}
              >
                <option value="">Add someone to the team…</option>
                {(people.data ?? [])
                  .filter((p) => !v.members.some((m) => m.employeeId === p.id))
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
              </Select>
            )}
          </div>
        </div>
      </fieldset>
      {project && <Tasks project={project} />}
    </Drawer>
  );
}

function Tasks({ project }: { project: Project }) {
  const toast = useToast();
  const qc = useQueryClient();
  const user = useUser();
  const people = useEmployeeLookup();
  const blank: SaveTask = { name: '', assigneeEmployeeId: null, dueDate: null, estimatedHours: null, isDone: false };
  const [draft, setDraft] = useState<SaveTask>(blank);
  const refresh = () => qc.invalidateQueries({ queryKey: qk.projects });
  const add = useMutation({
    mutationFn: () => projectsApi.addTask(project.id, { ...draft, name: draft.name.trim() }),
    onSuccess: () => {
      setDraft(blank);
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const toggle = useMutation({
    mutationFn: (t: Project['tasks'][number]) =>
      projectsApi.updateTask(project.id, t.id, {
        name: t.name,
        assigneeEmployeeId: t.assigneeEmployeeId,
        dueDate: t.dueDate,
        estimatedHours: t.estimatedHours,
        isDone: !t.isDone,
      }),
    onSuccess: refresh,
    onError: (e) => toast.error(e),
  });
  const remove = useMutation({ mutationFn: (id: string) => projectsApi.deleteTask(project.id, id), onSuccess: refresh, onError: (e) => toast.error(e) });
  return (
    <Card className="mt-6">
      <CardHeader title="Tasks" description="Optional: people can log hours against a task." />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {project.tasks.map((t) => {
          const mine = project.canManage || t.assigneeEmployeeId === user.employeeId;
          return (
            <li key={t.id} className="flex items-center gap-3 px-5 py-2.5 text-sm">
              <button
                type="button"
                disabled={!mine}
                aria-label={t.isDone ? 'Mark open' : 'Mark done'}
                onClick={() => toggle.mutate(t)}
                className="disabled:opacity-50"
              >
                {t.isDone ? <CheckCircle2 className="size-4 text-emerald-500" /> : <Circle className="size-4 text-zinc-400" />}
              </button>
              <span className={cn('flex-1', t.isDone && 'text-zinc-400 line-through')}>{t.name}</span>
              <span className="text-xs text-zinc-500">{t.assignee}</span>
              {t.dueDate && <span className="text-xs text-zinc-500">due {formatDate(t.dueDate)}</span>}
              <span className="text-xs tabular-nums text-zinc-500">
                {formatNumber(t.loggedHours)}
                {t.estimatedHours ? `/${formatNumber(t.estimatedHours)}` : ''} h
              </span>
              {project.canManage && (
                <IconButton label="Delete task" onClick={() => remove.mutate(t.id)}>
                  <Trash2 className="size-4" />
                </IconButton>
              )}
            </li>
          );
        })}
        {!project.tasks.length && <li className="px-5 py-3 text-sm text-zinc-500">No tasks.</li>}
      </ul>
      {project.canManage && (
        <div className="grid gap-2 border-t border-zinc-100 p-4 sm:grid-cols-[1fr_10rem_9rem_6rem_auto] dark:border-zinc-800">
          <Input placeholder="New task" aria-label="Task" value={draft.name} maxLength={200} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
          <Select
            aria-label="Assignee"
            value={draft.assigneeEmployeeId ?? ''}
            onChange={(e) => setDraft({ ...draft, assigneeEmployeeId: e.target.value || null })}
          >
            <option value="">Anyone</option>
            {(people.data ?? [])
              .filter((p) => project.openToAll || project.members.some((m) => m.employeeId === p.id))
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
          </Select>
          <Input type="date" aria-label="Due" value={draft.dueDate ?? ''} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value || null })} />
          <Input
            type="number"
            min={0}
            placeholder="Hours"
            aria-label="Estimated hours"
            value={draft.estimatedHours ?? ''}
            onChange={(e) => setDraft({ ...draft, estimatedHours: e.target.value ? Number(e.target.value) : null })}
          />
          <Button size="sm" disabled={!draft.name.trim()} loading={add.isPending} onClick={() => add.mutate()}>
            Add
          </Button>
        </div>
      )}
    </Card>
  );
}
