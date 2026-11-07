import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { CalendarRange, ChevronRight, CircleCheck, ClipboardList, Plus, Rocket, Settings2, Target, Trash2, Users, BookMarked, MessageSquareQuote } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useDepartments } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  performanceApi,
  type PerformanceSettings,
  type PerformanceTemplate,
  type ReviewCycle,
  type ReviewListItem,
  type ReviewSection,
} from '../../api/performance';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { RecordStamp } from '../../components/RecordStamp';
import {
  Avatar,
  Badge,
  Button,
  Card,
  ProgressRing,
  StatCard,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatDate } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';
import { FeedbackRequestsTab, KpiLibraryTab, kpiQuery } from './PerformanceExtras';
import { StageChip, StageMix, stageProgress } from './shared';
import { cn } from '../../lib/cn';
import { tx, txf } from '../../lib/i18n';

type Tab = 'mine' | 'team' | 'feedback' | 'cycles' | 'kpis' | 'templates' | 'settings';

export function PerformancePage() {
  const hr = isHr(useUser());
  const [params, setParams] = useSearchParams();
  const team = useQuery({ queryKey: qk.performanceTeam, queryFn: performanceApi.team });
  const raw = (params.get('tab') as Tab | null) ?? 'mine';
  const tab: Tab = !hr && (raw === 'cycles' || raw === 'kpis' || raw === 'templates' || raw === 'settings') ? 'mine' : raw;
  const showTeam = !!team.data?.length;
  return (
    <>
      <PageHeader title="Performance" description="Goals, reviews and ratings." />
      {hr && <PerformanceOverview />}
      <Tabs<Tab>
        className="mb-5"
        value={tab}
        onChange={(t) => setParams(t === 'mine' ? {} : { tab: t }, { replace: true })}
        items={[
          { value: 'mine', label: 'My reviews', icon: <Target className="size-4" /> },
          ...(showTeam ? ([{ value: 'team', label: `My team (${team.data!.length})`, icon: <Users className="size-4" /> }] as const) : []),
          { value: 'feedback', label: 'Feedback requests', icon: <MessageSquareQuote className="size-4" /> },
          ...(hr
            ? ([
                { value: 'cycles', label: 'Review cycles', icon: <CalendarRange className="size-4" /> },
                { value: 'kpis', label: 'KPI library', icon: <BookMarked className="size-4" /> },
                { value: 'templates', label: 'Templates', icon: <ClipboardList className="size-4" /> },
                { value: 'settings', label: 'Ratings', icon: <Settings2 className="size-4" /> },
              ] as const)
            : []),
        ]}
      />
      {tab === 'mine' && <ReviewList query={{ queryKey: qk.performanceMy, queryFn: performanceApi.my }} mine />}
      {tab === 'team' && <ReviewList query={{ queryKey: qk.performanceTeam, queryFn: performanceApi.team }} />}
      {tab === 'feedback' && <FeedbackRequestsTab />}
      {tab === 'cycles' && <Cycles />}
      {tab === 'kpis' && <KpiLibraryTab />}
      {tab === 'templates' && <Templates />}
      {tab === 'settings' && <SettingsTab />}
    </>
  );
}

