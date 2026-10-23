import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { CalendarDays, Clock, MapPin, Phone, Plus, UserRound } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  ASSIGNEE_LABELS,
  onboardingApi,
  TASK_CATEGORY_LABELS,
  type OnboardingAssignee,
  type OnboardingDetail,
  type OnboardingTask,
  type OnboardingTaskCategory,
} from '../../api/onboarding';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Badge, Button, Card, CardBody, CardHeader, ConfirmDialog, ErrorState, Field, FormError, Input, Modal, PageHeader, PageSpinner, Select, StatusBadge, useToast } from '../../components/ui';
import { formatDate, formatTime, todayIso } from '../../lib/format';
import { TaskList } from './shared';

export function OnboardingDetailPage() {
  const { id = '' } = useParams();
  const hr = isHr(useUser());
  const toast = useToast();
  const qc = useQueryClient();
  const detail = useQuery({ queryKey: qk.onboardingDetail(id), queryFn: () => onboardingApi.get(id) });
  const [adding, setAdding] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [deleting, setDeleting] = useState<OnboardingTask | null>(null);
  const cancel = useMutation({
    mutationFn: () => onboardingApi.cancel(id),
    onSuccess: () => {
      toast.success('Onboarding cancelled');
      setCancelling(false);
      qc.invalidateQueries({ queryKey: qk.onboarding });
    },
    onError: (err) => toast.error(err),
  });
  const remove = useMutation({
    mutationFn: (t: OnboardingTask) => onboardingApi.deleteTask(t.id),
    onSuccess: () => {
      setDeleting(null);
      qc.invalidateQueries({ queryKey: qk.onboarding });
    },
    onError: (err) => toast.error(err),
  });
  if (detail.isLoading) return <PageSpinner />;
  if (detail.isError || !detail.data)
    return (
      <>
        <PageHeader title="Onboarding" back={{ to: '/onboarding', label: 'Onboarding' }} />
        <Card>
          <ErrorState error={detail.error} onRetry={() => detail.refetch()} />
        </Card>
      </>
    );
  const d = detail.data;
  const s = d.summary;
  const active = s.status === 'InProgress';
  return (
    <>
      <PageHeader
        back={{ to: '/onboarding', label: 'Onboarding' }}
        title={s.employeeName}
        description={
          <span className="flex flex-wrap items-center gap-2">
            {[s.designation, s.department].filter(Boolean).join(' · ')} · joins {formatDate(s.joiningDate)} <StatusBadge status={s.status === 'InProgress' ? 'Pending' : s.status === 'Completed' ? 'Approved' : 'Cancelled'} label={s.status === 'InProgress' ? 'In progress' : s.status} />
          </span>
        }
        actions={
          hr &&
          active && (
            <>
              <Button variant="ghost" onClick={() => setCancelling(true)}>
                Cancel onboarding
              </Button>
              <Button icon={<Plus className="size-4" />} onClick={() => setAdding(true)}>
                Add task
              </Button>
            </>
          )
        }
      />
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <Card>
          <CardHeader title="Tasks" description={`${s.done} of ${s.total} done${s.overdue ? ` · ${s.overdue} late` : ''}`} />
          {d.tasks.length ? <TaskList tasks={d.tasks} onDelete={hr && active ? setDeleting : undefined} /> : <CardBody className="text-sm text-zinc-500">No tasks yet.</CardBody>}
        </Card>
        <div className="space-y-4">
          <FirstDay d={d} />
          {d.introduction && (
            <Card>
              <CardHeader title="Their introduction" />
              <CardBody className="space-y-2 text-sm">
                {Object.entries(d.introduction)
                  .filter(([, v]) => v)
                  .map(([k, v]) => (
                    <p key={k} className="text-zinc-700 dark:text-zinc-300">
                      {v}
                    </p>
                  ))}
              </CardBody>
            </Card>
          )}
        </div>
      </div>
      {adding && <AddTaskModal d={d} onClose={() => setAdding(false)} />}
      <ConfirmDialog
        open={cancelling}
        title="Cancel this onboarding?"
        message="Open tasks stop. The employee record stays."
        confirmLabel="Cancel onboarding"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onClose={() => setCancelling(false)}
      />
      <ConfirmDialog
        open={!!deleting}
        title="Remove this task?"
        message={deleting?.title ?? ''}
        confirmLabel="Remove"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}

export function FirstDay({ d }: { d: OnboardingDetail }) {
  const row = (Icon: typeof Clock, label: string, value: React.ReactNode) => (
    <div className="flex items-start gap-3">
      <Icon className="mt-0.5 size-4 shrink-0 text-zinc-400" />
      <div>
        <p className="text-xs text-zinc-500">{label}</p>
        <div className="text-sm text-zinc-900 dark:text-zinc-100">{value}</div>
      </div>
    </div>
  );
  return (
    <Card>
      <CardHeader title="First day" />
      <CardBody className="space-y-3">
        {row(CalendarDays, 'Date', formatDate(d.summary.joiningDate))}
        {d.reportingTime && row(Clock, 'Report at', formatTime(d.reportingTime))}
        {d.reportingPlace && row(MapPin, 'Where', d.reportingPlace)}
        {d.manager && row(UserRound, 'Manager', d.manager)}
        {d.buddy &&
          row(
            Phone,
            'Buddy',
            <>
              {d.buddy}
              {d.buddyPhone && (
                <a href={`tel:${d.buddyPhone}`} className="ml-2 text-accent-600 hover:underline dark:text-accent-400">
                  {d.buddyPhone}
                </a>
              )}
            </>,
          )}
        {d.instructions && <p className="rounded-lg bg-zinc-50 p-3 text-sm whitespace-pre-line text-zinc-700 dark:bg-zinc-800/60 dark:text-zinc-300">{d.instructions}</p>}
        {d.summary.introduced && <Badge tone="green">Introduced to the team</Badge>}
      </CardBody>
    </Card>
  );
}

function AddTaskModal({ d, onClose }: { d: OnboardingDetail; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const [title, setTitle] = useState('');
  const [category, setCategory] = useState<OnboardingTaskCategory>('Other');
  const [kind, setKind] = useState<OnboardingAssignee>('Hr');
  const [who, setWho] = useState('');
  const [due, setDue] = useState(d.summary.joiningDate > todayIso() ? d.summary.joiningDate : todayIso());
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => onboardingApi.addTask(d.summary.id, { title: title.trim(), category, assigneeKind: kind, assigneeEmployeeId: kind === 'SpecificEmployee' ? who || null : null, dueOn: due }),
    onSuccess: () => {
      toast.success('Task added');
      qc.invalidateQueries({ queryKey: qk.onboarding });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title="Add a task"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => (title.trim() ? save.mutate() : setError('Name the task.'))}>
            Add
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Task" required className="col-span-2">
          <Input value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} />
        </Field>
        <Field label="Category">
          <Select value={category} onChange={(e) => setCategory(e.target.value as OnboardingTaskCategory)}>
            {(Object.keys(TASK_CATEGORY_LABELS) as OnboardingTaskCategory[]).map((c) => (
              <option key={c} value={c}>
                {TASK_CATEGORY_LABELS[c]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Due">
          <Input type="date" value={due} onChange={(e) => setDue(e.target.value)} />
        </Field>
        <Field label="Who" className="col-span-2">
          <Select value={kind} onChange={(e) => setKind(e.target.value as OnboardingAssignee)}>
            {(Object.keys(ASSIGNEE_LABELS) as OnboardingAssignee[]).map((a) => (
              <option key={a} value={a}>
                {ASSIGNEE_LABELS[a]}
              </option>
            ))}
          </Select>
        </Field>
        {kind === 'SpecificEmployee' && (
          <Field label="Person" className="col-span-2">
            <Select value={who} onChange={(e) => setWho(e.target.value)}>
              <option value="">Choose…</option>
              {people.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
      </div>
    </Modal>
  );
}
