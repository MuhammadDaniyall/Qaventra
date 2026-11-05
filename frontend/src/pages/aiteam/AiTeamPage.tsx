import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bot, Check, ChevronDown, ExternalLink, OctagonX, Play, Settings2, ShieldCheck, Sparkles, X } from 'lucide-react';
import { aiTeamApi, type Robot, type RobotAutonomy, type RobotFinding, type RobotProposal, type RobotRun, type RobotSettings } from '../../api/aiTeam';
import {
  Badge,
  Button,
  Card,
  CardBody,
  EmptyState,
  ErrorState,
  Field,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Switch,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { useT } from '../../lib/i18n';

const LEVEL_TONE = { good: 'green', watch: 'amber', act: 'red' } as const;
const COLORS = ['from-violet-500 to-fuchsia-500', 'from-sky-500 to-cyan-500', 'from-emerald-500 to-teal-500', 'from-amber-500 to-orange-500',
  'from-rose-500 to-pink-500', 'from-indigo-500 to-blue-500', 'from-lime-500 to-green-500', 'from-slate-500 to-zinc-600'];

const AUTONOMY: { value: RobotAutonomy; label: string; about: string }[] = [
  { value: 'Advisory', label: 'L1 · Advisory', about: 'Looks and reports. Changes nothing, proposes nothing.' },
  { value: 'Supervised', label: 'L2 · Supervised', about: 'Also prepares proposals; a person approves each one before anything happens.' },
  { value: 'Routine', label: 'L3 · Routine', about: 'Sends low-risk reminders on its own; everything else still waits for a person.' },
];

function RobotAvatar({ robot, size = 'md' }: { robot: Pick<Robot, 'kind' | 'name'>; size?: 'md' | 'sm' }) {
  const kinds = ['PayrollChecker', 'AttendanceWatcher', 'CvScreener', 'HelpdeskResponder', 'PeopleRisk', 'OnboardingCoach', 'LeavePlanner', 'DocumentWatcher'];
  const color = COLORS[Math.max(0, kinds.indexOf(robot.kind)) % COLORS.length];
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-white shadow-sm',
        color,
        size === 'md' ? 'size-11' : 'size-8',
      )}
      aria-hidden="true"
    >
      <Bot className={size === 'md' ? 'size-6' : 'size-4'} />
    </span>
  );
}

function Findings({ findings }: { findings: RobotFinding[] }) {
  const { t } = useT();
  if (findings.length === 0) return <p className="text-sm text-zinc-500">{t('Nothing found.')}</p>;
  return (
    <ul className="space-y-2">
      {findings.map((f, i) => (
        <li key={i} className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={LEVEL_TONE[f.level] ?? 'gray'}>{t(f.level === 'good' ? 'Good' : f.level === 'watch' ? 'Watch' : 'Act')}</Badge>
            <span className="font-medium">{f.title}</span>
            {f.link && (
              <Link to={f.link} className="ms-auto inline-flex items-center gap-1 text-xs text-accent-700 hover:underline dark:text-accent-300">
                {t('Open')} <ExternalLink className="size-3" />
              </Link>
            )}
          </div>
          <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">{f.detail}</p>
        </li>
      ))}
    </ul>
  );
}

function RunView({ run }: { run: RobotRun }) {
  const { t } = useT();
  return (
    <div className="space-y-4">
      <div className="rounded-xl bg-gradient-to-br from-accent-50 to-white p-4 dark:from-accent-500/10 dark:to-zinc-900">
        <div className="flex items-center gap-2 text-xs text-zinc-500">
          {run.usedAi && <Sparkles className="size-3.5 text-accent-500" />}
          {run.usedAi ? t('Explained by AI') : t("The robot's own summary")}
        </div>
        <p className="mt-1 font-medium text-zinc-800 dark:text-zinc-100">{run.summary}</p>
        {run.error && <p className="mt-1 text-sm text-red-600">{run.error}</p>}
      </div>
      <Findings findings={run.findings} />
      <details className="rounded-lg border border-zinc-200 p-3 text-sm dark:border-zinc-800">
        <summary className="cursor-pointer font-medium">
          {t('Steps')} ({run.steps.length})
        </summary>
        <ol className="mt-2 list-decimal space-y-1 ps-5 text-zinc-600 dark:text-zinc-400">
          {run.steps.map((s, i) => (
            <li key={i}>{s}</li>
          ))}
        </ol>
      </details>
    </div>
  );
}