function ReviewList({ query, mine }: { query: { queryKey: readonly unknown[]; queryFn: () => Promise<ReviewListItem[]> }; mine?: boolean }) {
  const navigate = useNavigate();
  const list = useQuery({ queryKey: query.queryKey, queryFn: query.queryFn });
  if (list.isLoading)
    return (
      <Card>
        <SkeletonRows rows={3} cols={4} />
      </Card>
    );
  if (!list.data?.length)
    return (
      <Card>
        <EmptyState
          icon={Target}
          title={mine ? 'No reviews yet' : 'Nobody to review'}
          description={mine ? 'When HR starts a review cycle, your review appears here.' : 'Reviews of people who report to you appear here.'}
        />
      </Card>
    );
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {list.data.map((r) => (
        <button
          key={r.id}
          type="button"
          onClick={() => navigate(`/performance/reviews/${r.id}`)}
          className="group min-w-0 text-start focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500 rounded-2xl"
        >
          <Card className="h-full p-4 transition-all group-hover:-translate-y-0.5 group-hover:shadow-[0_18px_40px_-20px_rgb(79_70_229/0.35)]">
            <div className="flex items-start gap-3">
              {mine ? (
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-fuchsia-500 text-white shadow-md shadow-indigo-500/25">
                  <Target className="size-[18px]" aria-hidden="true" />
                </span>
              ) : (
                <Avatar name={r.employeeName} size="lg" className="!size-10 !text-sm" />
              )}
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold text-zinc-900 dark:text-white">{mine ? r.cycle : r.employeeName}</p>
                <p className="truncate text-xs text-zinc-500 dark:text-zinc-400">
                  {mine ? (r.reviewer ? `${tx('Reviewer')}: ${r.reviewer}` : tx('Reviewed by HR')) : `${r.cycle}${r.department ? ` · ${r.department}` : ''}`}
                </p>
              </div>
              <ProgressRing value={stageProgress(r.stage)} size={44} stroke={5} />
            </div>
            <div className="mt-4 flex items-center justify-between gap-2">
              <StageChip stage={r.stage} />
              <ChevronRight className="size-4 text-zinc-300 transition-transform group-hover:translate-x-0.5 rtl:group-hover:-translate-x-0.5 dark:text-zinc-600" aria-hidden="true" />
            </div>
            <dl className="mt-4 grid grid-cols-3 gap-2 text-center">
              {(
                [
                  ['Self', r.selfScore],
                  ['Manager', r.managerScore],
                  ['Final', r.finalRating],
                ] as const
              ).map(([k, v]) => (
                <div key={k} className={cn('rounded-xl px-2 py-2', k === 'Final' && v != null ? 'bg-gradient-to-br from-indigo-50 to-fuchsia-50 ring-1 ring-indigo-100 dark:from-indigo-500/10 dark:to-fuchsia-500/10 dark:ring-indigo-400/15' : 'bg-zinc-50 dark:bg-white/[0.03]')}>
                  <dt className="text-[11px] text-zinc-500 dark:text-zinc-400">{tx(k)}</dt>
                  <dd className="text-base font-semibold text-zinc-900 tabular-nums dark:text-white">{v ?? '—'}</dd>
                </div>
              ))}
            </dl>
          </Card>
        </button>
      ))}
    </div>
  );
}

/** HR's view of the whole module at a glance; each tile opens the tab behind it. */
function PerformanceOverview() {
  const cycles = useQuery({ queryKey: qk.performanceCycles, queryFn: performanceApi.cycles });
  const kpis = useQuery(kpiQuery);
  const active = (cycles.data ?? []).filter((c) => c.status === 'Active');
  const reviews = active.reduce((n, c) => n + c.reviews, 0);
  const done = active.reduce((n, c) => n + (c.byStage.Done ?? 0) + (c.byStage.SignOff ?? 0), 0);
  const withHr = active.reduce((n, c) => n + (c.byStage.Calibration ?? 0), 0);
  const loading = cycles.isLoading;
  return (
    <div className="mb-6 grid grid-cols-2 gap-4 lg:grid-cols-4">
      <StatCard label="Active cycles" value={active.length} icon={CalendarRange} loading={loading} hint={active[0]?.name} to="/performance?tab=cycles" />
      <StatCard label="Reviews in progress" value={reviews - done} icon={Target} tone="amber" loading={loading} hint={txf('{n} with HR', { n: withHr })} to="/performance?tab=cycles" />
      <StatCard
        label="Reviews done"
        value={reviews ? `${Math.round((done / reviews) * 100)}%` : '—'}
        icon={CircleCheck}
        tone="green"
        loading={loading}
        hint={txf('{done} of {total}', { done, total: reviews })}
        to="/performance?tab=cycles"
      />
      <StatCard label="KPIs in the library" value={kpis.data?.filter((k) => k.isActive).length} icon={BookMarked} tone="violet" loading={kpis.isLoading} hint="Ready to pick as goals" to="/performance?tab=kpis" />
    </div>
  );
}

