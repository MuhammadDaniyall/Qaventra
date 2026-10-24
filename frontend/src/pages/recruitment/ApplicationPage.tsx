import { useState } from 'react';
import { OfferLetterButton } from './OfferLetter';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Ban, CalendarPlus, Check, Handshake, Lock, MessageSquare, UserCheck, X } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import {
  CLOSED_STAGES,
  MODE_LABELS,
  RECOMMENDATION_LABELS,
  recruitmentApi,
  SOURCE_LABELS,
  STAGE_LABELS,
  type Application,
  type ApplicationStage,
  type HireRecommendation,
  type Interview,
  type InterviewMode,
} from '../../api/recruitment';
import { useUser } from '../../auth/AuthContext';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  ErrorState,
  Field,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import { RejectModal } from './JobPage';
import { StageBadge, Stars } from './shared';

const MOVABLE: ApplicationStage[] = ['Applied', 'Shortlisted', 'Test', 'Interview'];

/** One candidate in one job: profile and CV, stage, interviews and feedback, offer and hire, and the hiring log. */
export function ApplicationPage() {
  const { id = '' } = useParams();
  const app = useQuery({
    queryKey: qk.application(id),
    queryFn: () => recruitmentApi.application(id),
  });
  if (app.isLoading) return <SkeletonRows rows={8} cols={3} />;
  if (app.isError) return <ErrorState error={app.error} onRetry={() => app.refetch()} />;
  return <ApplicationView a={app.data!} />;
}

