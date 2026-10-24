import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { BookOpen, CalendarClock, ExternalLink, GraduationCap, Plus, ShieldCheck, Star } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useDepartments, useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  ENROLLMENT_LABELS,
  MODE_LABELS,
  trainingApi,
  type Compliance,
  type MyTraining,
  type TrainingMe,
  type TrainingMode,
  type TrainingProgram,
} from '../../api/training';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  StatusBadge,
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
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';
import { ComplianceBadge, Rating } from './shared';

type Tab = 'mine' | 'sessions' | 'catalog' | 'compliance' | 'requests' | 'report';

/** Courses, sessions, my training and requests for everyone; compliance for managers; catalog, requests and reports for HR. */
export function TrainingPage() {
  const me = useQuery({ queryKey: qk.trainingMe, queryFn: trainingApi.me });
  const [params, setParams] = useSearchParams();
  const [asking, setAsking] = useState<TrainingProgram | 'outside' | null>(null);
  if (me.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  const m = me.data!;
  const allowed: Tab[] = [
    'mine',
    'sessions',
    'catalog',
    ...(m.isHr || m.hasTeam ? (['compliance'] as Tab[]) : []),
    ...(m.isHr ? (['requests', 'report'] as Tab[]) : []),
  ];
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && allowed.includes(raw) ? raw : 'mine';
  const labels: Record<Tab, string> = {
    mine: 'My training',
    sessions: 'Sessions',
    catalog: 'Courses',
    compliance: m.isHr ? 'Compliance' : 'My team',
    requests: 'Requests',
    report: 'Reports',
  };
  return (
    <>
      <PageHeader
        title="Training"
        description="Courses, sessions and who is up to date on required training."
        actions={
          <Button variant="secondary" icon={<Plus className="size-4" />} onClick={() => setAsking('outside')}>
            Ask for a course
          </Button>
        }
      />
      <Tabs<Tab> className="mb-5" value={tab} onChange={(t) => setParams({ tab: t })} items={allowed.map((t) => ({ value: t, label: labels[t] }))} />
      {tab === 'mine' && <MineTab />}
      {tab === 'sessions' && <SessionsTab me={m} />}
      {tab === 'catalog' && <CatalogTab me={m} onAsk={setAsking} />}
      {tab === 'compliance' && <ComplianceTab me={m} />}
      {tab === 'requests' && <RequestsTab all />}
      {tab === 'report' && <ReportTab />}
      {asking && <RequestModal program={asking === 'outside' ? null : asking} onClose={() => setAsking(null)} />}
    </>
  );
}

// ---------- Mine ----------

function MineTab() {
  const my = useQuery({ queryKey: qk.trainingMy, queryFn: trainingApi.my });
  const compliance = useQuery({ queryKey: qk.trainingMyCompliance, queryFn: trainingApi.myCompliance });
  const [rating, setRating] = useState<MyTraining | null>(null);
  const now = Date.now();
  const at = (u: string) => new Date(/[zZ]$/.test(u) ? u : `${u}Z`).getTime();
  const upcoming = my.data?.filter((x) => x.status === 'Enrolled' && at(x.endsAtUtc) >= now) ?? [];
  const past = my.data?.filter((x) => !upcoming.includes(x)) ?? [];
  const required = compliance.data?.rows[0]?.cells ?? [];
  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_22rem]">
      <div className="space-y-5">
        <Card>
          <CardHeader title="Coming up" />
          {my.isLoading ? (
            <SkeletonRows rows={2} cols={3} />
          ) : !upcoming.length ? (
            <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">You are not booked on any session.</p>
          ) : (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {upcoming.map((x) => (
                <li key={x.enrollmentId} className="flex flex-wrap items-center gap-3 px-5 py-3">
                  <CalendarClock className="size-4 text-zinc-500 dark:text-zinc-400" />
                  <div className="min-w-0 flex-1">
                    <Link to={`/training/sessions/${x.sessionId}`} className="font-medium text-zinc-900 hover:text-accent-600 dark:text-white">
                      {x.program}
                    </Link>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {formatDateTimeUtc(x.startsAtUtc)} · {MODE_LABELS[x.mode]}
                      {x.venue ? ` · ${x.venue}` : ''}
                      {x.trainer ? ` · ${x.trainer}` : ''}
                    </p>
                  </div>
                  {x.link && (
                    <a
                      href={x.link}
                      target="_blank"
                      rel="noreferrer noopener"
                      className="inline-flex items-center gap-1 text-sm text-accent-600 hover:underline"
                    >
                      Material <ExternalLink className="size-3" />
                    </a>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card>
          <CardHeader title="History" />
          {!past.length ? (
            <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">No training yet.</p>
          ) : (
            <Table>
              <THead>
                <Tr>
                  <Th>Course</Th>
                  <Th>Date</Th>
                  <Th>Result</Th>
                  <Th />
                </Tr>
              </THead>
              <TBody>
                {past.map((x) => (
                  <Tr key={x.enrollmentId}>
                    <Td className="font-medium">{x.program}</Td>
                    <Td className="text-sm">{formatDate(x.completedOn ?? x.startsAtUtc.slice(0, 10))}</Td>
                    <Td>
                      <StatusBadge status={x.status} label={ENROLLMENT_LABELS[x.status]} />
                      {x.score != null && <span className="ms-2 text-xs text-zinc-500 dark:text-zinc-400">{x.score}%</span>}
                    </Td>
                    <Td className="text-end">
                      {(x.status === 'Attended' || x.status === 'Completed' || x.status === 'Failed') &&
                        (x.feedbackGiven ? (
                          <span className="text-xs text-zinc-500 dark:text-zinc-400">Feedback given</span>
                        ) : (
                          <Button size="sm" variant="secondary" icon={<Star className="size-3.5" />} onClick={() => setRating(x)}>
                            Give feedback
                          </Button>
                        ))}
                    </Td>
                  </Tr>
                ))}
              </TBody>
            </Table>
          )}
        </Card>
        <RequestsTab all={false} />
      </div>
      <Card className="self-start">
        <CardHeader title="Required for me" description="Courses the company asks everyone in your role to complete" />
        {!required.length ? (
          <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">Nothing required.</p>
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {required.map((c) => (
              <li key={c.programId} className="px-5 py-3 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-medium">{compliance.data!.programs.find((p) => p.id === c.programId)?.title}</span>
                  <ComplianceBadge state={c.state} />
                </div>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">
                  {c.lastDone ? `Done ${formatDate(c.lastDone)}` : 'Not done yet'}
                  {c.dueOn ? ` · ${c.lastDone ? 'renew' : 'due'} by ${formatDate(c.dueOn)}` : ''}
                  {c.scheduled ? ' · booked' : ''}
                </p>
              </li>
            ))}
          </ul>
        )}
      </Card>
      {rating && <FeedbackModal item={rating} onClose={() => setRating(null)} />}
    </div>
  );
}

function FeedbackModal({ item, onClose }: { item: MyTraining; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [rating, setRating] = useState<number | null>(null);
  const [trainer, setTrainer] = useState<number | null>(null);
  const [recommend, setRecommend] = useState<boolean | null>(null);
  const [comment, setComment] = useState('');
  const save = useMutation({
    mutationFn: () =>
      trainingApi.feedback(item.enrollmentId, { rating: rating!, trainerRating: trainer, wouldRecommend: recommend, comment: comment.trim() || null }),
    onSuccess: () => {
      toast.success('Thanks for the feedback');
      qc.invalidateQueries({ queryKey: qk.training });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Feedback: ${item.program}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!rating} onClick={() => save.mutate()}>
            Send
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="How useful was the session?" required>
          <Rating value={rating} onChange={setRating} />
        </Field>
        <Field label="The trainer">
          <Rating value={trainer} onChange={setTrainer} />
        </Field>
        <Field label="Would you recommend it to a colleague?">
          <div className="flex gap-2">
            {[true, false].map((v) => (
              <button
                key={String(v)}
                type="button"
                onClick={() => setRecommend(v)}
                className={
                  recommend === v
                    ? 'rounded-lg border border-accent-500 bg-accent-50 px-3 py-1.5 text-sm text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                    : 'rounded-lg border border-zinc-200 px-3 py-1.5 text-sm dark:border-zinc-700'
                }
              >
                {v ? 'Yes' : 'No'}
              </button>
            ))}
          </div>
        </Field>
        <Field label="Anything to add?">
          <Textarea rows={3} value={comment} maxLength={2000} onChange={(e) => setComment(e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Sessions ----------

function SessionsTab({ me }: { me: TrainingMe }) {
  const [past, setPast] = useState(false);
  const sessions = useQuery({ queryKey: qk.trainingSessions(past), queryFn: () => trainingApi.sessions(past) });
  const [adding, setAdding] = useState(false);
  const navigate = useNavigate();
  return (
    <Card>
      <CardHeader
        title={past ? 'Past sessions' : 'Upcoming sessions'}
        actions={
          <>
            <Button size="sm" variant="ghost" onClick={() => setPast(!past)}>
              {past ? 'Show upcoming' : 'Show past'}
            </Button>
            {me.isHr && (
              <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setAdding(true)}>
                Schedule session
              </Button>
            )}
          </>
        }
      />
      {sessions.isLoading ? (
        <SkeletonRows rows={4} cols={5} />
      ) : sessions.isError ? (
        <ErrorState error={sessions.error} onRetry={() => sessions.refetch()} />
      ) : !sessions.data?.length ? (
        <EmptyState
          icon={CalendarClock}
          title={past ? 'No past sessions' : 'Nothing scheduled'}
          description={me.isHr ? 'Schedule a session of a course and nominate people.' : undefined}
        />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Course</Th>
              <Th>When</Th>
              <Th>Trainer</Th>
              <Th>Seats</Th>
              <Th>Status</Th>
            </Tr>
          </THead>
          <TBody>
            {sessions.data.map((s) => (
              <Tr key={s.id} className="cursor-pointer" onClick={() => navigate(`/training/sessions/${s.id}`)}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-white">{s.program}</p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {MODE_LABELS[s.mode]}
                    {s.venue ? ` · ${s.venue}` : ''}
                  </p>
                </Td>
                <Td className="text-sm whitespace-nowrap">{formatDateTimeUtc(s.startsAtUtc)}</Td>
                <Td className="text-sm">{s.trainer ?? s.externalTrainer ?? '—'}</Td>
                <Td className="text-sm tabular-nums">
                  {s.enrolled}
                  {s.capacity ? ` / ${s.capacity}` : ''}
                </Td>
                <Td>
                  <StatusBadge status={s.status} />
                  {s.averageRating != null && <span className="ms-2 text-xs text-zinc-500 dark:text-zinc-400">★ {s.averageRating}</span>}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {adding && <SessionModal onClose={() => setAdding(false)} />}
    </Card>
  );
}

export function SessionModal({ session, onClose }: { session?: import('../../api/training').TrainingSession; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const programs = useQuery({ queryKey: qk.trainingPrograms, queryFn: trainingApi.programs });
  const people = useEmployeeLookup();
  const local = (u?: string) => {
    if (!u) return '';
    const d = new Date(/[zZ]$/.test(u) ? u : `${u}Z`);
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  };
  const [f, setF] = useState({
    programId: session?.programId ?? '',
    start: local(session?.startsAtUtc),
    end: local(session?.endsAtUtc),
    venue: session?.venue ?? '',
    trainerType: session?.externalTrainer ? 'outside' : 'employee',
    trainerId: session?.trainerEmployeeId ?? '',
    external: session?.externalTrainer ?? '',
    capacity: session?.capacity?.toString() ?? '',
    cost: session?.cost?.toString() ?? '',
    notes: session?.notes ?? '',
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: async () => {
      const b = {
        programId: f.programId,
        startsAtUtc: new Date(f.start).toISOString(),
        endsAtUtc: new Date(f.end).toISOString(),
        venue: f.venue.trim() || null,
        trainerEmployeeId: f.trainerType === 'employee' ? f.trainerId || null : null,
        externalTrainer: f.trainerType === 'outside' ? f.external.trim() || null : null,
        capacity: f.capacity ? Number(f.capacity) : null,
        cost: f.cost ? Number(f.cost) : null,
        notes: f.notes.trim() || null,
      };
      if (session) {
        await trainingApi.updateSession(session.id, b);
        return session.id;
      }
      return (await trainingApi.addSession(b)).id;
    },
    onSuccess: (id) => {
      toast.success(session ? 'Session updated' : 'Session scheduled', session ? undefined : 'Now nominate who attends.');
      qc.invalidateQueries({ queryKey: qk.training });
      onClose();
      if (!session) navigate(`/training/sessions/${id}`);
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={session ? 'Edit session' : 'Schedule a session'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.programId || !f.start || !f.end} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Course" required className="sm:col-span-2">
          <Select value={f.programId} onChange={(e) => set('programId', e.target.value)}>
            <option value="">Choose…</option>
            {programs.data
              ?.filter((p) => p.isActive)
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {p.title}
                </option>
              ))}
          </Select>
        </Field>
        <Field label="Starts" required>
          <Input type="datetime-local" value={f.start} onChange={(e) => set('start', e.target.value)} />
        </Field>
        <Field label="Ends" required>
          <Input type="datetime-local" value={f.end} min={f.start} onChange={(e) => set('end', e.target.value)} />
        </Field>
        <Field label="Where" className="sm:col-span-2" hint="Room, address or meeting link">
          <Input value={f.venue} maxLength={500} onChange={(e) => set('venue', e.target.value)} />
        </Field>
        <Field label="Trainer">
          <Select value={f.trainerType} onChange={(e) => set('trainerType', e.target.value)}>
            <option value="employee">An employee</option>
            <option value="outside">Outside trainer or institute</option>
          </Select>
        </Field>
        <Field label={f.trainerType === 'employee' ? 'Who' : 'Name'}>
          {f.trainerType === 'employee' ? (
            <Select value={f.trainerId} onChange={(e) => set('trainerId', e.target.value)}>
              <option value="">Choose…</option>
              {people.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          ) : (
            <Input value={f.external} maxLength={200} onChange={(e) => set('external', e.target.value)} placeholder="e.g. Rescue 1122 trainers" />
          )}
        </Field>
        <Field label="Seats" hint="Leave empty for no limit">
          <Input type="number" min={1} value={f.capacity} onChange={(e) => set('capacity', e.target.value)} />
        </Field>
        <Field label="Total cost (PKR)">
          <Input type="number" min={0} value={f.cost} onChange={(e) => set('cost', e.target.value)} />
        </Field>
        <Field label="Notes for attendees" className="sm:col-span-2">
          <Textarea rows={2} value={f.notes} maxLength={2000} onChange={(e) => set('notes', e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Catalog ----------

function CatalogTab({ me, onAsk }: { me: TrainingMe; onAsk: (p: TrainingProgram) => void }) {
  const programs = useQuery({ queryKey: qk.trainingPrograms, queryFn: trainingApi.programs });
  const categories = useQuery({ queryKey: qk.trainingCategories, queryFn: trainingApi.categories });
  const [editing, setEditing] = useState<TrainingProgram | 'new' | null>(null);
  const [deleting, setDeleting] = useState<TrainingProgram | null>(null);
  const [newCategory, setNewCategory] = useState('');
  const toast = useToast();
  const qc = useQueryClient();
  const addCat = useMutation({
    mutationFn: () => trainingApi.addCategory(newCategory.trim()),
    onSuccess: () => {
      setNewCategory('');
      qc.invalidateQueries({ queryKey: qk.trainingCategories });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <div className="space-y-5">
      <Card>
        <CardHeader
          title="Courses"
          actions={
            me.isHr && (
              <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
                New course
              </Button>
            )
          }
        />
        {programs.isLoading ? (
          <SkeletonRows rows={4} cols={4} />
        ) : !programs.data?.length ? (
          <EmptyState icon={BookOpen} title="No courses yet" description={me.isHr ? 'Add courses to the catalog, then schedule sessions.' : undefined} />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {programs.data.map((p) => (
              <li key={p.id} className="flex flex-wrap items-start gap-3 px-5 py-3.5">
                <GraduationCap className="mt-0.5 size-4 text-zinc-500 dark:text-zinc-400" />
                <div className="min-w-0 flex-1">
                  <p className="flex flex-wrap items-center gap-2 font-medium text-zinc-900 dark:text-white">
                    {p.title}
                    {p.isMandatory && (
                      <Badge tone="violet">
                        <ShieldCheck className="size-3" /> Required{p.repeatEveryMonths ? `, every ${p.repeatEveryMonths} months` : ''}
                      </Badge>
                    )}
                    {!p.isActive && <Badge>Off</Badge>}
                  </p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {[p.category, MODE_LABELS[p.mode], p.durationHours ? `${p.durationHours} h` : null, me.isHr ? `${p.completed} completed` : null]
                      .filter(Boolean)
                      .join(' · ')}
                  </p>
                  {p.description && <p className="mt-1 line-clamp-2 text-sm text-zinc-600 dark:text-zinc-300">{p.description}</p>}
                  {p.link && (
                    <a
                      href={p.link}
                      target="_blank"
                      rel="noreferrer noopener"
                      className="mt-1 inline-flex items-center gap-1 text-xs text-accent-600 hover:underline"
                    >
                      Course material <ExternalLink className="size-3" />
                    </a>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  {p.isActive && (
                    <Button size="sm" variant="secondary" onClick={() => onAsk(p)}>
                      Ask to attend
                    </Button>
                  )}
                  {me.isHr && <RowActions label={p.title} onEdit={() => setEditing(p)} onDelete={p.sessions ? undefined : () => setDeleting(p)} />}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>
      {me.isHr && (
        <Card>
          <CardHeader title="Categories" />
          <div className="flex flex-wrap items-center gap-2 px-5 py-4">
            {categories.data?.map((c) => (
              <Badge key={c.id}>
                {c.name} · {c.programs}
              </Badge>
            ))}
            <div className="flex gap-2">
              <Input className="h-8 w-48" value={newCategory} maxLength={100} onChange={(e) => setNewCategory(e.target.value)} placeholder="New category" />
              <Button size="sm" variant="secondary" disabled={!newCategory.trim()} loading={addCat.isPending} onClick={() => addCat.mutate()}>
                Add
              </Button>
            </div>
          </div>
        </Card>
      )}
      {editing && <ProgramModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name={deleting?.title ?? ''}
        remove={trainingApi.deleteProgram}
        invalidate={[qk.trainingPrograms]}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}

function ProgramModal({ item, onClose }: { item: TrainingProgram | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const categories = useQuery({ queryKey: qk.trainingCategories, queryFn: trainingApi.categories });
  const departments = useDepartments();
  const [f, setF] = useState({
    title: item?.title ?? '',
    categoryId: item?.categoryId ?? '',
    description: item?.description ?? '',
    mode: (item?.mode ?? 'Classroom') as TrainingMode,
    hours: item?.durationHours?.toString() ?? '',
    link: item?.link ?? '',
    mandatory: item?.isMandatory ?? false,
    departmentIds: item?.departmentIds ?? [],
    within: item?.completeWithinDays?.toString() ?? '',
    repeat: item?.repeatEveryMonths?.toString() ?? '',
    active: item?.isActive ?? true,
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () => {
      const b = {
        title: f.title.trim(),
        categoryId: f.categoryId || null,
        description: f.description.trim() || null,
        mode: f.mode,
        durationHours: f.hours ? Number(f.hours) : null,
        link: f.link.trim() || null,
        isMandatory: f.mandatory,
        departmentIds: f.departmentIds,
        completeWithinDays: f.within ? Number(f.within) : null,
        repeatEveryMonths: f.repeat ? Number(f.repeat) : null,
        isActive: f.active,
      };
      return item ? trainingApi.updateProgram(item.id, b) : trainingApi.addProgram(b);
    },
    onSuccess: () => {
      toast.success('Course saved');
      qc.invalidateQueries({ queryKey: qk.training });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={item ? 'Edit course' : 'New course'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.title.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Title" required className="sm:col-span-2">
          <Input value={f.title} maxLength={150} onChange={(e) => set('title', e.target.value)} placeholder="Fire safety" />
        </Field>
        <Field label="Category">
          <Select value={f.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
            <option value="">—</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="How it is given">
          <Select value={f.mode} onChange={(e) => set('mode', e.target.value as TrainingMode)}>
            {(Object.keys(MODE_LABELS) as TrainingMode[]).map((m) => (
              <option key={m} value={m}>
                {MODE_LABELS[m]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Hours">
          <Input type="number" min={0.5} step={0.5} value={f.hours} onChange={(e) => set('hours', e.target.value)} />
        </Field>
        <Field label="Material link">
          <Input type="url" value={f.link} maxLength={500} onChange={(e) => set('link', e.target.value)} placeholder="https://" />
        </Field>
        <Field label="Description" className="sm:col-span-2">
          <Textarea rows={3} value={f.description} maxLength={4000} onChange={(e) => set('description', e.target.value)} />
        </Field>
        <div className="space-y-3 sm:col-span-2">
          <Checkbox
            label="Required training"
            description="Tracked on the compliance page"
            checked={f.mandatory}
            onChange={(e) => set('mandatory', e.target.checked)}
          />
          {f.mandatory && (
            <div className="grid gap-4 rounded-lg border border-zinc-200 p-3 sm:grid-cols-2 dark:border-zinc-700">
              <Field label="New joiners complete it within (days)">
                <Input type="number" min={1} value={f.within} onChange={(e) => set('within', e.target.value)} />
              </Field>
              <Field label="Repeat every (months)" hint="Empty: once is enough">
                <Input type="number" min={1} value={f.repeat} onChange={(e) => set('repeat', e.target.value)} />
              </Field>
              <Field
                label="For departments"
                hint={f.departmentIds.length ? `${f.departmentIds.length} chosen` : 'None chosen: everyone'}
                className="sm:col-span-2"
              >
                <div className="flex flex-wrap gap-x-4 gap-y-1.5">
                  {departments.data?.map((d) => (
                    <Checkbox
                      key={d.id}
                      label={d.name}
                      checked={f.departmentIds.includes(d.id)}
                      onChange={(e) => set('departmentIds', e.target.checked ? [...f.departmentIds, d.id] : f.departmentIds.filter((x) => x !== d.id))}
                    />
                  ))}
                </div>
              </Field>
            </div>
          )}
          {item && <Checkbox label="In use" checked={f.active} onChange={(e) => set('active', e.target.checked)} />}
        </div>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Compliance ----------

function ComplianceTab({ me }: { me: TrainingMe }) {
  const departments = useDepartments();
  const [dept, setDept] = useState('');
  const c = useQuery({ queryKey: qk.trainingCompliance(dept), queryFn: () => trainingApi.compliance(dept) });
  return (
    <Card>
      <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        {c.data && (
          <div className="flex flex-wrap gap-2 text-xs">
            <ComplianceBadge state="Done" count={c.data.done} />
            <ComplianceBadge state="DueSoon" count={c.data.dueSoon} />
            <ComplianceBadge state="Overdue" count={c.data.overdue} />
            <ComplianceBadge state="NotDone" count={c.data.notDone} />
          </div>
        )}
        {me.isHr && (
          <Select className="ms-auto w-56" value={dept} onChange={(e) => setDept(e.target.value)}>
            <option value="">All departments</option>
            {departments.data?.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        )}
      </div>
      {c.isLoading ? (
        <SkeletonRows rows={5} cols={4} />
      ) : c.isError ? (
        <ErrorState error={c.error} onRetry={() => c.refetch()} />
      ) : !c.data!.programs.length ? (
        <EmptyState icon={ShieldCheck} title="No required training" description="Mark courses as required to track who has done them." />
      ) : (
        <ComplianceMatrix data={c.data!} />
      )}
    </Card>
  );
}

function ComplianceMatrix({ data }: { data: Compliance }) {
  return (
    <div className="overflow-x-auto">
      <Table>
        <THead>
          <Tr>
            <Th>Employee</Th>
            {data.programs.map((p) => (
              <Th key={p.id}>{p.title}</Th>
            ))}
          </Tr>
        </THead>
        <TBody>
          {data.rows.map((r) => (
            <Tr key={r.employeeId}>
              <Td>
                <p className="font-medium">{r.name}</p>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">{[r.code, r.department].filter(Boolean).join(' · ')}</p>
              </Td>
              {data.programs.map((p) => {
                const cell = r.cells.find((x) => x.programId === p.id);
                return (
                  <Td key={p.id}>
                    {cell ? (
                      <div title={cell.lastDone ? `Done ${formatDate(cell.lastDone)}` : undefined}>
                        <ComplianceBadge state={cell.state} />
                        <p className="mt-0.5 text-[11px] text-zinc-500 dark:text-zinc-400">
                          {cell.dueOn ? formatDate(cell.dueOn) : cell.lastDone ? formatDate(cell.lastDone) : ''}
                          {cell.scheduled ? ' · booked' : ''}
                        </p>
                      </div>
                    ) : (
                      <span className="text-xs text-zinc-500 dark:text-zinc-400">—</span>
                    )}
                  </Td>
                );
              })}
            </Tr>
          ))}
        </TBody>
      </Table>
    </div>
  );
}

// ---------- Requests ----------

function RequestsTab({ all }: { all: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const requests = useQuery({ queryKey: qk.trainingRequests, queryFn: trainingApi.requests });
  const [open, setOpen] = useState<string | null>(null);
  const cancel = useMutation({
    mutationFn: trainingApi.cancelRequest,
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.training }),
    onError: (e) => toast.error(e),
  });
  const complete = useMutation({
    mutationFn: (id: string) => trainingApi.completeRequest(id, todayIso()),
    onSuccess: () => {
      toast.success('Marked completed');
      qc.invalidateQueries({ queryKey: qk.training });
    },
    onError: (e) => toast.error(e),
  });
  const list = requests.data ?? [];
  if (!all && !list.length) return null;
  return (
    <Card>
      <CardHeader
        title={all ? 'Training requests' : 'My requests'}
        description={all ? 'Approved outside courses: attach the certificate and mark them done.' : undefined}
      />
      {requests.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : !list.length ? (
        <EmptyState icon={GraduationCap} title="No requests" />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.map((r) => (
            <li key={r.id} className="px-5 py-3">
              <div className="flex flex-wrap items-start gap-3">
                <div className="min-w-0 flex-1">
                  <p className="font-medium text-zinc-900 dark:text-white">
                    {r.course}
                    {r.provider && <span className="font-normal text-zinc-500 dark:text-zinc-400"> · {r.provider}</span>}
                  </p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {[
                      all ? r.employeeName : null,
                      r.cost ? formatCurrency(r.cost) : null,
                      r.preferredDate ? `from ${formatDate(r.preferredDate)}` : null,
                      formatDateTimeUtc(r.createdAtUtc),
                    ]
                      .filter(Boolean)
                      .join(' · ')}
                  </p>
                  <p className="mt-0.5 text-sm text-zinc-600 dark:text-zinc-300">{r.reason}</p>
                  {r.decisionNote && <p className="text-xs text-zinc-500 dark:text-zinc-400">“{r.decisionNote}”</p>}
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  {r.completedOn ? <Badge tone="green">Completed {formatDate(r.completedOn)}</Badge> : <StatusBadge status={r.status} />}
                  {!r.programId && r.status === 'Approved' && (
                    <Button size="sm" variant="ghost" onClick={() => setOpen(open === r.id ? null : r.id)}>
                      Files
                    </Button>
                  )}
                  {all && !r.programId && r.status === 'Approved' && !r.completedOn && (
                    <Button size="sm" variant="secondary" loading={complete.isPending && complete.variables === r.id} onClick={() => complete.mutate(r.id)}>
                      Mark done
                    </Button>
                  )}
                  {!all && (r.status === 'Pending' || (r.status === 'Approved' && !r.completedOn)) && (
                    <Button size="sm" variant="ghost" loading={cancel.isPending && cancel.variables === r.id} onClick={() => cancel.mutate(r.id)}>
                      Cancel
                    </Button>
                  )}
                </div>
              </div>
              {open === r.id && (
                <div className="mt-3">
                  <AttachmentList owner="TrainingRequest" ownerId={r.id} emptyText="Attach the invoice or certificate." />
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

function RequestModal({ program, onClose }: { program: TrainingProgram | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [f, setF] = useState({ course: '', provider: '', date: '', cost: '', reason: '' });
  const set = <K extends keyof typeof f>(k: K, v: string) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () =>
      trainingApi.request({
        programId: program?.id ?? null,
        courseName: program ? null : f.course.trim(),
        provider: program ? null : f.provider.trim() || null,
        preferredDate: f.date || null,
        cost: program || !f.cost ? null : Number(f.cost),
        reason: f.reason.trim(),
      }),
    onSuccess: () => {
      toast.success('Request sent for approval');
      qc.invalidateQueries({ queryKey: qk.training });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={program ? `Ask to attend ${program.title}` : 'Ask for an outside course'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.reason.trim() || (!program && !f.course.trim())} onClick={() => save.mutate()}>
            Send for approval
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        {!program && (
          <>
            <Field label="Course" required className="sm:col-span-2">
              <Input value={f.course} maxLength={200} onChange={(e) => set('course', e.target.value)} placeholder="AWS Solutions Architect" />
            </Field>
            <Field label="Provider">
              <Input value={f.provider} maxLength={200} onChange={(e) => set('provider', e.target.value)} placeholder="Coursera, LUMS…" />
            </Field>
            <Field label="Cost (PKR)">
              <Input type="number" min={0} value={f.cost} onChange={(e) => set('cost', e.target.value)} />
            </Field>
          </>
        )}
        <Field label="Preferred start">
          <Input type="date" min={todayIso()} value={f.date} onChange={(e) => set('date', e.target.value)} />
        </Field>
        <Field label="How will it help your work?" required className="sm:col-span-2">
          <Textarea rows={3} value={f.reason} maxLength={2000} onChange={(e) => set('reason', e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Report ----------

function ReportTab() {
  const r = useQuery({ queryKey: qk.trainingReport, queryFn: trainingApi.report });
  if (r.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (r.isError) return <ErrorState error={r.error} onRetry={() => r.refetch()} />;
  const d = r.data!;
  const tile = (label: string, value: string | number) => (
    <Card className="p-4">
      <p className="text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value}</p>
      <p className="text-xs text-zinc-500 dark:text-zinc-400">{label}</p>
    </Card>
  );
  const table = (title: string, rows: typeof d.byProgram, showCost: boolean) => (
    <Card>
      <CardHeader title={title} />
      {!rows.length ? (
        <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">No training this year.</p>
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th />
              <Th className="text-end">Sessions</Th>
              <Th className="text-end">People</Th>
              <Th className="text-end">Hours</Th>
              {showCost && <Th className="text-end">Cost</Th>}
              <Th className="text-end">Rating</Th>
            </Tr>
          </THead>
          <TBody>
            {rows.map((x) => (
              <Tr key={x.name}>
                <Td>{x.name}</Td>
                <Td className="text-end tabular-nums">{x.sessions}</Td>
                <Td className="text-end tabular-nums">{x.people}</Td>
                <Td className="text-end tabular-nums">{x.hours}</Td>
                {showCost && <Td className="text-end tabular-nums">{formatCurrency(x.cost)}</Td>}
                <Td className="text-end tabular-nums">{x.rating ?? '—'}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {tile('Sessions this year', d.sessions)}
        {tile('People trained', d.peopleTrained)}
        {tile('Training hours', d.hours)}
        {tile('Cost', formatCurrency(d.cost))}
        {tile('Average rating', d.averageRating ?? '—')}
        {tile('Would recommend', d.recommendPercent != null ? `${d.recommendPercent}%` : '—')}
      </div>
      {table('By course', d.byProgram, true)}
      {table('By department', d.byDepartment, false)}
    </div>
  );
}
