import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { BookMarked, MessageSquareQuote, NotebookPen, Pencil, Plus, Trash2, UserPlus } from 'lucide-react';
import { http } from '../../api/client';
import { employeesApi } from '../../api/employees';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  IconButton,
  Input,
  Modal,
  Select,
  SkeletonRows,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { tx } from '../../lib/i18n';

export interface Kpi {
  id: string;
  name: string;
  description: string | null;
  category: string | null;
  unit: string | null;
  defaultTarget: string | null;
  defaultWeight: number;
  isActive: boolean;
}
interface Feedback {
  id: string;
  fromEmployeeId: string;
  from: string;
  rating: number | null;
  comment: string | null;
  submittedAtUtc: string | null;
}
interface MyFeedback {
  id: string;
  reviewId: string;
  employee: string;
  designation: string | null;
  cycle: string;
  rating: number | null;
  comment: string | null;
  submittedAtUtc: string | null;
}
interface CheckIn {
  id: string;
  author: string;
  byEmployee: boolean;
  period: string;
  progress: number | null;
  note: string;
  createdAtUtc: string;
}
type KpiBody = Omit<Kpi, 'id'>;

const api = {
  kpis: () => http.get<Kpi[]>('/performance/kpis'),
  addKpi: (b: KpiBody) => http.post<Kpi>('/performance/kpis', b),
  updateKpi: (id: string, b: KpiBody) => http.put<Kpi>(`/performance/kpis/${id}`, b),
  removeKpi: (id: string) => http.del(`/performance/kpis/${id}`),
  feedback: (reviewId: string) => http.get<Feedback[]>(`/performance/reviews/${reviewId}/feedback`),
  requestFeedback: (reviewId: string, employeeIds: string[]) => http.post(`/performance/reviews/${reviewId}/feedback`, { employeeIds }),
  myFeedback: () => http.get<MyFeedback[]>('/performance/feedback/mine'),
  giveFeedback: (id: string, rating: number, comment: string) => http.put(`/performance/feedback/${id}`, { rating, comment }),
  checkIns: (reviewId: string) => http.get<CheckIn[]>(`/performance/reviews/${reviewId}/check-ins`),
  addCheckIn: (reviewId: string, b: { period: string; progress: number | null; note: string }) => http.post(`/performance/reviews/${reviewId}/check-ins`, b),
};

const KPIS = ['performance', 'kpis'] as const;

/** "Q4 2026" for today. */
function currentQuarter() {
  const d = new Date();
  return `Q${Math.floor(d.getMonth() / 3) + 1} ${d.getFullYear()}`;
}

function Stars({ value, onChange }: { value: number; onChange?: (v: number) => void }) {
  return (
    <span className="inline-flex gap-0.5" dir="ltr">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          disabled={!onChange}
          aria-label={`${n} / 5`}
          onClick={() => onChange?.(n)}
          className={cn('text-lg leading-none disabled:cursor-default', n <= value ? 'text-amber-500' : 'text-zinc-300 dark:text-zinc-600')}
        >
          ★
        </button>
      ))}
    </span>
  );
}

// ---------- KPI library (HR tab) ----------