function ApplicationView({ a }: { a: Application }) {
  const toast = useToast();
  const qc = useQueryClient();
  const c = a.candidate;
  const name = `${c.firstName} ${c.lastName}`.trim();
  const [rejecting, setRejecting] = useState(false);
  const [scheduling, setScheduling] = useState(false);
  const [offering, setOffering] = useState(false);
  const [hiring, setHiring] = useState(false);
  const refresh = () => qc.invalidateQueries({ queryKey: qk.recruitment });
  const move = useMutation({
    mutationFn: (stage: ApplicationStage) => recruitmentApi.move(a.id, stage, null),
    onSuccess: (_, s) => {
      toast.success(`Moved to ${STAGE_LABELS[s]}`);
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const rate = useMutation({
    mutationFn: (r: number | null) => recruitmentApi.rate(a.id, r),
    onSuccess: refresh,
    onError: (e) => toast.error(e),
  });
  const respond = useMutation({
    mutationFn: (accepted: boolean) => recruitmentApi.offerResponse(a.id, accepted, null),
    onSuccess: (_, accepted) => {
      toast.success(accepted ? 'Offer accepted' : 'Offer declined');
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const closed = a.stage === 'Hired' || CLOSED_STAGES.includes(a.stage);
  const active = !closed || a.stage === 'OnHold';

  return (
    <>
      <PageHeader
        back={a.canRun ? { to: `/recruitment/jobs/${a.jobPostId}`, label: a.job } : { to: '/recruitment?tab=interviews', label: 'My interviews' }}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {name} <StageBadge stage={a.stage} />
            {c.isBlacklisted && (
              <Badge tone="red">
                <Ban className="size-3" /> Do not hire
              </Badge>
            )}
          </span>
        }
        description={`${a.job} · applied ${formatDateTimeUtc(a.appliedAtUtc)} · ${SOURCE_LABELS[c.source]}${c.referredBy ? ` (${c.referredBy})` : ''}`}
        actions={
          a.canRun && (
            <div className="flex flex-wrap gap-2">
              {active && a.stage !== 'Offer' && (
                <Button size="sm" variant="secondary" icon={<CalendarPlus className="size-3.5" />} onClick={() => setScheduling(true)}>
                  Schedule interview
                </Button>
              )}
              {a.canOffer && active && a.offerStatus !== 'Accepted' && (
                <Button size="sm" variant="secondary" icon={<Handshake className="size-3.5" />} onClick={() => setOffering(true)}>
                  {a.offerStatus === 'Sent' ? 'Change offer' : 'Make offer'}
                </Button>
              )}
              {a.canHire && a.offerStatus === 'Accepted' && a.stage !== 'Hired' && (
                <Button size="sm" variant="success" icon={<UserCheck className="size-3.5" />} onClick={() => setHiring(true)}>
                  Hire
                </Button>
              )}
              {a.stage !== 'Hired' && !CLOSED_STAGES.includes(a.stage) && (
                <Button size="sm" variant="ghost" icon={<X className="size-3.5" />} onClick={() => setRejecting(true)}>
                  Not going ahead
                </Button>
              )}
            </div>
          )
        }
      />

      {c.isBlacklisted && c.blacklistReason && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-2.5 text-sm text-red-800 dark:bg-red-500/10 dark:text-red-200">
          On the do-not-hire list: {c.blacklistReason}
        </div>
      )}
      {a.stage === 'Hired' && c.employeeId && (
        <div className="mb-4 rounded-lg bg-emerald-50 px-4 py-2.5 text-sm text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-200">
          Hired.{' '}
          <Link to={`/employees/${c.employeeId}`} className="font-medium underline">
            Open the employee record
          </Link>
        </div>
      )}

      <div className="grid gap-5 lg:grid-cols-[1fr_22rem]">
        <div className="space-y-5">
          {a.canRun && !closed && (
            <Card className="flex flex-wrap items-center gap-x-6 gap-y-3 px-5 py-3">
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="me-1 text-xs font-medium text-zinc-500 dark:text-zinc-400">Stage</span>
                {MOVABLE.map((s) => (
                  <button
                    key={s}
                    type="button"
                    disabled={move.isPending}
                    onClick={() => s !== a.stage && move.mutate(s)}
                    className={cn(
                      'rounded-full px-3 py-1 text-xs font-medium transition',
                      s === a.stage ? 'bg-accent-600 text-white' : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300',
                    )}
                  >
                    {STAGE_LABELS[s]}
                  </button>
                ))}
                {a.stage === 'Offer' && <span className="rounded-full bg-accent-600 px-3 py-1 text-xs font-medium text-white">Offer</span>}
              </div>
              <div className="flex items-center gap-1.5">
                <span className="text-xs font-medium text-zinc-500 dark:text-zinc-400">Screening</span>
                <Stars value={a.rating} onChange={(r) => rate.mutate(r)} />
              </div>
            </Card>
          )}

          {a.offerStatus && a.canOffer && (
            <Card>
              <CardHeader
                title="Offer"
                description={a.offerStatus === 'Sent' ? 'Waiting for the candidate’s answer' : `Offer ${a.offerStatus.toLowerCase()}`}
                actions={
                  <>
                    <OfferLetterButton applicationId={a.id} />
                    {a.offerStatus === 'Sent' && (
                      <>
                        <Button
                          size="sm"
                          variant="success"
                          icon={<Check className="size-3.5" />}
                          loading={respond.isPending && respond.variables}
                          onClick={() => respond.mutate(true)}
                        >
                          Accepted
                        </Button>
                        <Button size="sm" variant="secondary" loading={respond.isPending && !respond.variables} onClick={() => respond.mutate(false)}>
                          Declined
                        </Button>
                      </>
                    )}
                  </>
                }
              />
              <dl className="grid grid-cols-2 gap-3 px-5 py-4 text-sm sm:grid-cols-3">
                <div>
                  <dt className="text-xs text-zinc-500 dark:text-zinc-400">Salary</dt>
                  <dd className="font-medium">{formatCurrency(a.offeredSalary)} a month</dd>
                </div>
                <div>
                  <dt className="text-xs text-zinc-500 dark:text-zinc-400">Joining</dt>
                  <dd className="font-medium">{formatDate(a.offeredJoiningDate)}</dd>
                </div>
                {a.offerNote && (
                  <div className="col-span-2 sm:col-span-3">
                    <dt className="text-xs text-zinc-500 dark:text-zinc-400">Note</dt>
                    <dd className="whitespace-pre-line">{a.offerNote}</dd>
                  </div>
                )}
              </dl>
            </Card>
          )}

          <Card>
            <CardHeader title="Interviews" description={a.interviews.length ? undefined : 'None yet.'} />
            {a.interviews.length > 0 && (
              <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {a.interviews.map((i) => (
                  <InterviewItem key={i.id} i={i} canRun={a.canRun} />
                ))}
              </ul>
            )}
          </Card>

          {a.coverLetter && (
            <Card>
              <CardHeader title="Cover letter" />
              <p className="px-5 py-4 text-sm whitespace-pre-line text-zinc-700 dark:text-zinc-300">{a.coverLetter}</p>
            </Card>
          )}

          <Notes a={a} />
        </div>

        <div className="space-y-5">
          <Card>
            <CardHeader title="Candidate" />
            <dl className="space-y-2.5 px-5 py-4 text-sm">
              {(
                [
                  ['Email', c.email],
                  ['Phone', c.phone],
                  ['City', c.city],
                  ['Current job', [c.currentTitle, c.currentCompany].filter(Boolean).join(' at ') || null],
                  ['Experience', c.experienceYears != null ? `${c.experienceYears} years` : null],
                  ['Notice period', c.noticeDays != null ? `${c.noticeDays} days` : null],
                  ...(a.canRun
                    ? ([
                        ['Expected salary', c.expectedSalary ? formatCurrency(c.expectedSalary) : null],
                        ['CNIC', c.cnic],
                      ] as Array<[string, string | null]>)
                    : []),
                  ['Skills', c.skills],
                ] as Array<[string, string | null]>
              ).map(([k, v]) => (
                <div key={k} className="flex justify-between gap-3">
                  <dt className="shrink-0 text-zinc-500 dark:text-zinc-400">{k}</dt>
                  <dd className="text-end break-words text-zinc-900 dark:text-zinc-100">{v ?? '—'}</dd>
                </div>
              ))}
              {c.linkedInUrl && (
                <a href={c.linkedInUrl} target="_blank" rel="noreferrer noopener" className="block text-accent-600 hover:underline">
                  LinkedIn profile
                </a>
              )}
            </dl>
            {a.canRun && c.applications.length > 1 && (
              <div className="border-t border-zinc-100 px-5 py-3 text-sm dark:border-zinc-800">
                <p className="mb-1 text-xs font-medium text-zinc-500 dark:text-zinc-400">Also considered for</p>
                {c.applications
                  .filter((x) => x.id !== a.id)
                  .map((x) => (
                    <Link key={x.id} to={`/recruitment/applications/${x.id}`} className="flex items-center justify-between gap-2 py-1 hover:text-accent-600">
                      {x.job} <StageBadge stage={x.stage} />
                    </Link>
                  ))}
              </div>
            )}
            {a.canRun && c.possibleDuplicates.length > 0 && (
              <div className="border-t border-zinc-100 px-5 py-3 text-xs text-amber-700 dark:border-zinc-800 dark:text-amber-300">
                Possible duplicate: {c.possibleDuplicates.map((d) => d.name).join(', ')}
              </div>
            )}
          </Card>
          <Card>
            <CardHeader title="CV and files" />
            <div className="px-5 py-4">
              <AttachmentList owner="Candidate" ownerId={c.id} emptyText="No CV." />
            </div>
          </Card>
        </div>
      </div>

      {rejecting && <RejectModal card={{ id: a.id, name }} onClose={() => setRejecting(false)} />}
      {scheduling && <ScheduleModal a={a} onClose={() => setScheduling(false)} />}
      {offering && <OfferModal a={a} onClose={() => setOffering(false)} />}
      {hiring && <HireModal a={a} name={name} onClose={() => setHiring(false)} />}
    </>
  );
}

function InterviewItem({ i, canRun }: { i: Interview; canRun: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [giving, setGiving] = useState(false);
  const started = new Date(/[zZ]$/.test(i.startsAtUtc) ? i.startsAtUtc : `${i.startsAtUtc}Z`).getTime() <= Date.now();
  const status = useMutation({
    mutationFn: (s: Interview['status']) => recruitmentApi.setInterviewStatus(i.id, s),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.recruitment }),
    onError: (e) => toast.error(e),
  });
  const statusTone = i.status === 'Completed' ? 'green' : i.status === 'Scheduled' ? 'blue' : 'gray';
  return (
    <li className="px-5 py-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="font-medium text-zinc-900 dark:text-white">
            {i.title} <span className="text-xs font-normal text-zinc-500 dark:text-zinc-400">round {i.round}</span>
          </p>
          <p className="text-xs text-zinc-500 dark:text-zinc-400">
            {formatDateTimeUtc(i.startsAtUtc)} · {i.durationMinutes} min · {MODE_LABELS[i.mode]}
            {i.where && (
              <>
                {' · '}
                {/^https?:\/\//.test(i.where) ? (
                  <a href={i.where} target="_blank" rel="noreferrer noopener" className="text-accent-600 hover:underline">
                    {i.where}
                  </a>
                ) : (
                  i.where
                )}
              </>
            )}
          </p>
          <p className="text-xs text-zinc-500 dark:text-zinc-400">With {i.interviewers.join(', ')}</p>
          {i.notes && <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">{i.notes}</p>}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Badge tone={statusTone}>{i.status === 'NoShow' ? 'No-show' : i.status}</Badge>
          {i.iAmInterviewer && started && (i.status === 'Scheduled' || i.status === 'Completed') && (
            <Button size="sm" variant={i.iGaveFeedback ? 'ghost' : 'primary'} icon={<MessageSquare className="size-3.5" />} onClick={() => setGiving(true)}>
              {i.iGaveFeedback ? 'Edit my feedback' : 'Give feedback'}
            </Button>
          )}
          {canRun && i.status === 'Scheduled' && (
            <Select className="h-8 w-36 text-xs" value="" onChange={(e) => e.target.value && status.mutate(e.target.value as Interview['status'])}>
              <option value="">Mark as…</option>
              <option value="Completed">Completed</option>
              <option value="NoShow">No-show</option>
              <option value="Cancelled">Cancelled</option>
            </Select>
          )}
        </div>
      </div>
      {i.feedback.length > 0 && (
        <ul className="mt-3 space-y-2">
          {i.feedback.map((f) => (
            <li key={f.interviewerEmployeeId} className="rounded-lg bg-zinc-50 px-3 py-2 text-sm dark:bg-zinc-800/50">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{f.interviewer}</span>
                <Stars value={f.rating} size="sm" />
                <Badge tone={f.recommendation === 'StrongYes' || f.recommendation === 'Yes' ? 'green' : 'red'}>{RECOMMENDATION_LABELS[f.recommendation]}</Badge>
              </div>
              {f.strengths && (
                <p className="mt-1 text-xs">
                  <span className="text-zinc-500 dark:text-zinc-400">Strengths:</span> {f.strengths}
                </p>
              )}
              {f.concerns && (
                <p className="text-xs">
                  <span className="text-zinc-500 dark:text-zinc-400">Concerns:</span> {f.concerns}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
      {i.feedbackHidden > 0 && (
        <p className="mt-2 inline-flex items-center gap-1 text-xs text-zinc-500 dark:text-zinc-400">
          <Lock className="size-3" />
          {i.feedbackHidden} other {i.feedbackHidden === 1 ? 'verdict is' : 'verdicts are'} hidden until you give yours.
        </p>
      )}
      {giving && <FeedbackModal i={i} onClose={() => setGiving(false)} />}
    </li>
  );
}

function FeedbackModal({ i, onClose }: { i: Interview; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const me = useUser();
  const mine = i.feedback.find((f) => f.interviewerEmployeeId === me.employeeId);
  const [rating, setRating] = useState<number | null>(mine?.rating ?? null);
  const [rec, setRec] = useState<HireRecommendation | null>(mine?.recommendation ?? null);
  const [strengths, setStrengths] = useState(mine?.strengths ?? '');
  const [concerns, setConcerns] = useState(mine?.concerns ?? '');
  const save = useMutation({
    mutationFn: () =>
      recruitmentApi.feedback(i.id, {
        rating: rating!,
        recommendation: rec!,
        strengths: strengths.trim() || null,
        concerns: concerns.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Feedback saved');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Feedback: ${i.title}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!rating || !rec} onClick={() => save.mutate()}>
            Save feedback
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Overall rating" required>
          <Stars value={rating} onChange={setRating} />
        </Field>
        <Field label="Would you hire?" required>
          <div className="flex flex-wrap gap-2">
            {(['StrongNo', 'No', 'Yes', 'StrongYes'] as HireRecommendation[]).map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => setRec(r)}
                className={cn(
                  'rounded-lg border px-3 py-1.5 text-sm',
                  rec === r
                    ? r.endsWith('Yes')
                      ? 'border-emerald-500 bg-emerald-50 text-emerald-800 dark:bg-emerald-500/15 dark:text-emerald-200'
                      : 'border-red-500 bg-red-50 text-red-800 dark:bg-red-500/15 dark:text-red-200'
                    : 'border-zinc-200 dark:border-zinc-700',
                )}
              >
                {RECOMMENDATION_LABELS[r]}
              </button>
            ))}
          </div>
        </Field>
        <Field label="Strengths">
          <Textarea rows={3} value={strengths} maxLength={2000} onChange={(e) => setStrengths(e.target.value)} />
        </Field>
        <Field label="Concerns">
          <Textarea rows={3} value={concerns} maxLength={2000} onChange={(e) => setConcerns(e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function ScheduleModal({ a, onClose }: { a: Application; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useQuery({ queryKey: qk.recruitmentPeople, queryFn: recruitmentApi.people, staleTime: 5 * 60 * 1000 });
  const round = a.interviews.filter((i) => i.status !== 'Cancelled').length + 1;
  const [f, setF] = useState({
    title: round === 1 ? 'First interview' : `Round ${round}`,
    when: '',
    duration: '60',
    mode: 'Onsite' as InterviewMode,
    where: '',
    notes: '',
    email: true,
  });
  const [ids, setIds] = useState<string[]>(a.hiringManagerEmployeeId ? [a.hiringManagerEmployeeId] : []);
  const [search, setSearch] = useState('');
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () =>
      recruitmentApi.schedule(a.id, {
        title: f.title.trim(),
        startsAtUtc: new Date(f.when).toISOString(),
        durationMinutes: Number(f.duration),
        mode: f.mode,
        where: f.where.trim() || null,
        interviewerIds: ids,
        notes: f.notes.trim() || null,
        emailCandidate: f.email,
      }),
    onSuccess: () => {
      toast.success('Interview scheduled', 'The interviewers have been told.');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onClose();
    },
  });
  const shown = people.data?.filter((p) => ids.includes(p.id) || p.name.toLowerCase().includes(search.toLowerCase())) ?? [];
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Schedule an interview"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.title.trim() || !f.when || !ids.length} onClick={() => save.mutate()}>
            Schedule
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Interview" required className="sm:col-span-2">
          <Input value={f.title} maxLength={150} onChange={(e) => set('title', e.target.value)} placeholder="Technical round" />
        </Field>
        <Field label="When" required>
          <Input type="datetime-local" value={f.when} min={`${todayIso()}T00:00`} onChange={(e) => set('when', e.target.value)} />
        </Field>
        <Field label="Length">
          <Select value={f.duration} onChange={(e) => set('duration', e.target.value)}>
            {[15, 30, 45, 60, 90, 120].map((m) => (
              <option key={m} value={m}>
                {m < 60 ? `${m} minutes` : `${m / 60} hour${m > 60 ? 's' : ''}`}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="How">
          <Select value={f.mode} onChange={(e) => set('mode', e.target.value as InterviewMode)}>
            {(Object.keys(MODE_LABELS) as InterviewMode[]).map((m) => (
              <option key={m} value={m}>
                {MODE_LABELS[m]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label={f.mode === 'Online' ? 'Meeting link' : f.mode === 'Phone' ? 'Number to call' : 'Address'}>
          <Input value={f.where} maxLength={500} onChange={(e) => set('where', e.target.value)} />
        </Field>
        <Field label="Interviewers" required hint={`${ids.length} chosen`} className="sm:col-span-2">
          <div>
            <Input className="mb-2" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search people" />
            <ul className="max-h-48 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
              {shown.map((p) => (
                <li key={p.id} className="px-3 py-1.5">
                  <Checkbox
                    label={p.name}
                    checked={ids.includes(p.id)}
                    onChange={(e) => setIds((x) => (e.target.checked ? [...x, p.id] : x.filter((y) => y !== p.id)))}
                  />
                </li>
              ))}
            </ul>
          </div>
        </Field>
        <Field label="Notes for the interviewers" className="sm:col-span-2">
          <Textarea rows={2} value={f.notes} maxLength={2000} onChange={(e) => set('notes', e.target.value)} placeholder="What to focus on" />
        </Field>
        <Checkbox
          className="sm:col-span-2"
          label={`Email ${a.candidate.firstName} the invitation`}
          checked={f.email}
          onChange={(e) => set('email', e.target.checked)}
        />
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function OfferModal({ a, onClose }: { a: Application; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [salary, setSalary] = useState(a.offeredSalary?.toString() ?? a.candidate.expectedSalary?.toString() ?? '');
  const [joining, setJoining] = useState(a.offeredJoiningDate ?? '');
  const [note, setNote] = useState(a.offerNote ?? '');
  const [email, setEmail] = useState(true);
  const save = useMutation({
    mutationFn: () =>
      recruitmentApi.offer(a.id, {
        salary: Number(salary),
        joiningDate: joining,
        note: note.trim() || null,
        emailCandidate: email,
      }),
    onSuccess: () => {
      toast.success('Offer recorded', email ? 'The candidate has been emailed.' : undefined);
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Offer to ${a.candidate.firstName}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!(Number(salary) > 0) || !joining} onClick={() => save.mutate()}>
            {email ? 'Send offer' : 'Save offer'}
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Salary (PKR a month)" required hint={a.candidate.expectedSalary ? `Expects ${formatCurrency(a.candidate.expectedSalary)}` : undefined}>
          <Input type="number" min={1} value={salary} onChange={(e) => setSalary(e.target.value)} />
        </Field>
        <Field label="Joining date" required>
          <Input type="date" min={todayIso()} value={joining} onChange={(e) => setJoining(e.target.value)} />
        </Field>
        <Field label="Note to the candidate" className="sm:col-span-2" hint="Benefits, probation, documents to bring…">
          <Textarea rows={3} value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} />
        </Field>
        <Checkbox className="sm:col-span-2" label="Email the offer to the candidate" checked={email} onChange={(e) => setEmail(e.target.checked)} />
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function HireModal({ a, name, onClose }: { a: Application; name: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const hire = useMutation({
    mutationFn: () => recruitmentApi.hire(a.id, code.trim() || null),
    onSuccess: (r) => {
      toast.success(`${name} hired as ${r.employeeCode}`);
      qc.invalidateQueries({ queryKey: qk.recruitment });
      qc.invalidateQueries({ queryKey: qk.employees });
      qc.invalidateQueries({ queryKey: qk.onboarding });
      navigate(`/employees/${r.employeeId}`);
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Hire ${name}`}
      description="Creates the employee record with the job's department, designation and manager, the offered salary and joining date. The CV moves to their documents, and onboarding starts if a template is set to start on its own."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="success" loading={hire.isPending} onClick={() => hire.mutate()}>
            Hire
          </Button>
        </>
      }
    >
      <dl className="mb-4 grid grid-cols-2 gap-3 text-sm">
        <div>
          <dt className="text-xs text-zinc-500 dark:text-zinc-400">Salary</dt>
          <dd className="font-medium">{formatCurrency(a.offeredSalary)}</dd>
        </div>
        <div>
          <dt className="text-xs text-zinc-500 dark:text-zinc-400">Joining</dt>
          <dd className="font-medium">{formatDate(a.offeredJoiningDate)}</dd>
        </div>
      </dl>
      <Field label="Employee code" hint="Leave empty for the next number">
        <Input value={code} maxLength={20} onChange={(e) => setCode(e.target.value)} />
      </Field>
      <FormError message={hire.isError ? errorMessage(hire.error) : null} />
    </Modal>
  );
}

function Notes({ a }: { a: Application }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [text, setText] = useState('');
  const add = useMutation({
    mutationFn: () => recruitmentApi.note(a.id, text.trim()),
    onSuccess: () => {
      setText('');
      qc.invalidateQueries({ queryKey: qk.application(a.id) });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader title={a.canRun ? 'Notes and history' : 'Notes'} description="Visible to the hiring team and interviewers, never to the candidate." />
      <div className="border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <Textarea rows={2} value={text} maxLength={2000} onChange={(e) => setText(e.target.value)} placeholder="Add a note…" />
        <div className="mt-2 flex justify-end">
          <Button size="sm" disabled={!text.trim()} loading={add.isPending} onClick={() => add.mutate()}>
            Add note
          </Button>
        </div>
      </div>
      <ol className="space-y-3 px-5 py-4">
        {a.events.map((e) => (
          <li key={e.id} className="flex gap-3 text-sm">
            <span className={cn('mt-1.5 size-2 shrink-0 rounded-full', e.isNote ? 'bg-accent-500' : 'bg-zinc-300 dark:bg-zinc-600')} />
            <div className="min-w-0">
              <p className={cn('whitespace-pre-line', e.isNote ? 'text-zinc-900 dark:text-zinc-100' : 'text-zinc-600 dark:text-zinc-400')}>{e.text}</p>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                {e.byName ? `${e.byName} · ` : ''}
                {formatDateTimeUtc(e.createdAtUtc)}
              </p>
            </div>
          </li>
        ))}
        {!a.events.length && <p className="text-sm text-zinc-500 dark:text-zinc-400">Nothing yet.</p>}
      </ol>
    </Card>
  );
}
