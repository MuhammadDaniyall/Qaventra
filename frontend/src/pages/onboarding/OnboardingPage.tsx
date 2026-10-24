import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ClipboardCheck, ListChecks, Plus, Rocket, Trash2, UserPlus, Users } from 'lucide-react';
import { assetsApi } from '../../api/assets';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  ASSIGNEE_LABELS,
  onboardingApi,
  TASK_CATEGORY_LABELS,
  type OnboardingAssignee,
  type OnboardingTaskCategory,
  type OnboardingTemplate,
  type TemplateTask,
} from '../../api/onboarding';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Badge,
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
  Table,
  TBody,
  Tabs,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, timeFromInput } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';
import { TaskList } from './shared';

type Tab = 'active' | 'tasks' | 'completed' | 'templates';

export function OnboardingPage() {
  const hr = isHr(useUser());
  const [params, setParams] = useSearchParams();
  const raw = (params.get('tab') as Tab | null) ?? (hr ? 'active' : 'tasks');
  const tab: Tab = hr ? raw : 'tasks';
  const [starting, setStarting] = useState(false);
  return (
    <>
      <PageHeader
        title="Onboarding"
        description={hr ? 'Welcome new joiners: who does what before and after their first day.' : 'Tasks you have for colleagues who are joining.'}
        actions={
          hr && (
            <Button icon={<Rocket className="size-4" />} onClick={() => setStarting(true)}>
              Start onboarding
            </Button>
          )
        }
      />
      {hr && (
        <Tabs<Tab>
          className="mb-5"
          value={tab}
          onChange={(t) => setParams(t === 'active' ? {} : { tab: t }, { replace: true })}
          items={[
            { value: 'active', label: 'In progress', icon: <Users className="size-4" /> },
            { value: 'tasks', label: 'My tasks', icon: <ListChecks className="size-4" /> },
            { value: 'completed', label: 'Completed', icon: <ClipboardCheck className="size-4" /> },
            { value: 'templates', label: 'Templates', icon: <ClipboardCheck className="size-4" /> },
          ]}
        />
      )}
      {tab === 'active' && <List status="InProgress" />}
      {tab === 'completed' && <List status="Completed" />}
      {tab === 'tasks' && <MyTasks />}
      {tab === 'templates' && <Templates />}
      {starting && <StartModal onClose={() => setStarting(false)} />}
    </>
  );
}

function List({ status }: { status: 'InProgress' | 'Completed' }) {
  const navigate = useNavigate();
  const list = useQuery({ queryKey: [...qk.onboarding, 'list', status], queryFn: () => onboardingApi.list(status) });
  if (list.isLoading) return <Card><SkeletonRows rows={4} cols={4} /></Card>;
  if (list.isError) return <Card><ErrorState error={list.error} onRetry={() => list.refetch()} /></Card>;
  if (!list.data?.length)
    return (
      <Card>
        <EmptyState
          icon={UserPlus}
          title={status === 'InProgress' ? 'Nobody is being onboarded' : 'No completed onboardings yet'}
          description="Set a default template and onboarding starts by itself when you add an employee."
        />
      </Card>
    );
  return (
    <Card>
      <Table>
        <THead>
          <tr>
            <Th>New joiner</Th>
            <Th>Joins</Th>
            <Th>Progress</Th>
            <Th>Template</Th>
          </tr>
        </THead>
        <TBody>
          {list.data.map((o) => {
            const pct = o.total ? Math.round((o.done / o.total) * 100) : 100;
            return (
              <Tr key={o.id} onClick={() => navigate(`/onboarding/${o.id}`)}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{o.employeeName}</p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">{[o.designation, o.department].filter(Boolean).join(' · ') || o.employeeCode}</p>
                </Td>
                <Td className="text-sm">{formatDate(o.joiningDate)}</Td>
                <Td>
                  <div className="flex items-center gap-2">
                    <div className="h-1.5 w-24 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                      <div className="h-full rounded-full bg-emerald-500" style={{ width: `${pct}%` }} />
                    </div>
                    <span className="text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">
                      {o.done}/{o.total}
                    </span>
                    {o.overdue > 0 && <Badge tone="red">{o.overdue} late</Badge>}
                    {o.introduced && <Badge tone="green">Introduced</Badge>}
                  </div>
                </Td>
                <Td className="text-sm text-zinc-600 dark:text-zinc-300">{o.templateName}</Td>
              </Tr>
            );
          })}
        </TBody>
      </Table>
    </Card>
  );
}