export function KpiLibraryTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: KPIS, queryFn: api.kpis });
  const [editing, setEditing] = useState<Kpi | 'new' | null>(null);
  const [removing, setRemoving] = useState<Kpi | null>(null);
  const remove = useMutation({
    mutationFn: (k: Kpi) => api.removeKpi(k.id),
    onSuccess: () => (setRemoving(null), qc.invalidateQueries({ queryKey: KPIS })),
    onError: (e) => toast.error(e),
  });
  const groups = Object.entries(
    (list.data ?? []).reduce<Record<string, Kpi[]>>((acc, k) => ((acc[k.category ?? ''] ??= []).push(k), acc), {}),
  );

  return (
    <>
      <div className="mb-4 flex justify-end">
        <Button icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
          Add KPI
        </Button>
      </div>
      {list.isLoading ? (
        <Card>
          <SkeletonRows rows={4} cols={3} />
        </Card>
      ) : list.isError ? (
        <Card>
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        </Card>
      ) : !groups.length ? (
        <Card>
          <EmptyState
            icon={BookMarked}
            title="No KPIs yet"
            description="Build a library of goals with targets. Managers and staff can then pick them when setting goals."
          />
        </Card>
      ) : (
        <div className="space-y-4">
          {groups.map(([category, kpis]) => (
            <Card key={category}>
              <CardHeader title={category || tx('Other')} />
              <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {kpis.map((k) => (
                  <li key={k.id} className="flex items-center gap-3 px-5 py-3">
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">
                        {k.name} {!k.isActive && <Badge tone="gray">{tx('Off')}</Badge>}
                      </span>
                      <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">
                        {[k.defaultTarget && `${tx('Target')}: ${k.defaultTarget}${k.unit ? ` ${k.unit}` : ''}`, `${tx('Weight')} ${k.defaultWeight}`].filter(Boolean).join(' · ')}
                      </span>
                    </span>
                    <IconButton label="Edit" onClick={() => setEditing(k)}>
                      <Pencil className="size-4" />
                    </IconButton>
                    <IconButton label="Delete" onClick={() => setRemoving(k)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}
      {editing && <KpiModal kpi={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!removing}
        title="Delete this KPI?"
        message="Goals already made from it stay as they are."
        confirmLabel="Delete"
        loading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing)}
        onClose={() => setRemoving(null)}
      />
    </>
  );
}

function KpiModal({ kpi, onClose }: { kpi: Kpi | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [v, setV] = useState<KpiBody>({
    name: kpi?.name ?? '',
    description: kpi?.description ?? '',
    category: kpi?.category ?? '',
    unit: kpi?.unit ?? '',
    defaultTarget: kpi?.defaultTarget ?? '',
    defaultWeight: kpi?.defaultWeight ?? 10,
    isActive: kpi?.isActive ?? true,
  });
  const save = useMutation({
    mutationFn: () => (kpi ? api.updateKpi(kpi.id, v) : api.addKpi(v)),
    onSuccess: () => (qc.invalidateQueries({ queryKey: KPIS }), onClose()),
    onError: (e) => toast.error(e),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={kpi ? 'Edit KPI' : 'Add KPI'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!v.name.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2">
          <Input value={v.name} maxLength={200} onChange={(e) => setV({ ...v, name: e.target.value })} placeholder="On-time delivery" />
        </Field>
        <Field label="Category">
          <Input value={v.category ?? ''} maxLength={100} onChange={(e) => setV({ ...v, category: e.target.value })} placeholder="Operations" />
        </Field>
        <Field label="Unit" hint="%, PKR, units, days">
          <Input value={v.unit ?? ''} maxLength={30} onChange={(e) => setV({ ...v, unit: e.target.value })} placeholder="%" />
        </Field>
        <Field label="Usual target">
          <Input value={v.defaultTarget ?? ''} maxLength={200} onChange={(e) => setV({ ...v, defaultTarget: e.target.value })} placeholder="95" />
        </Field>
        <Field label="Usual weight">
          <Input type="number" min={0} max={100} value={v.defaultWeight} onChange={(e) => setV({ ...v, defaultWeight: Number(e.target.value) })} />
        </Field>
        <Field label="Description" className="sm:col-span-2">
          <Textarea rows={2} value={v.description ?? ''} maxLength={1000} onChange={(e) => setV({ ...v, description: e.target.value })} />
        </Field>
        {kpi && <Checkbox className="sm:col-span-2" label="Offer this KPI when setting goals" checked={v.isActive} onChange={(e) => setV({ ...v, isActive: e.target.checked })} />}
      </div>
    </Modal>
  );
}

/** Picks a KPI and hands back a ready goal (title, target, weight). */
export function KpiPickerButton({ onPick }: { onPick: (k: Kpi) => void }) {
  const [open, setOpen] = useState(false);
  const list = useQuery({ queryKey: KPIS, queryFn: api.kpis, enabled: open });
  const active = (list.data ?? []).filter((k) => k.isActive);
  return (
    <>
      <Button variant="ghost" size="sm" icon={<BookMarked className="size-3.5" />} onClick={() => setOpen(true)}>
        From KPI library
      </Button>
      <Modal open={open} onClose={() => setOpen(false)} title="Pick a KPI" footer={<Button variant="secondary" onClick={() => setOpen(false)}>{tx('Close')}</Button>}>
        {list.isLoading ? (
          <SkeletonRows rows={4} cols={1} />
        ) : !active.length ? (
          <EmptyState icon={BookMarked} title="The KPI library is empty" description="Ask HR to add KPIs." className="py-6" />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {active.map((k) => (
              <li key={k.id}>
                <button
                  type="button"
                  className="w-full px-1 py-2.5 text-start hover:bg-zinc-50 dark:hover:bg-zinc-800/50"
                  onClick={() => (onPick(k), setOpen(false))}
                >
                  <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">{k.name}</span>
                  <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                    {[k.category, k.defaultTarget && `${tx('Target')}: ${k.defaultTarget}${k.unit ? ` ${k.unit}` : ''}`].filter(Boolean).join(' · ')}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </Modal>
    </>
  );
}

// ---------- Feedback requests (everyone) ----------

export function FeedbackRequestsTab() {
  const list = useQuery({ queryKey: ['performance', 'feedback', 'mine'], queryFn: api.myFeedback });
  const [giving, setGiving] = useState<MyFeedback | null>(null);
  return (
    <Card>
      {list.isLoading ? (
        <SkeletonRows rows={3} cols={2} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data!.length ? (
        <EmptyState icon={MessageSquareQuote} title="No feedback requests" description="When a manager asks for your view on a colleague, it shows up here." />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data!.map((f) => (
            <li key={f.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">{f.employee}</span>
                <span className="block text-xs text-zinc-500 dark:text-zinc-400">{[f.designation, f.cycle].filter(Boolean).join(' · ')}</span>
              </span>
              {f.submittedAtUtc && <Stars value={f.rating ?? 0} />}
              <Button size="sm" variant={f.submittedAtUtc ? 'ghost' : 'primary'} onClick={() => setGiving(f)}>
                {f.submittedAtUtc ? 'Edit' : 'Give feedback'}
              </Button>
            </li>
          ))}
        </ul>
      )}
      {giving && <GiveFeedbackModal f={giving} onClose={() => setGiving(null)} />}
    </Card>
  );
}

function GiveFeedbackModal({ f, onClose }: { f: MyFeedback; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [rating, setRating] = useState(f.rating ?? 0);
  const [comment, setComment] = useState(f.comment ?? '');
  const save = useMutation({
    mutationFn: () => api.giveFeedback(f.id, rating, comment.trim()),
    onSuccess: () => {
      toast.success('Feedback sent');
      qc.invalidateQueries({ queryKey: ['performance', 'feedback'] });
      onClose();
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`${tx('Feedback on')} ${f.employee}`}
      description="Only their reviewer and HR see it."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!rating || !comment.trim()} onClick={() => save.mutate()}>
            Send
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Overall">
          <Stars value={rating} onChange={setRating} />
        </Field>
        <Field label="What they do well, and what could be better" required>
          <Textarea rows={5} value={comment} maxLength={4000} onChange={(e) => setComment(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

// ---------- On a review: check-ins and others' feedback ----------

export function ReviewExtras({ reviewId, isReviewer, isEmployee }: { reviewId: string; isReviewer: boolean; isEmployee: boolean }) {
  return (
    <div className="mt-6 grid gap-6 lg:grid-cols-2">
      <CheckInsCard reviewId={reviewId} />
      {isReviewer && !isEmployee && <FeedbackCard reviewId={reviewId} />}
    </div>
  );
}

function CheckInsCard({ reviewId }: { reviewId: string }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = ['performance', 'check-ins', reviewId];
  const list = useQuery({ queryKey: key, queryFn: () => api.checkIns(reviewId) });
  const [period, setPeriod] = useState(currentQuarter);
  const [progress, setProgress] = useState('');
  const [note, setNote] = useState('');
  const add = useMutation({
    mutationFn: () => api.addCheckIn(reviewId, { period, progress: progress === '' ? null : Number(progress), note: note.trim() }),
    onSuccess: () => {
      setNote('');
      setProgress('');
      qc.invalidateQueries({ queryKey: key });
      toast.success('Check-in added');
    },
    onError: (e) => toast.error(e),
  });
  const year = new Date().getFullYear();
  const quarters = [year - 1, year].flatMap((y) => [1, 2, 3, 4].map((q) => `Q${q} ${y}`));

  return (
    <Card>
      <CardHeader title="Check-ins" description="Progress notes during the year, from either side" />
      <CardBody className="space-y-3 border-b border-zinc-100 dark:border-zinc-800">
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Quarter">
            <Select value={period} onChange={(e) => setPeriod(e.target.value)}>
              {quarters.map((q) => (
                <option key={q}>{q}</option>
              ))}
            </Select>
          </Field>
          <Field label="Progress (%)" hint="Optional">
            <Input type="number" min={0} max={100} value={progress} onChange={(e) => setProgress(e.target.value)} />
          </Field>
        </div>
        <Field label="Note">
          <Textarea rows={3} value={note} maxLength={4000} onChange={(e) => setNote(e.target.value)} placeholder="What moved, what is stuck, what help is needed" />
        </Field>
        <div className="flex justify-end">
          <Button size="sm" icon={<NotebookPen className="size-3.5" />} loading={add.isPending} disabled={!note.trim()} onClick={() => add.mutate()}>
            Add check-in
          </Button>
        </div>
      </CardBody>
      {list.isLoading ? (
        <SkeletonRows rows={2} cols={1} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data!.length ? (
        <p className="px-5 py-4 text-sm text-zinc-500 dark:text-zinc-400">{tx('No check-ins yet.')}</p>
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data!.map((c) => (
            <li key={c.id} className="px-5 py-3">
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <Badge tone={c.byEmployee ? 'blue' : 'violet'}>{c.period}</Badge>
                <span className="font-medium text-zinc-900 dark:text-zinc-100">{c.author}</span>
                {c.progress != null && <span className="text-xs text-zinc-500 tabular-nums dark:text-zinc-400">{c.progress}%</span>}
                <span className="ms-auto text-xs text-zinc-500 dark:text-zinc-400">{formatDateTimeUtc(c.createdAtUtc)}</span>
              </div>
              <p className="mt-1 text-sm whitespace-pre-line text-zinc-700 dark:text-zinc-300">{c.note}</p>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

function FeedbackCard({ reviewId }: { reviewId: string }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = ['performance', 'feedback', reviewId];
  const list = useQuery({ queryKey: key, queryFn: () => api.feedback(reviewId) });
  const people = useQuery({ queryKey: qk.employeeLookup, queryFn: employeesApi.lookup });
  const [pick, setPick] = useState('');
  const ask = useMutation({
    mutationFn: () => api.requestFeedback(reviewId, [pick]),
    onSuccess: () => {
      setPick('');
      qc.invalidateQueries({ queryKey: key });
      toast.success('Feedback requested');
    },
    onError: (e) => toast.error(e),
  });
  const asked = new Set((list.data ?? []).map((f) => f.fromEmployeeId));

  return (
    <Card>
      <CardHeader title="Feedback from others" description="Ask peers or other managers. The employee doesn't see it." />
      <CardBody className="flex gap-2 border-b border-zinc-100 dark:border-zinc-800">
        <Select className="min-w-0 flex-1" value={pick} onChange={(e) => setPick(e.target.value)} aria-label="Person to ask">
          <option value="">{tx('Choose a person')}</option>
          {(people.data ?? [])
            .filter((p) => !asked.has(p.id))
            .map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
        </Select>
        <Button icon={<UserPlus className="size-4" />} loading={ask.isPending} disabled={!pick} onClick={() => ask.mutate()}>
          Ask
        </Button>
      </CardBody>
      {list.isLoading ? (
        <SkeletonRows rows={2} cols={1} />
      ) : !list.data?.length ? (
        <p className="px-5 py-4 text-sm text-zinc-500 dark:text-zinc-400">{tx('No one asked yet.')}</p>
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data.map((f) => (
            <li key={f.id} className="px-5 py-3">
              <div className="flex items-center gap-2 text-sm">
                <span className="font-medium text-zinc-900 dark:text-zinc-100">{f.from}</span>
                {f.submittedAtUtc ? <Stars value={f.rating ?? 0} /> : <Badge tone="amber">{tx('Waiting')}</Badge>}
              </div>
              {f.comment && <p className="mt-1 text-sm whitespace-pre-line text-zinc-700 dark:text-zinc-300">{f.comment}</p>}
            </li>
          ))}
        </ul>
      )}
      <p className="px-5 pb-4 text-xs text-zinc-500 dark:text-zinc-400">
        <Link to="/performance?tab=feedback" className="hover:underline">
          {tx('Your own feedback requests')}
        </Link>
      </p>
    </Card>
  );
}
