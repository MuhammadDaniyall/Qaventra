import { useState, type DragEvent } from 'react';
import { ScreeningCard } from './Screening';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Ban, CalendarClock, Copy, MapPin, Pencil, Plus, Trash2, UserPlus } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import {
  CLOSED_STAGES,
  EMPLOYMENT_LABELS,
  EMPTY_CANDIDATE,
  PIPELINE,
  recruitmentApi,
  SOURCE_LABELS,
  STAGE_LABELS,
  type ApplicationStage,
  type CandidateFormValues,
  type JobPostStatus,
  type PipelineCard,
} from '../../api/recruitment';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Modal,
  PageHeader,
  SkeletonRows,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate } from '../../lib/format';
import { relative } from '../helpdesk/shared';
import { CandidateFields, careersUrl, cvProblem, JobPostModal, PostStatusBadge, Stars, StageBadge, useCopy } from './shared';

type Column = ApplicationStage | 'Closed';
const COLUMNS: Column[] = [...PIPELINE, 'Closed'];

/** One job: details, status and its pipeline as a board. Cards drag between stages. */
export function JobPage() {
  const { id = '' } = useParams();
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const copy = useCopy();
  const job = useQuery({
    queryKey: qk.jobPost(id),
    queryFn: () => recruitmentApi.job(id),
  });
  const cards = useQuery({
    queryKey: qk.pipeline(id),
    queryFn: () => recruitmentApi.pipeline(id),
  });
  const me = useQuery({
    queryKey: qk.recruitmentMe,
    queryFn: recruitmentApi.me,
  });
  const [editing, setEditing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [rejecting, setRejecting] = useState<PipelineCard | null>(null);
  const [dragOver, setDragOver] = useState<Column | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: qk.recruitment });
  const status = useMutation({
    mutationFn: (s: JobPostStatus) => recruitmentApi.setJobStatus(id, s),
    onSuccess: (_, s) => {
      toast.success(
        s === 'Open' ? 'Job is open' : s === 'Closed' ? 'Applications closed' : 'Marked filled',
        s === 'Open' ? 'It is on the careers page now.' : undefined,
      );
      refresh();
    },
    onError: (e) => toast.error(e),
  });
  const move = useMutation({
    mutationFn: ({ card, stage }: { card: PipelineCard; stage: ApplicationStage }) => recruitmentApi.move(card.id, stage, null),
    onMutate: async ({ card, stage }) => {
      await qc.cancelQueries({ queryKey: qk.pipeline(id) });
      const before = qc.getQueryData<PipelineCard[]>(qk.pipeline(id));
      qc.setQueryData<PipelineCard[]>(qk.pipeline(id), (list) => list?.map((c) => (c.id === card.id ? { ...c, stage } : c)));
      return { before };
    },
    onError: (e, _, ctx) => {
      qc.setQueryData(qk.pipeline(id), ctx?.before);
      toast.error(e);
    },
    onSettled: refresh,
  });
  const remove = useMutation({
    mutationFn: () => recruitmentApi.deleteJob(id),
    onSuccess: () => {
      toast.success('Job deleted');
      refresh();
      navigate('/recruitment');
    },
    onError: (e) => toast.error(e),
  });

  if (job.isLoading) return <SkeletonRows rows={6} cols={4} />;
  if (job.isError) return <ErrorState error={job.error} onRetry={() => job.refetch()} />;
  const j = job.data!;
  const slug = me.data?.careersSlug;
  const hr = j.canEdit;

  const drop = (column: Column, e: DragEvent) => {
    e.preventDefault();
    setDragOver(null);
    const card = cards.data?.find((c) => c.id === e.dataTransfer.getData('text/plain'));
    if (!card || card.stage === column || (column === 'Closed' && CLOSED_STAGES.includes(card.stage))) return;
    if (card.stage === 'Hired') return toast.error('This candidate has been hired.');
    if (column === 'Closed') return setRejecting(card);
    if (column === 'Hired' || (column === 'Offer' && !card.offerStatus)) {
      toast.info(column === 'Hired' ? 'Hire from the candidate page' : 'Make the offer from the candidate page');
      return navigate(`/recruitment/applications/${card.id}`);
    }
    move.mutate({ card, stage: column });
  };
  const inColumn = (c: Column) => cards.data?.filter((x) => (c === 'Closed' ? CLOSED_STAGES.includes(x.stage) : x.stage === c)) ?? [];

  return (
    <>
      <PageHeader
        back={{ to: '/recruitment', label: 'Recruitment' }}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {j.title} <PostStatusBadge status={j.status} />
          </span>
        }
        description={
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            {j.department && <span>{j.department}</span>}
            {j.location && (
              <span className="inline-flex items-center gap-1">
                <MapPin className="size-3" />
                {j.location}
              </span>
            )}
            <span>{EMPLOYMENT_LABELS[j.employmentType]}</span>
            <span>
              {j.byStage.Hired ?? 0}/{j.positions} hired
            </span>
            {(j.salaryMin || j.salaryMax) && (
              <span>
                {formatCurrency(j.salaryMin)}–{formatCurrency(j.salaryMax)}
              </span>
            )}
            {j.closesOn && <span>Closes {formatDate(j.closesOn)}</span>}
          </span>
        }
        actions={
          <div className="flex flex-wrap gap-2">
            {j.status === 'Open' && slug && (
              <Button variant="secondary" size="sm" icon={<Copy className="size-3.5" />} onClick={() => copy(careersUrl(slug, j.slug), 'Job link')}>
                Copy job link
              </Button>
            )}
            {j.status !== 'Filled' && (
              <Button variant="secondary" size="sm" icon={<UserPlus className="size-3.5" />} onClick={() => setAdding(true)}>
                Add candidate
              </Button>
            )}
            {hr && (
              <>
                {j.status !== 'Open' && (
                  <Button size="sm" loading={status.isPending} onClick={() => status.mutate('Open')}>
                    {j.status === 'Draft' ? 'Open job' : 'Reopen'}
                  </Button>
                )}
                {j.status === 'Open' && (
                  <Button size="sm" variant="secondary" loading={status.isPending} onClick={() => status.mutate('Closed')}>
                    Close applications
                  </Button>
                )}
                {j.status !== 'Filled' && j.status !== 'Draft' && (
                  <Button size="sm" variant="ghost" onClick={() => status.mutate('Filled')}>
                    Mark filled
                  </Button>
                )}
                <Button size="sm" variant="ghost" icon={<Pencil className="size-3.5" />} onClick={() => setEditing(true)} disabled={j.status === 'Filled'}>
                  Edit
                </Button>
                {j.total === 0 && (
                  <Button size="sm" variant="ghost" icon={<Trash2 className="size-3.5" />} onClick={() => setDeleting(true)}>
                    Delete
                  </Button>
                )}
              </>
            )}
          </div>
        }
      />

      {j.status === 'Draft' && hr && (
        <div className="mb-4 rounded-lg bg-amber-50 px-4 py-2.5 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
          This job is a draft. Open it to show it on the careers page and the internal job board.
        </div>
      )}

      {cards.isLoading ? (
        <SkeletonRows rows={5} cols={6} />
      ) : cards.isError ? (
        <ErrorState error={cards.error} onRetry={() => cards.refetch()} />
      ) : !cards.data?.length ? (
        <Card>
          <EmptyState
            icon={UserPlus}
            title="No candidates yet"
            description={
              j.status === 'Open'
                ? 'Share the job link, or add people who sent their CV another way.'
                : 'Open the job to take applications, or add people yourself.'
            }
            action={
              <Button icon={<Plus className="size-4" />} onClick={() => setAdding(true)}>
                Add candidate
              </Button>
            }
          />
        </Card>
      ) : (
        <div className="-mx-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:px-0">
          <div className="grid min-w-[72rem] grid-cols-7 gap-3">
            {COLUMNS.map((col) => {
              const list = inColumn(col);
              return (
                <section
                  key={col}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setDragOver(col);
                  }}
                  onDragLeave={() => setDragOver((c) => (c === col ? null : c))}
                  onDrop={(e) => drop(col, e)}
                  className={cn(
                    'flex min-h-72 flex-col rounded-xl bg-zinc-100/70 p-2 transition dark:bg-zinc-900/60',
                    dragOver === col && 'ring-2 ring-accent-400',
                  )}
                  aria-label={col === 'Closed' ? 'Not going ahead' : STAGE_LABELS[col]}
                >
                  <h2 className="mb-2 flex items-center justify-between px-1 text-xs font-semibold tracking-wide text-zinc-500 uppercase">
                    {col === 'Closed' ? 'Not going ahead' : STAGE_LABELS[col]}
                    <span className="rounded-full bg-white px-1.5 text-[11px] text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">{list.length}</span>
                  </h2>
                  <div className="flex flex-1 flex-col gap-2">
                    {list.map((c) => (
                      <CandidateCard key={c.id} card={c} />
                    ))}
                  </div>
                </section>
              );
            })}
          </div>
          <p className="mt-2 text-xs text-zinc-500">Drag a card to move the candidate. Offers and hiring happen on the candidate's page.</p>
        </div>
      )}

      {editing && <JobPostModal post={j} onClose={() => setEditing(false)} />}
      {adding && <AddCandidateModal jobId={id} onClose={() => setAdding(false)} />}
      {rejecting && <RejectModal card={rejecting} onClose={() => setRejecting(null)} />}
      <ConfirmDialog
        open={deleting}
        title="Delete this job?"
        message="It has no candidates. This cannot be undone."
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={() => setDeleting(false)}
      />
      {hr && <ScreeningCard jobId={id} />}
    </>
  );
}