function MyTasks() {
  const [done, setDone] = useState(false);
  const tasks = useQuery({ queryKey: [...qk.onboarding, 'my-tasks', done], queryFn: () => onboardingApi.myTasks(done) });
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">{done ? 'Tasks you have finished.' : 'Things to do for people joining.'}</p>
        <Select className="w-36" value={done ? 'done' : 'todo'} onChange={(e) => setDone(e.target.value === 'done')} aria-label="Show">
          <option value="todo">To do</option>
          <option value="done">Done</option>
        </Select>
      </div>
      {tasks.isLoading ? (
        <SkeletonRows rows={4} cols={3} />
      ) : !tasks.data?.length ? (
        <EmptyState icon={ListChecks} title={done ? 'Nothing finished yet' : 'Nothing to do'} description="Onboarding tasks assigned to you appear here." />
      ) : (
        <TaskList tasks={tasks.data} showFor />
      )}
    </Card>
  );
}

function StartModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const people = useEmployeeLookup();
  const templates = useQuery({ queryKey: qk.onboardingTemplates, queryFn: onboardingApi.templates });
  const [employeeId, setEmployeeId] = useState('');
  const [templateId, setTemplateId] = useState('');
  const [time, setTime] = useState('09:00');
  const [place, setPlace] = useState('');
  const [buddy, setBuddy] = useState('');
  const [instructions, setInstructions] = useState('');
  const [error, setError] = useState<string | null>(null);
  const start = useMutation({
    mutationFn: () =>
      onboardingApi.start({
        employeeId,
        templateId: templateId || null,
        reportingTime: time ? timeFromInput(time) : null,
        reportingPlace: place.trim() || null,
        buddyEmployeeId: buddy || null,
        instructions: instructions.trim() || null,
      }),
    onSuccess: (d) => {
      toast.success('Onboarding started', `${d.summary.total} task${d.summary.total === 1 ? '' : 's'} shared out.`);
      qc.invalidateQueries({ queryKey: qk.onboarding });
      onClose();
      navigate(`/onboarding/${d.summary.id}`);
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Start onboarding"
      description="Everyone involved gets their tasks, and the new joiner gets a welcome page."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={start.isPending} onClick={() => (employeeId ? start.mutate() : setError('Choose the new joiner.'))}>
            Start
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="New joiner" required>
          <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
            <option value="">Choose…</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Template">
          <Select value={templateId} onChange={(e) => setTemplateId(e.target.value)}>
            <option value="">No tasks yet</option>
            {templates.data?.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name} ({t.tasks.length} tasks)
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Report at">
          <Input type="time" value={time} onChange={(e) => setTime(e.target.value)} />
        </Field>
        <Field label="Where">
          <Input value={place} maxLength={300} onChange={(e) => setPlace(e.target.value)} placeholder="3rd floor reception" />
        </Field>
        <Field label="Buddy" hint="Shows them around; gets the buddy tasks" className="sm:col-span-2">
          <Select value={buddy} onChange={(e) => setBuddy(e.target.value)}>
            <option value="">No buddy (their manager does buddy tasks)</option>
            {people.data?.filter((p) => p.id !== employeeId).map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="First-day instructions" hint="Blank uses the template's welcome message" className="sm:col-span-2">
          <Textarea rows={3} maxLength={4000} value={instructions} onChange={(e) => setInstructions(e.target.value)} placeholder="Bring your CNIC and two photos. Parking is behind the building." />
        </Field>
      </div>
    </Modal>
  );
}

function Templates() {
  const list = useQuery({ queryKey: qk.onboardingTemplates, queryFn: onboardingApi.templates });
  const [editing, setEditing] = useState<OnboardingTemplate | 'new' | null>(null);
  const [deleting, setDeleting] = useState<OnboardingTemplate | null>(null);
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">Reusable task lists. The default one starts by itself for every new employee.</p>
        <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          Add template
        </Button>
      </div>
      {!list.data?.length ? (
        <EmptyState icon={ClipboardCheck} title="No templates" description="E.g. a standard joiner checklist: offer pack, laptop, accounts, office tour, first-week plan." />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data.map((t) => (
            <li key={t.id} className="flex items-center justify-between gap-3 px-5 py-3">
              <div>
                <p className="font-medium text-zinc-900 dark:text-zinc-100">
                  {t.name} {t.isDefault && <Badge tone="accent">Default</Badge>}
                </p>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">
                  {t.tasks.length} task{t.tasks.length === 1 ? '' : 's'}
                  {t.description ? ` · ${t.description}` : ''}
                </p>
              </div>
              <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
            </li>
          ))}
        </ul>
      )}
      {editing && <TemplateModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Template" remove={onboardingApi.deleteTemplate} invalidate={[qk.onboarding]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

const blankTask = (): TemplateTask => ({ id: null, title: '', description: null, category: 'Paperwork', assigneeKind: 'Hr', assigneeEmployeeId: null, dueOffsetDays: 0 });

function TemplateModal({ item, onClose }: { item: OnboardingTemplate | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const kits = useQuery({ queryKey: qk.assetKits, queryFn: assetsApi.kits });
  const [name, setName] = useState(item?.name ?? '');
  const [description, setDescription] = useState(item?.description ?? '');
  const [isDefault, setIsDefault] = useState(item?.isDefault ?? false);
  const [kit, setKit] = useState(item?.assetKitId ?? '');
  const [welcome, setWelcome] = useState(item?.welcomeMessage ?? '');
  const [tasks, setTasks] = useState<TemplateTask[]>(item?.tasks.length ? item.tasks : [blankTask()]);
  const [error, setError] = useState<string | null>(null);
  const set = (i: number, p: Partial<TemplateTask>) => setTasks((t) => t.map((x, j) => (j === i ? { ...x, ...p } : x)));
  const save = useMutation({
    mutationFn: () => {
      const b = {
        name: name.trim(),
        description: description.trim() || null,
        isDefault,
        assetKitId: kit || null,
        welcomeMessage: welcome.trim() || null,
        tasks: tasks.filter((t) => t.title.trim()).map((t) => ({ ...t, title: t.title.trim() })),
      };
      return item ? onboardingApi.updateTemplate(item.id, b) : onboardingApi.createTemplate(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Template saved' : 'Template added');
      qc.invalidateQueries({ queryKey: qk.onboarding });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={item ? 'Edit template' : 'Add template'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => (name.trim() ? save.mutate() : setError('Name the template.'))}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        <FormError message={error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Name" required>
            <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Standard joiner" />
          </Field>
          <Field label="Starter kit" hint="Queued for hand-over when onboarding starts">
            <Select value={kit} onChange={(e) => setKit(e.target.value)}>
              <option value="">None</option>
              {kits.data?.map((k) => (
                <option key={k.id} value={k.id}>
                  {k.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Description" className="sm:col-span-2">
            <Input value={description} maxLength={500} onChange={(e) => setDescription(e.target.value)} />
          </Field>
          <Field label="Welcome message" hint="Shown on the new joiner's welcome page" className="sm:col-span-2">
            <Textarea rows={2} maxLength={4000} value={welcome} onChange={(e) => setWelcome(e.target.value)} />
          </Field>
          <Checkbox className="sm:col-span-2" label="Default" description="Start this automatically for every employee you add." checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} />
        </div>
        <div>
          <p className="mb-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Tasks</p>
          <div className="space-y-2">
            {tasks.map((t, i) => (
              <div key={i} className="grid grid-cols-2 gap-2 rounded-lg border border-zinc-200 p-3 md:grid-cols-[2fr_1fr_1fr_90px_auto] dark:border-zinc-800">
                <Input className="col-span-2 md:col-span-1" value={t.title} maxLength={200} placeholder="Task" onChange={(e) => set(i, { title: e.target.value })} aria-label="Task" />
                <Select value={t.category} onChange={(e) => set(i, { category: e.target.value as OnboardingTaskCategory })} aria-label="Category">
                  {(Object.keys(TASK_CATEGORY_LABELS) as OnboardingTaskCategory[]).map((c) => (
                    <option key={c} value={c}>
                      {TASK_CATEGORY_LABELS[c]}
                    </option>
                  ))}
                </Select>
                <Select value={t.assigneeKind} onChange={(e) => set(i, { assigneeKind: e.target.value as OnboardingAssignee })} aria-label="Who">
                  {(Object.keys(ASSIGNEE_LABELS) as OnboardingAssignee[]).map((a) => (
                    <option key={a} value={a}>
                      {ASSIGNEE_LABELS[a]}
                    </option>
                  ))}
                </Select>
                <Input
                  type="number"
                  min={-60}
                  max={365}
                  value={t.dueOffsetDays}
                  onChange={(e) => set(i, { dueOffsetDays: Number(e.target.value) })}
                  aria-label="Due, days from joining"
                  title="Days from the joining date (negative is before)"
                />
                <IconButton label="Remove task" onClick={() => setTasks((list) => list.filter((_, j) => j !== i))}>
                  <Trash2 className="size-4" />
                </IconButton>
                {t.assigneeKind === 'SpecificEmployee' && (
                  <Select className={cn('col-span-2 md:col-span-5')} value={t.assigneeEmployeeId ?? ''} onChange={(e) => set(i, { assigneeEmployeeId: e.target.value || null })} aria-label="Person">
                    <option value="">Who?</option>
                    {people.data?.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </Select>
                )}
              </div>
            ))}
          </div>
          <div className="mt-2 flex items-center justify-between">
            <Button variant="ghost" size="sm" icon={<Plus className="size-3.5" />} onClick={() => setTasks((t) => [...t, blankTask()])}>
              Add task
            </Button>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">The number is days from the joining date: −2 is two days before, 7 is the first week.</p>
          </div>
        </div>
      </div>
    </Modal>
  );
}