function Cycles() {
  const navigate = useNavigate();
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: qk.performanceCycles, queryFn: performanceApi.cycles });
  const [editing, setEditing] = useState<ReviewCycle | 'new' | null>(null);
  const [launching, setLaunching] = useState<ReviewCycle | null>(null);
  const [deleting, setDeleting] = useState<ReviewCycle | null>(null);
  const launch = useMutation({
    mutationFn: (id: string) => performanceApi.launch(id),
    onSuccess: (r) => {
      toast.success('Cycle launched', `${r.reviews} reviews started; everyone has been told.`);
      setLaunching(null);
      qc.invalidateQueries({ queryKey: qk.performance });
    },
    onError: (err) => {
      setLaunching(null);
      toast.error(err);
    },
  });
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">Each cycle reviews everyone (or chosen departments) against a template.</p>
        <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          New cycle
        </Button>
      </div>
      {!list.data?.length ? (
        <EmptyState icon={CalendarRange} title="No review cycles" description="Make a template first, then start e.g. “Annual review 2026”." />
      ) : (
        <div className="grid gap-4 p-4 sm:p-5 lg:grid-cols-2">
          {list.data.map((c) => {
            const done = (c.byStage.Done ?? 0) + (c.byStage.SignOff ?? 0);
            const open = c.status !== 'Draft';
            return (
              <div
                key={c.id}
                role={open ? 'link' : undefined}
                tabIndex={open ? 0 : undefined}
                onClick={open ? () => navigate(`/performance/cycles/${c.id}`) : undefined}
                onKeyDown={open ? (e) => e.key === 'Enter' && navigate(`/performance/cycles/${c.id}`) : undefined}
                className={cn(
                  'relative overflow-hidden rounded-2xl border border-zinc-200/80 bg-gradient-to-br from-white to-indigo-50/40 p-4 transition-all dark:border-white/[0.07] dark:from-white/[0.02] dark:to-indigo-500/[0.04]',
                  open && 'cursor-pointer hover:-translate-y-0.5 hover:border-indigo-200 hover:shadow-[0_18px_40px_-22px_rgb(79_70_229/0.4)] focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:border-indigo-400/25',
                )}
              >
                <div className="flex items-start gap-4">
                  {open ? (
                    <ProgressRing value={c.reviews ? done / c.reviews : 0} size={64} stroke={7} title={txf('{done} of {total}', { done, total: c.reviews })} />
                  ) : (
                    <span className="flex size-16 shrink-0 items-center justify-center rounded-2xl bg-amber-50 text-amber-600 ring-1 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-400/20">
                      <Rocket className="size-6" aria-hidden="true" />
                    </span>
                  )}
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="truncate font-semibold text-zinc-900 dark:text-white">{c.name}</p>
                      <Badge tone={c.status === 'Active' ? 'green' : c.status === 'Draft' ? 'amber' : 'gray'} dot>
                        {c.status}
                      </Badge>
                    </div>
                    <p className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">
                      {formatDate(c.periodFrom)} – {formatDate(c.periodTo)} · {c.template}
                    </p>
                    <p className="truncate text-xs text-zinc-500 dark:text-zinc-400">{c.departments.length ? c.departments.join(', ') : tx('Everyone')}</p>
                  </div>
                  <div className="flex shrink-0 gap-1" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
                    {c.status === 'Draft' ? (
                      <>
                        <Button size="sm" icon={<Rocket className="size-3.5" />} onClick={() => setLaunching(c)}>
                          Launch
                        </Button>
                        <RowActions label={c.name} onEdit={() => setEditing(c)} onDelete={() => setDeleting(c)} />
                      </>
                    ) : (
                      <RowActions label={c.name} onEdit={c.status === 'Active' ? () => setEditing(c) : undefined} />
                    )}
                  </div>
                </div>
                {open ? (
                  <StageMix byStage={c.byStage} total={c.reviews} className="mt-4" />
                ) : (
                  <p className="mt-4 text-xs text-zinc-500 dark:text-zinc-400">{tx('Launch it to start reviews for everyone in scope.')}</p>
                )}
                <RecordStamp stamps={c.stamps} className="mt-3 border-t border-zinc-100 pt-2.5 dark:border-white/[0.06]" />
              </div>
            );
          })}
        </div>
      )}
      {editing && <CycleModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!launching}
        title={`Launch ${launching?.name}?`}
        message="Reviews are created for everyone in scope and they are notified. The template is fixed for this cycle from now on."
        confirmLabel="Launch"
        loading={launch.isPending}
        onConfirm={() => launching && launch.mutate(launching.id)}
        onClose={() => setLaunching(null)}
      />
      <DeleteConfirm item={deleting} name="Cycle" remove={performanceApi.deleteCycle} invalidate={[qk.performance]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function CycleModal({ item, onClose }: { item: ReviewCycle | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const templates = useQuery({ queryKey: qk.performanceTemplates, queryFn: performanceApi.templates });
  const departments = useDepartments();
  const year = new Date().getFullYear();
  const [name, setName] = useState(item?.name ?? `Annual review ${year}`);
  const [from, setFrom] = useState(item?.periodFrom ?? `${year}-01-01`);
  const [to, setTo] = useState(item?.periodTo ?? `${year}-12-31`);
  const [templateId, setTemplateId] = useState(item?.templateId ?? '');
  const [depts, setDepts] = useState<string[]>(item?.departmentIds ?? []);
  const [goalsDue, setGoalsDue] = useState(item?.goalsDue ?? '');
  const [selfDue, setSelfDue] = useState(item?.selfReviewDue ?? '');
  const [managerDue, setManagerDue] = useState(item?.managerReviewDue ?? '');
  const [finalReview, setFinalReview] = useState(item?.finalReview ?? false);
  const [error, setError] = useState<string | null>(null);
  const launched = item?.status === 'Active';
  const save = useMutation({
    mutationFn: () => {
      const b = {
        name: name.trim(),
        periodFrom: from,
        periodTo: to,
        templateId,
        departmentIds: depts,
        goalsDue: goalsDue || null,
        selfReviewDue: selfDue || null,
        managerReviewDue: managerDue || null,
        finalReview,
      };
      return item ? performanceApi.updateCycle(item.id, b) : performanceApi.createCycle(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Cycle saved' : 'Cycle created', item ? undefined : 'Launch it when you are ready.');
      qc.invalidateQueries({ queryKey: qk.performance });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={item ? 'Edit cycle' : 'New review cycle'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => (name.trim() && templateId ? save.mutate() : setError('Name the cycle and choose a template.'))}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {item && <RecordStamp className="sm:col-span-2" stamps={item.stamps} />}
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Name" required className="sm:col-span-2">
          <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} />
        </Field>
        <Field label="Period from">
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </Field>
        <Field label="to">
          <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
        </Field>
        <Field label="Template" required className="sm:col-span-2">
          <Select value={templateId} disabled={launched} onChange={(e) => setTemplateId(e.target.value)}>
            <option value="">Choose…</option>
            {templates.data?.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Who" hint={depts.length ? `${depts.length} department(s)` : 'Everyone'} className="sm:col-span-2">
          <div className="flex flex-wrap gap-1.5">
            {departments.data?.map((d) => (
              <Checkbox
                key={d.id}
                label={d.name}
                disabled={launched}
                checked={depts.includes(d.id)}
                onChange={(e) => setDepts((x) => (e.target.checked ? [...x, d.id] : x.filter((y) => y !== d.id)))}
              />
            ))}
          </div>
        </Field>
        <Field label="Goals due">
          <Input type="date" value={goalsDue} onChange={(e) => setGoalsDue(e.target.value)} />
        </Field>
        <Field label="Self review due">
          <Input type="date" value={selfDue} onChange={(e) => setSelfDue(e.target.value)} />
        </Field>
        <Field label="Manager review due">
          <Input type="date" value={managerDue} onChange={(e) => setManagerDue(e.target.value)} />
        </Field>
        <div className="sm:col-span-2">
          <Checkbox
            label="Final review: the manager's manager checks each review before HR"
            checked={finalReview}
            onChange={(e) => setFinalReview(e.target.checked)}
          />
        </div>
      </div>
    </Modal>
  );
}

function Templates() {
  const list = useQuery({ queryKey: qk.performanceTemplates, queryFn: performanceApi.templates });
  const [editing, setEditing] = useState<PerformanceTemplate | 'new' | null>(null);
  const [deleting, setDeleting] = useState<PerformanceTemplate | null>(null);
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">What a review covers: each person's goals and the values everyone is rated on.</p>
        <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          Add template
        </Button>
      </div>
      {!list.data?.length ? (
        <EmptyState icon={ClipboardList} title="No templates" description="E.g. Goals 60% + Values 40% (teamwork, ownership, communication)." />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data.map((t) => (
            <li key={t.id} className="flex items-center justify-between gap-3 px-5 py-3">
              <div>
                <p className="font-medium text-zinc-900 dark:text-zinc-100">{t.name}</p>
                <p className="text-xs text-zinc-500">
                  {t.sections.map((s) => `${s.name} ${s.weight}%`).join(' · ')}
                  {t.selfReview ? ' · with self review' : ''}
                </p>
                <RecordStamp stamps={t.stamps} />
              </div>
              <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={t.cycles ? undefined : () => setDeleting(t)} />
            </li>
          ))}
        </ul>
      )}
      {editing && <TemplateModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Template" remove={performanceApi.deleteTemplate} invalidate={[qk.performance]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function TemplateModal({ item, onClose }: { item: PerformanceTemplate | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [name, setName] = useState(item?.name ?? '');
  const [description, setDescription] = useState(item?.description ?? '');
  const [selfReview, setSelfReview] = useState(item?.selfReview ?? true);
  const [sections, setSections] = useState<ReviewSection[]>(
    item?.sections ?? [
      { name: 'Goals', weight: 60, kind: 'Goals', items: [] },
      {
        name: 'Values',
        weight: 40,
        kind: 'Competencies',
        items: [
          { title: 'Teamwork', description: null },
          { title: 'Ownership', description: null },
          { title: 'Communication', description: null },
        ],
      },
    ],
  );
  const [error, setError] = useState<string | null>(null);
  const total = sections.reduce((t, s) => t + (Number(s.weight) || 0), 0);
  const set = (i: number, p: Partial<ReviewSection>) => setSections((x) => x.map((s, j) => (j === i ? { ...s, ...p } : s)));
  const save = useMutation({
    mutationFn: () => {
      const b = { name: name.trim(), description: description.trim() || null, selfReview, sections: sections.map((s) => ({ ...s, weight: Number(s.weight) })) };
      return item ? performanceApi.updateTemplate(item.id, b) : performanceApi.createTemplate(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Template saved' : 'Template added');
      qc.invalidateQueries({ queryKey: qk.performance });
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
      description={item?.cycles ? 'Running cycles keep the version they were launched with.' : undefined}
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
      <div className="space-y-4">
        {item && <RecordStamp stamps={item.stamps} />}
        <FormError message={error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Name" required>
            <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Annual review" />
          </Field>
          <Field label="Description">
            <Input value={description} maxLength={500} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </div>
        <Checkbox
          label="Self review"
          description="Employees rate themselves before their manager."
          checked={selfReview}
          onChange={(e) => setSelfReview(e.target.checked)}
        />
        <div className="flex items-center justify-between">
          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Sections</p>
          <Badge tone={total === 100 ? 'green' : 'amber'}>Weights {total} / 100</Badge>
        </div>
        {sections.map((s, i) => (
          <div key={i} className="space-y-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
            <div className="grid grid-cols-[1fr_150px_90px_auto] gap-2">
              <Input value={s.name} maxLength={100} placeholder="Section" onChange={(e) => set(i, { name: e.target.value })} aria-label="Section name" />
              <Select value={s.kind} onChange={(e) => set(i, { kind: e.target.value as ReviewSection['kind'] })} aria-label="Kind">
                <option value="Goals">Own goals</option>
                <option value="Competencies">Same for all</option>
              </Select>
              <Input type="number" min={1} max={100} value={s.weight} onChange={(e) => set(i, { weight: Number(e.target.value) })} aria-label="Weight" />
              <IconButton label="Remove section" onClick={() => setSections((x) => x.filter((_, j) => j !== i))}>
                <Trash2 className="size-4" />
              </IconButton>
            </div>
            {s.kind === 'Goals' ? (
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                Each person writes their own goals at the start of the cycle; their manager approves them.
              </p>
            ) : (
              <Textarea
                rows={3}
                value={s.items.map((x) => x.title).join('\n')}
                onChange={(e) => set(i, { items: e.target.value.split('\n').map((t) => ({ title: t, description: null })) })}
                placeholder={'One per line, e.g.\nTeamwork\nOwnership'}
                aria-label="Items"
              />
            )}
          </div>
        ))}
        <Button
          variant="ghost"
          size="sm"
          icon={<Plus className="size-3.5" />}
          onClick={() => setSections((x) => [...x, { name: '', weight: 0, kind: 'Competencies', items: [] }])}
        >
          Add section
        </Button>
      </div>
    </Modal>
  );
}

function SettingsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const current = useQuery({ queryKey: qk.performanceSettings, queryFn: performanceApi.settings });
  const [draft, setDraft] = useState<PerformanceSettings | null>(null);
  const v = draft ?? current.data;
  const save = useMutation({
    mutationFn: () => performanceApi.saveSettings(v!),
    onSuccess: (s) => {
      qc.setQueryData(qk.performanceSettings, s);
      setDraft(null);
      toast.success('Saved');
    },
    onError: (err) => toast.error(err),
  });
  if (!v)
    return (
      <Card>
        <SkeletonRows rows={4} cols={3} />
      </Card>
    );
  const bellTotal = v.bellCurve.reduce((t, x) => t + x, 0);
  return (
    <Card className="max-w-3xl p-5">
      <div className="space-y-6">
        <div>
          <p className="mb-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Ratings and the bell curve</p>
          <p className="mb-3 text-xs text-zinc-500">
            Name each rating and the share of people you expect at it. Calibration shows how the real spread compares.
          </p>
          <div className="space-y-2">
            {v.ratingLabels.map((l, i) => (
              <div key={i} className="grid grid-cols-[32px_1fr_100px] items-center gap-2">
                <span className="text-center font-semibold text-zinc-900 dark:text-zinc-100">{i + 1}</span>
                <Input
                  value={l}
                  maxLength={40}
                  onChange={(e) => setDraft({ ...v, ratingLabels: v.ratingLabels.map((x, j) => (j === i ? e.target.value : x)) })}
                  aria-label={`Rating ${i + 1} name`}
                />
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={v.bellCurve[i]}
                  onChange={(e) => setDraft({ ...v, bellCurve: v.bellCurve.map((x, j) => (j === i ? Number(e.target.value) : x)) })}
                  aria-label={`Rating ${i + 1} share`}
                />
              </div>
            ))}
            <p className={bellTotal === 100 ? 'text-end text-xs text-zinc-500' : 'text-end text-xs font-medium text-red-600'}>Shares add up to {bellTotal}%</p>
          </div>
        </div>
        <div>
          <p className="mb-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Suggested raise</p>
          <p className="mb-3 text-xs text-zinc-500 dark:text-zinc-400">The raise shown at calibration for a final rating at or above each step.</p>
          <div className="space-y-2">
            {v.raiseSlabs.map((s, i) => (
              <div key={i} className="grid grid-cols-[1fr_1fr_auto] items-center gap-2">
                <Input
                  type="number"
                  min={1}
                  max={5}
                  step="0.1"
                  value={s.minScore}
                  onChange={(e) => setDraft({ ...v, raiseSlabs: v.raiseSlabs.map((x, j) => (j === i ? { ...x, minScore: Number(e.target.value) } : x)) })}
                  aria-label="Rating from"
                />
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={s.raisePercent}
                  onChange={(e) => setDraft({ ...v, raiseSlabs: v.raiseSlabs.map((x, j) => (j === i ? { ...x, raisePercent: Number(e.target.value) } : x)) })}
                  aria-label="Raise %"
                />
                <IconButton label="Remove step" onClick={() => setDraft({ ...v, raiseSlabs: v.raiseSlabs.filter((_, j) => j !== i) })}>
                  <Trash2 className="size-4" />
                </IconButton>
              </div>
            ))}
            <Button
              variant="ghost"
              size="sm"
              icon={<Plus className="size-3.5" />}
              onClick={() => setDraft({ ...v, raiseSlabs: [...v.raiseSlabs, { minScore: 3, raisePercent: 5 }] })}
            >
              Add step
            </Button>
          </div>
        </div>
        <div className="flex justify-end">
          <Button disabled={!draft} loading={save.isPending} onClick={() => save.mutate()}>
            Save
          </Button>
        </div>
      </div>
    </Card>
  );
}