function SettingsModal({ robot, owners, onClose }: { robot: Robot; owners: { id: string; name: string }[]; onClose: () => void }) {
  const { t } = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const [s, setS] = useState<RobotSettings>({
    enabled: robot.enabled,
    autonomy: robot.autonomy,
    ownerUserId: robot.ownerUserId,
    runHour: robot.runHour,
    runsPerMonth: robot.runsPerMonth,
  });
  const save = useMutation({
    mutationFn: () => aiTeamApi.update(robot.id, s),
    onSuccess: () => {
      toast.success(t('Saved'));
      qc.invalidateQueries({ queryKey: ['ai-team'] });
      onClose();
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={robot.name}
      description={robot.about}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            {t('Save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Switch checked={s.enabled} onChange={(v) => setS({ ...s, enabled: v })} label="Run every day" description="Switched off, it runs only when you press Run now." />
        <div className="space-y-2">
          <p className="text-sm font-medium">{t('How far it may go')}</p>
          {AUTONOMY.filter((a) => robot.canPropose || a.value === 'Advisory').map((a) => (
            <label
              key={a.value}
              className={cn(
                'flex cursor-pointer items-start gap-3 rounded-lg border p-3',
                s.autonomy === a.value ? 'border-accent-500 bg-accent-50/50 dark:bg-accent-500/10' : 'border-zinc-200 dark:border-zinc-800',
              )}
            >
              <input type="radio" name="autonomy" className="mt-1 accent-accent-600" checked={s.autonomy === a.value} onChange={() => setS({ ...s, autonomy: a.value })} />
              <span>
                <span className="block text-sm font-medium">{t(a.label)}</span>
                <span className="block text-xs text-zinc-500">{t(a.about)}</span>
              </span>
            </label>
          ))}
          {!robot.canPropose && <p className="text-xs text-zinc-500">{t('This robot only gives advice.')}</p>}
        </div>
        <Field label="Owner" hint="Hears about anything that needs a person.">
          <Select value={s.ownerUserId ?? ''} onChange={(e) => setS({ ...s, ownerUserId: e.target.value || null })}>
            <option value="">{t('Nobody')}</option>
            {owners.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Daily run at">
            <Select value={s.runHour} onChange={(e) => setS({ ...s, runHour: Number(e.target.value) })}>
              {Array.from({ length: 24 }, (_, h) => (
                <option key={h} value={h}>
                  {String(h).padStart(2, '0')}:00
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Runs a month (budget)">
            <Input type="number" min={1} max={500} value={s.runsPerMonth} onChange={(e) => setS({ ...s, runsPerMonth: Number(e.target.value) })} />
          </Field>
        </div>
      </div>
    </Modal>
  );
}

function RobotCard({ robot, owners, stopped, onRan }: { robot: Robot; owners: { id: string; name: string }[]; stopped: boolean; onRan: (r: RobotRun) => void }) {
  const { t } = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const [settings, setSettings] = useState(false);
  const run = useMutation({
    mutationFn: () => aiTeamApi.run(robot.id),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['ai-team'] });
      onRan(r);
    },
    onError: (e) => toast.error(e),
  });
  const level = AUTONOMY.find((a) => a.value === robot.autonomy);
  return (
    <Card className="flex flex-col">
      <CardBody className="flex flex-1 flex-col gap-3">
        <div className="flex items-start gap-3">
          <RobotAvatar robot={robot} />
          <div className="min-w-0 flex-1">
            <p className="font-semibold">{robot.name}</p>
            <div className="mt-1 flex flex-wrap gap-1.5">
              <Badge tone={robot.enabled ? 'green' : 'gray'} dot>
                {t(robot.enabled ? 'On' : 'Off')}
              </Badge>
              <Badge tone="violet">{t(level?.label ?? robot.autonomy)}</Badge>
              {robot.pending > 0 && <Badge tone="amber">{robot.pending} {t('waiting')}</Badge>}
            </div>
          </div>
        </div>
        <p className="text-sm text-zinc-600 dark:text-zinc-400">{robot.about}</p>
        {robot.lastSummary && (
          <div className="rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-800/50">
            <p className="text-xs text-zinc-500">
              {t('Last run')} {formatDateTimeUtc(robot.lastRunAtUtc)}
            </p>
            <p className="mt-0.5 line-clamp-3">{robot.lastSummary}</p>
          </div>
        )}
        <p className="mt-auto text-xs text-zinc-500">
          {robot.runsThisMonth}/{robot.runsPerMonth} {t('runs this month')} · {t('owner')}: {robot.ownerName ?? t('nobody')} · {String(robot.runHour).padStart(2, '0')}:00
        </p>
        <div className="flex gap-2">
          <Button size="sm" icon={<Play className="size-4" />} loading={run.isPending} disabled={stopped} onClick={() => run.mutate()}>
            {t('Run now')}
          </Button>
          <Button size="sm" variant="secondary" icon={<Settings2 className="size-4" />} onClick={() => setSettings(true)}>
            {t('Settings')}
          </Button>
        </div>
      </CardBody>
      {settings && <SettingsModal robot={robot} owners={owners} onClose={() => setSettings(false)} />}
    </Card>
  );
}

function ProposalItem({ p }: { p: RobotProposal }) {
  const { t } = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const [body, setBody] = useState(p.detail);
  const decide = useMutation({
    mutationFn: (approve: boolean) => aiTeamApi.decide(p.id, approve, undefined, p.canEdit && body !== p.detail ? body : undefined),
    onSuccess: (_, approve) => {
      toast.success(t(approve ? 'Approved and done' : 'Rejected'));
      qc.invalidateQueries({ queryKey: ['ai-team'] });
      qc.invalidateQueries({ queryKey: ['ai-team-proposals'] });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardBody className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <Badge tone="violet">{p.robotName}</Badge>
          <span className="font-medium">{p.title}</span>
          {p.link && (
            <Link to={p.link} className="ms-auto inline-flex items-center gap-1 text-xs text-accent-700 hover:underline dark:text-accent-300">
              {t('Open record')} <ExternalLink className="size-3" />
            </Link>
          )}
        </div>
        {p.canEdit ? (
          <Field label={p.action === 'helpdesk_reply' ? 'Reply the robot drafted (you can edit it)' : 'Message (you can edit it)'}>
            <Textarea rows={p.action === 'helpdesk_reply' ? 5 : 3} value={body} onChange={(e) => setBody(e.target.value)} />
          </Field>
        ) : (
          <p className="text-sm text-zinc-600 dark:text-zinc-400">{p.detail}</p>
        )}
        <div className="flex items-center gap-2">
          <Button size="sm" icon={<Check className="size-4" />} loading={decide.isPending && decide.variables} onClick={() => decide.mutate(true)}>
            {t('Approve')}
          </Button>
          <Button size="sm" variant="secondary" icon={<X className="size-4" />} loading={decide.isPending && !decide.variables} onClick={() => decide.mutate(false)}>
            {t('Reject')}
          </Button>
          <span className="ms-auto text-xs text-zinc-500">{formatDateTimeUtc(p.createdAtUtc)}</span>
        </div>
      </CardBody>
    </Card>
  );
}

function Inbox() {
  const { t } = useT();
  const [history, setHistory] = useState(false);
  const q = useQuery({ queryKey: ['ai-team-proposals', history], queryFn: () => aiTeamApi.proposals(!history) });
  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        <Button size="sm" variant={history ? 'secondary' : 'primary'} onClick={() => setHistory(false)}>
          {t('Waiting')}
        </Button>
        <Button size="sm" variant={history ? 'primary' : 'secondary'} onClick={() => setHistory(true)}>
          {t('Decided')}
        </Button>
      </div>
      {q.isLoading && <SkeletonRows rows={4} />}
      {q.isError && <ErrorState error={q.error} />}
      {q.data?.length === 0 && (
        <EmptyState icon={ShieldCheck} title={history ? 'Nothing decided yet' : 'Nothing waiting'} description="Robots set to L2 or L3 put their proposals here for a person to approve." />
      )}
      {!history && q.data?.map((p) => <ProposalItem key={p.id} p={p} />)}
      {history &&
        q.data?.map((p) => (
          <Card key={p.id}>
            <CardBody className="flex flex-wrap items-center gap-2 text-sm">
              <Badge tone={p.status === 'Approved' || p.status === 'AutoDone' ? 'green' : p.status === 'Rejected' ? 'gray' : 'red'}>
                {t(p.status === 'AutoDone' ? 'Sent by robot' : p.status)}
              </Badge>
              <span className="font-medium">{p.title}</span>
              <span className="text-zinc-500">
                · {p.robotName} · {p.decidedByName} · {formatDateTimeUtc(p.decidedAtUtc)}
              </span>
              {p.note && <span className="w-full text-zinc-500">{p.note}</span>}
            </CardBody>
          </Card>
        ))}
    </div>
  );
}

function Activity({ robots }: { robots: Robot[] }) {
  const { t } = useT();
  const [robotId, setRobotId] = useState('');
  const [open, setOpen] = useState<string | null>(null);
  const q = useQuery({ queryKey: ['ai-team-runs', robotId], queryFn: () => aiTeamApi.runs(robotId || undefined) });
  return (
    <div className="space-y-3">
      <div className="max-w-xs">
        <Select value={robotId} onChange={(e) => setRobotId(e.target.value)} aria-label={t('Robot')}>
          <option value="">{t('All robots')}</option>
          {robots.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </Select>
      </div>
      {q.isLoading && <SkeletonRows rows={5} />}
      {q.isError && <ErrorState error={q.error} />}
      {q.data?.length === 0 && <EmptyState icon={Bot} title="No runs yet" description="Press Run now on a robot, or switch it on to run every day." />}
      {q.data?.map((r) => (
        <Card key={r.id}>
          <button type="button" className="flex w-full items-center gap-3 p-4 text-start" onClick={() => setOpen(open === r.id ? null : r.id)}>
            <Badge tone={r.status === 'Done' ? 'green' : r.status === 'Failed' ? 'red' : 'blue'}>{t(r.status)}</Badge>
            <span className="min-w-0 flex-1">
              <span className="block font-medium">{r.robotName}</span>
              <span className="block truncate text-sm text-zinc-500">{r.summary}</span>
            </span>
            <span className="hidden text-xs text-zinc-500 sm:block">
              {formatDateTimeUtc(r.startedAtUtc)} · {r.manual ? r.startedByName : t('Schedule')}
            </span>
            <ChevronDown className={cn('size-4 transition-transform', open === r.id && 'rotate-180')} />
          </button>
          {open === r.id && (
            <div className="border-t border-zinc-200 p-4 dark:border-zinc-800">
              <RunView run={r} />
            </div>
          )}
        </Card>
      ))}
    </div>
  );
}

/** AI Team: robot employees that check payroll, attendance, CVs, tickets, onboarding, leave and documents, with people approving what matters. */
export function AiTeamPage() {
  const { t } = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const [tab, setTab] = useState<'robots' | 'inbox' | 'activity'>('robots');
  const [ran, setRan] = useState<RobotRun | null>(null);
  const team = useQuery({ queryKey: ['ai-team'], queryFn: aiTeamApi.get });
  const stop = useMutation({
    mutationFn: (stopped: boolean) => aiTeamApi.stop(stopped),
    onSuccess: (_, stopped) => {
      toast.success(t(stopped ? 'All robots stopped' : 'Robots started again'));
      qc.invalidateQueries({ queryKey: ['ai-team'] });
    },
    onError: (e) => toast.error(e),
  });
  const d = team.data;
  const on = d?.robots.filter((r) => r.enabled).length ?? 0;
  const runs = d?.robots.reduce((n, r) => n + r.runsThisMonth, 0) ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            <Bot className="size-6 text-accent-600" /> {t('AI Team')}
          </span>
        }
        description="Robot employees that watch payroll, attendance, hiring, the help desk, onboarding, leave and documents. They find, explain and prepare; people approve what matters."
        actions={
          d &&
          (d.stopped ? (
            <Button icon={<Play className="size-4" />} loading={stop.isPending} onClick={() => stop.mutate(false)}>
              {t('Start robots again')}
            </Button>
          ) : (
            <Button variant="danger" icon={<OctagonX className="size-4" />} loading={stop.isPending} onClick={() => stop.mutate(true)}>
              {t('Emergency stop')}
            </Button>
          ))
        }
      />
      {team.isError && <ErrorState error={team.error} />}
      {d && (
        <>
          {d.stopped && (
            <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-200">
              {t('Every robot is stopped')}
              {d.stoppedBy ? ` (${d.stoppedBy})` : ''}. {t('Nothing runs until you start them again.')}
            </div>
          )}
          {!d.aiOn && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-200">
              {t('AI is not switched on for this site, so robots give their own plain summaries and cannot draft replies or read CVs.')}
            </div>
          )}
          <div className="grid gap-3 sm:grid-cols-3">
            {[
              { label: 'Robots on', value: `${on}/${d.robots.length}` },
              { label: 'Waiting for approval', value: d.pending },
              { label: 'Runs this month', value: runs },
            ].map((s) => (
              <div key={s.label} className="rounded-xl border border-zinc-200 bg-gradient-to-br from-white to-zinc-50 p-4 dark:border-zinc-800 dark:from-zinc-900 dark:to-zinc-900/50">
                <p className="text-xs text-zinc-500">{t(s.label)}</p>
                <p className="mt-1 text-2xl font-semibold">{s.value}</p>
              </div>
            ))}
          </div>
          <p className="flex items-center gap-2 text-xs text-zinc-500">
            <ShieldCheck className="size-4" />
            {t('Robots never approve requests, change pay or release payroll. Every step they take is logged.')}
          </p>
        </>
      )}
      <Tabs
        value={tab}
        onChange={setTab}
        items={[
          { value: 'robots', label: t('Robots') },
          { value: 'inbox', label: `${t('Approval inbox')}${d?.pending ? ` (${d.pending})` : ''}` },
          { value: 'activity', label: t('Activity log') },
        ]}
      />
      {tab === 'robots' && (
        <>
          {team.isLoading && <SkeletonRows rows={6} />}
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {d?.robots.map((r) => <RobotCard key={r.id} robot={r} owners={d.owners} stopped={d.stopped} onRan={setRan} />)}
          </div>
        </>
      )}
      {tab === 'inbox' && <Inbox />}
      {tab === 'activity' && d && <Activity robots={d.robots} />}
      {ran && (
        <Modal open onClose={() => setRan(null)} title={ran.robotName} description={t('Result of this run')} size="lg">
          <RunView run={ran} />
          {ran.proposals > 0 && (
            <Button
              className="mt-4"
              onClick={() => {
                setRan(null);
                setTab('inbox');
              }}
            >
              {t('Open the approval inbox')}
            </Button>
          )}
        </Modal>
      )}
    </div>
  );
}