function CandidateCard({ card }: { card: PipelineCard }) {
  return (
    <Link
      to={`/recruitment/applications/${card.id}`}
      draggable={card.stage !== 'Hired'}
      onDragStart={(e) => e.dataTransfer.setData('text/plain', card.id)}
      className="block rounded-lg border border-zinc-200 bg-white p-2.5 text-left shadow-xs transition hover:border-accent-300 dark:border-zinc-700 dark:bg-zinc-900"
    >
      <p className="flex items-center gap-1 text-sm font-medium text-zinc-900 dark:text-white">
        <span className="truncate">{card.name}</span>
        {card.isBlacklisted && <Ban className="size-3.5 shrink-0 text-red-500" aria-label="Do not hire" />}
      </p>
      <p className="truncate text-xs text-zinc-500">
        {[card.currentTitle, card.experienceYears != null ? `${card.experienceYears} yrs` : null].filter(Boolean).join(' · ') || card.email}
      </p>
      <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
        {card.rating ? <Stars value={card.rating} size="sm" /> : null}
        {card.interviews > 0 && (
          <span className="inline-flex items-center gap-0.5 text-[11px] text-zinc-500">
            <CalendarClock className="size-3" />
            {card.interviews}
            {card.averageFeedback != null && ` · ${card.averageFeedback.toFixed(1)}/5`}
          </span>
        )}
        {card.source === 'Referral' && <Badge tone="violet">Referral</Badge>}
        {card.offerStatus && card.stage === 'Offer' && (
          <Badge tone={card.offerStatus === 'Accepted' ? 'green' : 'amber'}>Offer {card.offerStatus.toLowerCase()}</Badge>
        )}
        {CLOSED_STAGES.includes(card.stage) && <StageBadge stage={card.stage} />}
      </div>
      <p className="mt-1 text-[11px] text-zinc-400" title={SOURCE_LABELS[card.source]}>
        {relative(card.stageChangedAtUtc ?? card.appliedAtUtc)}
      </p>
    </Link>
  );
}

