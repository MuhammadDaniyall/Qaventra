import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { Check, Plus, Send, Trash2, Undo2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { performanceApi, POTENTIAL_LABELS, STAGE_LABELS, type Review, type ReviewItem } from '../../api/performance';
import { Badge, Button, Card, CardBody, CardHeader, ConfirmDialog, ErrorState, Field, FormError, IconButton, Input, PageHeader, PageSpinner, Select, Textarea, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate } from '../../lib/format';
import { RatingPicker, StageBar } from './shared';

export function ReviewPage() {
  const { id = '' } = useParams();
  const review = useQuery({ queryKey: qk.review(id), queryFn: () => performanceApi.get(id) });
  if (review.isLoading) return <PageSpinner />;
  if (review.isError || !review.data)
    return (
      <>
        <PageHeader title="Review" back={{ to: '/performance', label: 'Performance' }} />
        <Card>
          <ErrorState error={review.error} onRetry={() => review.refetch()} />
        </Card>
      </>
    );
  return <ReviewView r={review.data} />;
}

function useSave(id: string) {
  const qc = useQueryClient();
  return (r: Review) => {
    qc.setQueryData(qk.review(id), r);
    qc.invalidateQueries({ queryKey: qk.performance });
  };
}

function ReviewView({ r }: { r: Review }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [reverting, setReverting] = useState(false);
  const revert = useMutation({
    mutationFn: () => performanceApi.revert(r.id),
    onSuccess: () => {
      toast.success('Sent back one stage');
      setReverting(false);
      qc.invalidateQueries({ queryKey: qk.performance });
    },
    onError: (err) => toast.error(err),
  });
  const goalsTurn = r.stage === 'Goals' && r.isEmployee;
  const goalsApproval = (r.stage === 'Goals' || r.stage === 'GoalsSubmitted') && r.isReviewer;
  return (
    <>
      <PageHeader
        back={{ to: '/performance', label: 'Performance' }}
        title={r.isEmployee ? `My review · ${r.cycle}` : `${r.employeeName} · ${r.cycle}`}
        description={`${formatDate(r.periodFrom)} – ${formatDate(r.periodTo)}${r.reviewer ? ` · Reviewer: ${r.reviewer}` : ''}${r.due ? ` · Due ${formatDate(r.due)}` : ''}`}
        actions={
          r.isHr &&
          r.stage !== 'Goals' && (
            <Button variant="ghost" icon={<Undo2 className="size-4" />} onClick={() => setReverting(true)}>
              Send back a stage
            </Button>
          )
        }
      />
      <StageBar stage={r.stage} selfReview={r.selfReviewEnabled} hasGoals={r.sections.some((s) => s.kind === 'Goals')} className="mb-6" />
      {goalsTurn || goalsApproval ? (
        <GoalsEditor r={r} />
      ) : r.stage === 'SelfReview' && r.isEmployee ? (
        <ScoreForm r={r} mode="self" />
      ) : r.stage === 'ManagerReview' && r.isReviewer ? (
        <ScoreForm r={r} mode="manager" />
      ) : (
        <ReadOnly r={r} />
      )}
      {r.stage === 'SignOff' && r.isEmployee && <SignOff r={r} />}
      {r.stage === 'Calibration' && r.isHr && <CalibrateCard r={r} />}
      <ConfirmDialog
        open={reverting}
        title="Send this review back a stage?"
        message={`It goes back from "${STAGE_LABELS[r.stage]}" so the previous person can change it.`}
        confirmLabel="Send back"
        loading={revert.isPending}
        onConfirm={() => revert.mutate()}
        onClose={() => setReverting(false)}
      />
    </>
  );
}

type GoalDraft = { sectionIndex: number; title: string; description: string; target: string; weight: string };

function GoalsEditor({ r }: { r: Review }) {
  const toast = useToast();
  const save = useSave(r.id);
  const goalSections = r.sections.map((s, i) => ({ s, i })).filter((x) => x.s.kind === 'Goals');
  const [goals, setGoals] = useState<GoalDraft[]>(() =>
    r.items
      .filter((i) => goalSections.some((g) => g.i === i.sectionIndex))
      .map((i) => ({ sectionIndex: i.sectionIndex, title: i.title, description: i.description ?? '', target: i.target ?? '', weight: String(i.weight) })),
  );
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState('');
  const payload = () => goals.filter((g) => g.title.trim()).map((g) => ({ sectionIndex: g.sectionIndex, title: g.title.trim(), description: g.description.trim() || null, target: g.target.trim() || null, weight: Number(g.weight) || 0 }));
  const run = useMutation({
    mutationFn: async (what: 'save' | 'submit' | 'approve' | 'return') => {
      if (what === 'return') return performanceApi.decideGoals(r.id, false, note.trim() || null);
      const saved = await performanceApi.saveGoals(r.id, payload());
      if (what === 'submit') return performanceApi.submitGoals(r.id);
      if (what === 'approve') return performanceApi.decideGoals(r.id, true, note.trim() || null);
      return saved;
    },
    onSuccess: (res, what) => {
      save(res);
      toast.success({ save: 'Goals saved', submit: 'Goals sent for approval', approve: 'Goals approved', return: 'Sent back with your note' }[what]);
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const set = (i: number, p: Partial<GoalDraft>) => setGoals((g) => g.map((x, j) => (j === i ? { ...x, ...p } : x)));
  return (
    <div className="space-y-6">
      <FormError message={error} />
      {r.isReviewer && r.stage === 'Goals' && <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">{r.employeeName} has not sent their goals yet. You can write or edit them yourself and approve.</p>}
      {goalSections.map(({ s, i: si }) => {
        const mine = goals.map((g, i) => ({ g, i })).filter((x) => x.g.sectionIndex === si);
        const total = mine.reduce((t, x) => t + (Number(x.g.weight) || 0), 0);
        return (
          <Card key={si}>
            <CardHeader title={`${s.name} · ${s.weight}% of the review`} description="Each goal's weight is its share of this section; they add up to 100." actions={<Badge tone={total === 100 ? 'green' : 'amber'}>{total} / 100</Badge>} />
            <CardBody className="space-y-3">
              {mine.map(({ g, i }) => (
                <div key={i} className="grid gap-2 rounded-lg border border-zinc-200 p-3 sm:grid-cols-[1fr_90px_auto] dark:border-zinc-800">
                  <Input value={g.title} maxLength={300} placeholder="Goal" onChange={(e) => set(i, { title: e.target.value })} aria-label="Goal" />
                  <Input type="number" min={1} max={100} value={g.weight} onChange={(e) => set(i, { weight: e.target.value })} aria-label="Weight" />
                  <IconButton label="Remove goal" onClick={() => setGoals((list) => list.filter((_, j) => j !== i))}>
                    <Trash2 className="size-4" />
                  </IconButton>
                  <Input className="sm:col-span-3" value={g.target} maxLength={1000} placeholder="What success looks like (measure, target, date)" onChange={(e) => set(i, { target: e.target.value })} aria-label="Target" />
                </div>
              ))}
              <Button variant="ghost" size="sm" icon={<Plus className="size-3.5" />} onClick={() => setGoals((g) => [...g, { sectionIndex: si, title: '', description: '', target: '', weight: '' }])}>
                Add goal
              </Button>
            </CardBody>
          </Card>
        );
      })}
      {r.sections.filter((s) => s.kind === 'Competencies').length > 0 && (
        <p className="text-sm text-zinc-500 dark:text-zinc-400">Also rated: {r.sections.filter((s) => s.kind === 'Competencies').map((s) => `${s.name} (${s.items.map((i) => i.title).join(', ')})`).join('; ')}.</p>
      )}
      <Card>
        <CardBody className="flex flex-wrap items-center justify-end gap-2">
          {r.isReviewer && (
            <Input className="min-w-60 flex-1" value={note} maxLength={1000} placeholder="Note to the employee (optional)" onChange={(e) => setNote(e.target.value)} aria-label="Note" />
          )}
          <Button variant="secondary" loading={run.isPending} onClick={() => run.mutate('save')}>
            Save draft
          </Button>
          {r.isEmployee && (
            <Button icon={<Send className="size-4" />} loading={run.isPending} onClick={() => run.mutate('submit')}>
              Send for approval
            </Button>
          )}
          {r.isReviewer && r.stage === 'GoalsSubmitted' && (
            <Button variant="secondary" loading={run.isPending} onClick={() => run.mutate('return')}>
              Send back
            </Button>
          )}
          {r.isReviewer && (
            <Button icon={<Check className="size-4" />} loading={run.isPending} onClick={() => run.mutate('approve')}>
              Approve goals
            </Button>
          )}
        </CardBody>
      </Card>
    </div>
  );
}

function ScoreForm({ r, mode }: { r: Review; mode: 'self' | 'manager' }) {
  const toast = useToast();
  const save = useSave(r.id);
  const self = mode === 'self';
  const [scores, setScores] = useState<Record<string, { score: number | null; comment: string }>>(() =>
    Object.fromEntries(r.items.map((i) => [i.id, { score: self ? i.selfScore : i.managerScore, comment: (self ? i.selfComment : i.managerComment) ?? '' }])),
  );
  const [comment, setComment] = useState((self ? r.selfComment : r.managerComment) ?? '');
  const [strengths, setStrengths] = useState(r.strengths ?? '');
  const [improvements, setImprovements] = useState(r.improvements ?? '');
  const [plan, setPlan] = useState(r.developmentPlan ?? '');
  const [potential, setPotential] = useState(r.potential ? String(r.potential) : '');
  const [error, setError] = useState<string | null>(null);
  useEffect(() => setError(null), [scores]);
  const run = useMutation({
    mutationFn: (submit: boolean) => {
      const list = Object.entries(scores).map(([itemId, v]) => ({ itemId, score: v.score, comment: v.comment.trim() || null }));
      return self
        ? performanceApi.saveSelf(r.id, { scores: list, comment: comment.trim() || null, submit })
        : performanceApi.saveManager(r.id, {
            scores: list,
            comment: comment.trim() || null,
            strengths: strengths.trim() || null,
            improvements: improvements.trim() || null,
            developmentPlan: plan.trim() || null,
            potential: potential ? Number(potential) : null,
            submit,
          });
    },
    onSuccess: (res, submit) => {
      save(res);
      toast.success(submit ? (self ? 'Self review sent to your manager' : 'Review sent to HR') : 'Draft saved');
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const rated = Object.values(scores).filter((v) => v.score).length;
  return (
    <div className="space-y-6">
      <FormError message={error} />
      {r.sections.map((s, si) => (
        <Card key={si}>
          <CardHeader title={`${s.name} · ${s.weight}%`} />
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {r.items.filter((i) => i.sectionIndex === si).map((i) => (
              <li key={i.id} className="space-y-3 px-5 py-4">
                <ItemHeader i={i} />
                {!self && i.selfScore && (
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    Self rating: <span className="font-medium text-zinc-700 dark:text-zinc-300">{i.selfScore}</span>
                    {i.selfComment ? ` · “${i.selfComment}”` : ''}
                  </p>
                )}
                <RatingPicker value={scores[i.id]?.score ?? null} onChange={(v) => setScores((x) => ({ ...x, [i.id]: { ...x[i.id], score: v } }))} />
                <Input value={scores[i.id]?.comment ?? ''} maxLength={2000} placeholder="Comment (optional)" onChange={(e) => setScores((x) => ({ ...x, [i.id]: { ...x[i.id], comment: e.target.value } }))} aria-label={`Comment on ${i.title}`} />
              </li>
            ))}
          </ul>
        </Card>
      ))}
      <Card>
        <CardBody className="grid gap-4 sm:grid-cols-2">
          <Field label={self ? 'Overall, how did your year go?' : 'Overall comment'} className="sm:col-span-2">
            <Textarea rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} />
          </Field>
          {!self && (
            <>
              {r.selfComment && <p className="text-sm text-zinc-600 sm:col-span-2 dark:text-zinc-400">Their own summary: “{r.selfComment}”</p>}
              <Field label="Strengths">
                <Textarea rows={2} maxLength={4000} value={strengths} onChange={(e) => setStrengths(e.target.value)} />
              </Field>
              <Field label="Areas to develop">
                <Textarea rows={2} maxLength={4000} value={improvements} onChange={(e) => setImprovements(e.target.value)} />
              </Field>
              <Field label="Development plan">
                <Textarea rows={2} maxLength={4000} value={plan} onChange={(e) => setPlan(e.target.value)} />
              </Field>
              <Field label="Potential" hint="For the 9-box; HR only">
                <Select value={potential} onChange={(e) => setPotential(e.target.value)}>
                  <option value="">Choose…</option>
                  <option value="1">Low: at their level</option>
                  <option value="2">Medium: could grow one step</option>
                  <option value="3">High: ready for more</option>
                </Select>
              </Field>
            </>
          )}
        </CardBody>
        <div className="flex flex-wrap items-center justify-between gap-2 border-t border-zinc-100 px-5 py-3 dark:border-zinc-800">
          <span className="text-sm text-zinc-500 dark:text-zinc-400">
            {rated} of {r.items.length} rated
          </span>
          <div className="flex gap-2">
            <Button variant="secondary" loading={run.isPending} onClick={() => run.mutate(false)}>
              Save draft
            </Button>
            <Button icon={<Send className="size-4" />} loading={run.isPending} onClick={() => run.mutate(true)}>
              {self ? 'Send to my manager' : 'Send to HR'}
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}

function ItemHeader({ i }: { i: ReviewItem }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-2">
      <div>
        <p className="font-medium text-zinc-900 dark:text-zinc-100">{i.title}</p>
        {i.target && <p className="text-xs text-zinc-500 dark:text-zinc-400">Target: {i.target}</p>}
        {i.description && <p className="text-xs text-zinc-500 dark:text-zinc-400">{i.description}</p>}
      </div>
      <Badge>{i.weight}%</Badge>
    </div>
  );
}

function Score({ label, value }: { label: string; value: number | null }) {
  return (
    <div className="rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
      <p className="text-xs text-zinc-500 dark:text-zinc-400">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value ?? '—'}</p>
    </div>
  );
}

function ReadOnly({ r }: { r: Review }) {
  const waiting =
    r.stage === 'GoalsSubmitted' && r.isEmployee
      ? 'Your goals are with your manager for approval.'
      : r.stage === 'Goals' && !r.isEmployee
        ? `${r.employeeName} is writing their goals.`
        : r.stage === 'SelfReview' && !r.isEmployee
          ? `${r.employeeName} is doing their self review.`
          : r.stage === 'ManagerReview' && r.isEmployee
            ? 'Your manager is writing your review.'
            : r.stage === 'Calibration' && r.isEmployee
              ? 'HR is finalising ratings. You will be told when your review is ready.'
              : null;
  return (
    <div className="space-y-6">
      {waiting && <p className="rounded-lg bg-sky-50 p-3 text-sm text-sky-800 dark:bg-sky-500/10 dark:text-sky-200">{waiting}</p>}
      {(r.selfScore !== null || r.managerScore !== null || r.finalRating !== null) && (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <Score label="Self score" value={r.selfScore} />
          <Score label="Manager score" value={r.managerScore} />
          <div className="col-span-2 rounded-xl border border-accent-200 bg-accent-50 p-4 dark:border-accent-500/30 dark:bg-accent-500/10">
            <p className="text-xs text-accent-700 dark:text-accent-300">Final rating</p>
            <p className="mt-1 text-2xl font-semibold text-zinc-900 dark:text-white">{r.finalRating ? `${r.finalRating} · ${r.finalLabel ?? ''}` : '—'}</p>
            {r.potential && !r.isEmployee && <p className="text-xs text-zinc-500 dark:text-zinc-400">Potential: {POTENTIAL_LABELS[r.potential]}</p>}
          </div>
        </div>
      )}
      {r.sections.map((s, si) => (
        <Card key={si}>
          <CardHeader title={`${s.name} · ${s.weight}%`} />
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {r.items.filter((i) => i.sectionIndex === si).map((i) => (
              <li key={i.id} className="space-y-1.5 px-5 py-4">
                <ItemHeader i={i} />
                <div className="flex flex-wrap gap-4 text-sm">
                  {i.selfScore !== null && (
                    <span className="text-zinc-600 dark:text-zinc-300">
                      Self <b>{i.selfScore}</b>
                      {i.selfComment ? ` · ${i.selfComment}` : ''}
                    </span>
                  )}
                  {i.managerScore !== null && (
                    <span className="text-zinc-600 dark:text-zinc-300">
                      Manager <b>{i.managerScore}</b>
                      {i.managerComment ? ` · ${i.managerComment}` : ''}
                    </span>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </Card>
      ))}
      {(r.managerComment || r.strengths || r.improvements || r.developmentPlan || r.selfComment) && (
        <Card>
          <CardBody className="grid gap-4 sm:grid-cols-2">
            {[
              ['Their summary', r.selfComment],
              ['Manager comment', r.managerComment],
              ['Strengths', r.strengths],
              ['Areas to develop', r.improvements],
              ['Development plan', r.developmentPlan],
              ['Calibration note (HR only)', r.calibrationNote],
              ['Sign-off', r.employeeAgrees === null ? null : `${r.employeeAgrees ? 'Agrees' : 'Disagrees'}${r.employeeSignOffComment ? `: ${r.employeeSignOffComment}` : ''}`],
            ]
              .filter(([, v]) => v)
              .map(([k, v]) => (
                <div key={k}>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">{k}</p>
                  <p className="text-sm whitespace-pre-line text-zinc-800 dark:text-zinc-200">{v}</p>
                </div>
              ))}
          </CardBody>
        </Card>
      )}
    </div>
  );
}

function SignOff({ r }: { r: Review }) {
  const toast = useToast();
  const save = useSave(r.id);
  const [agree, setAgree] = useState<boolean | null>(null);
  const [comment, setComment] = useState('');
  const [error, setError] = useState<string | null>(null);
  const sign = useMutation({
    mutationFn: () => performanceApi.signOff(r.id, agree!, comment.trim() || null),
    onSuccess: (res) => {
      save(res);
      toast.success('Signed off');
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Card className="mt-6 border-accent-200 dark:border-accent-500/30">
      <CardHeader title="Sign off your review" description="Tell us whether you agree. If not, say why; HR and your manager will see it." />
      <CardBody className="space-y-4">
        <FormError message={error} />
        <div className="flex gap-2">
          {[true, false].map((v) => (
            <button
              key={String(v)}
              type="button"
              aria-pressed={agree === v}
              onClick={() => setAgree(v)}
              className={cn(
                'rounded-lg border px-4 py-2 text-sm font-medium',
                agree === v ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300' : 'border-zinc-200 text-zinc-700 dark:border-zinc-700 dark:text-zinc-300',
              )}
            >
              {v ? 'I agree' : 'I disagree'}
            </button>
          ))}
        </div>
        <Field label="Comment" required={agree === false}>
          <Textarea rows={3} maxLength={4000} value={comment} onChange={(e) => setComment(e.target.value)} />
        </Field>
        <div className="flex justify-end">
          <Button disabled={agree === null} loading={sign.isPending} onClick={() => sign.mutate()}>
            Sign off
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}

function CalibrateCard({ r }: { r: Review }) {
  const toast = useToast();
  const qc = useQueryClient();
  const settings = useQuery({ queryKey: qk.performanceSettings, queryFn: performanceApi.settings });
  const [rating, setRating] = useState<number | null>(r.finalRating);
  const [note, setNote] = useState(r.calibrationNote ?? '');
  const save = useMutation({
    mutationFn: () => performanceApi.calibrate(r.id, rating!, note.trim() || null),
    onSuccess: () => {
      toast.success('Final rating saved');
      qc.invalidateQueries({ queryKey: qk.performance });
    },
    onError: (err) => toast.error(err),
  });
  return (
    <Card className="mt-6">
      <CardHeader title="Calibration" description={`Manager score ${r.managerScore ?? '—'}. Adjust the final rating if needed; release it from the cycle page.`} />
      <CardBody className="space-y-3">
        <RatingPicker value={rating} onChange={setRating} labels={settings.data?.ratingLabels} />
        <Input value={note} maxLength={4000} placeholder="Why it changed (HR only)" onChange={(e) => setNote(e.target.value)} aria-label="Calibration note" />
        <div className="flex justify-end">
          <Button disabled={!rating} loading={save.isPending} onClick={() => save.mutate()}>
            Save final rating
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}