function AddCandidateModal({ jobId, onClose }: { jobId: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [v, setV] = useState<CandidateFormValues>({
    ...EMPTY_CANDIDATE,
    source: 'Manual',
  });
  const problem = cvProblem(v, false);
  const add = useMutation({
    mutationFn: () => recruitmentApi.addCandidate(jobId, v),
    onSuccess: () => {
      toast.success('Candidate added');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title="Add a candidate"
      description="Someone who sent a CV by email, walked in or came through an agency. If the email is already known, their record is reused."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={add.isPending} disabled={!!problem} onClick={() => add.mutate()}>
            Add
          </Button>
        </>
      }
    >
      <CandidateFields value={v} onChange={setV} withSource />
      <FormError message={add.isError ? errorMessage(add.error) : null} />
    </Modal>
  );
}

export function RejectModal({ card, onClose }: { card: { id: string; name: string }; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [stage, setStage] = useState<ApplicationStage>('Rejected');
  const [reason, setReason] = useState('');
  const [email, setEmail] = useState(true);
  const save = useMutation({
    mutationFn: () => recruitmentApi.move(card.id, stage, reason.trim() || null, stage === 'Rejected' && email),
    onSuccess: () => {
      toast.success(`${card.name}: ${STAGE_LABELS[stage].toLowerCase()}`);
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Not going ahead with ${card.name}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            variant={stage === 'Rejected' ? 'danger' : 'primary'}
            loading={save.isPending}
            disabled={stage === 'Rejected' && !reason.trim()}
            onClick={() => save.mutate()}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="flex gap-2">
          {(['Rejected', 'Withdrawn', 'OnHold'] as ApplicationStage[]).map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setStage(s)}
              className={cn(
                'rounded-lg border px-3 py-1.5 text-sm',
                stage === s
                  ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                  : 'border-zinc-200 dark:border-zinc-700',
              )}
            >
              {s === 'Withdrawn' ? 'Candidate withdrew' : STAGE_LABELS[s]}
            </button>
          ))}
        </div>
        <Field label="Reason" required={stage === 'Rejected'} hint="Kept for the team; never sent to the candidate">
          <Textarea rows={3} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </Field>
        {stage === 'Rejected' && <Checkbox label="Email the candidate a polite regret" checked={email} onChange={(e) => setEmail(e.target.checked)} />}
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
