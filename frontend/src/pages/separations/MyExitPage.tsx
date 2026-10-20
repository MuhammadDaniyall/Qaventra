import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Circle, DoorOpen } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { separationsApi } from '../../api/separations';
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  ErrorState,
  Field,
  FormError,
  Input,
  PageHeader,
  PageSpinner,
  Select,
  StatusBadge,
  Switch,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import type { Separation } from '../../types/workflows';
import { EXIT_REASONS, SeparationSteps, Stars } from './shared';

export function MyExitPage() {
  const mine = useQuery({ queryKey: qk.mySeparation, queryFn: separationsApi.my });

  if (mine.isLoading) return <PageSpinner />;
  if (mine.isError) {
    return (
      <>
        <PageHeader title="Resignation" />
        <Card>
          <ErrorState error={mine.error} onRetry={() => mine.refetch()} />
        </Card>
      </>
    );
  }

  const s = mine.data;
  const open = s && (s.summary.status === 'Requested' || s.summary.status === 'InProgress' || s.summary.status === 'Completed');
  return (
    <>
      <PageHeader title="Resignation" description="Resign, follow your clearance and share feedback before you leave." />
      {open ? <ExitStatus s={s} /> : <ResignForm previous={s} />}
    </>
  );
}

function ResignForm({ previous }: { previous?: Separation }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [lastDay, setLastDay] = useState('');
  const [reason, setReason] = useState('');
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const resign = useMutation({
    mutationFn: () => separationsApi.resign(lastDay, reason.trim() || null),
    onSuccess: () => {
      toast.success('Resignation submitted', 'HR will review it and confirm your last working day.');
      qc.invalidateQueries({ queryKey: qk.separations });
      setConfirming(false);
    },
    onError: (err) => {
      setConfirming(false);
      setError(errorMessage(err));
    },
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!lastDay) return setError('Choose your proposed last working day.');
    setConfirming(true);
  };

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      {previous && (
        <p className="rounded-xl bg-zinc-100 px-4 py-3 text-sm text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
          Your earlier request from {formatDate(previous.summary.noticeDate)} was{' '}
          {previous.summary.status === 'Rejected' ? 'declined' : 'withdrawn'}.
        </p>
      )}
      <Card>
        <CardHeader title="Submit your resignation" description="Your notice period starts today. HR confirms the final last working day." />
        <CardBody>
          <form onSubmit={submit} className="space-y-4">
            <FormError message={error} />
            <Field label="Proposed last working day" required hint="Usually today plus your notice period (often 30 days).">
              <Input type="date" value={lastDay} min={todayIso()} onChange={(e) => setLastDay(e.target.value)} />
            </Field>
            <Field label="Reason" hint="Optional. Only HR sees this.">
              <Textarea value={reason} maxLength={2000} rows={4} onChange={(e) => setReason(e.target.value)} />
            </Field>
            <div className="flex justify-end">
              <Button type="submit" variant="danger" icon={<DoorOpen className="size-4" />}>
                Submit resignation
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>
      <ConfirmDialog
        open={confirming}
        title="Submit your resignation?"
        message={<>HR will be notified. You can withdraw it until HR accepts it.</>}
        confirmLabel="Yes, submit"
        tone="danger"
        loading={resign.isPending}
        onConfirm={() => resign.mutate()}
        onClose={() => setConfirming(false)}
      />
    </div>
  );
}

function ExitStatus({ s }: { s: Separation }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [withdrawing, setWithdrawing] = useState(false);
  const withdraw = useMutation({
    mutationFn: () => separationsApi.withdraw(s.summary.id),
    onSuccess: () => {
      toast.success('Resignation withdrawn');
      qc.invalidateQueries({ queryKey: qk.separations });
    },
    onError: (err) => toast.error(err),
  });
  const status = s.summary.status;
  const lastDay = s.summary.lastWorkingDay ?? s.summary.proposedLastDay;

  return (
    <div className="space-y-6">
      <Card>
        <CardBody className="space-y-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm text-zinc-500">{status === 'Requested' ? 'Proposed last working day' : 'Last working day'}</p>
              <p className="mt-1 text-2xl font-semibold tracking-tight text-zinc-900 dark:text-white">{formatDate(lastDay)}</p>
              <p className="mt-1 text-xs text-zinc-500">Notice given {formatDate(s.summary.noticeDate)}</p>
            </div>
            <div className="flex items-center gap-3">
              <StatusBadge status={status} label={status === 'Requested' ? 'Waiting for HR' : undefined} />
              {status === 'Requested' && (
                <Button variant="secondary" size="sm" onClick={() => setWithdrawing(true)}>
                  Withdraw
                </Button>
              )}
            </div>
          </div>
          <SeparationSteps s={s} />
          {s.decisionNote && status !== 'Requested' && (
            <p className="rounded-lg bg-zinc-50 p-3 text-sm text-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-300">HR: “{s.decisionNote}”</p>
          )}
        </CardBody>
      </Card>

      {status !== 'Requested' && (
        <div className="grid gap-6 lg:grid-cols-2">
          <Card>
            <CardHeader title="Clearance" description={`${s.clearance.filter((c) => c.isCleared).length} of ${s.clearance.length} done`} />
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {s.clearance.map((c) => (
                <li key={c.id} className="flex items-start gap-3 px-5 py-3">
                  {c.isCleared ? (
                    <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-500" aria-label="Done" />
                  ) : (
                    <Circle className="mt-0.5 size-4 shrink-0 text-zinc-300" aria-label="Pending" />
                  )}
                  <div className="min-w-0">
                    <p className="text-sm text-zinc-900 dark:text-zinc-100">{c.title}</p>
                    <p className="text-xs text-zinc-500">{c.department}</p>
                  </div>
                </li>
              ))}
            </ul>
          </Card>
          {status === 'Completed' ? <FinalSummary s={s} /> : <ExitInterview s={s} />}
        </div>
      )}

      <ConfirmDialog
        open={withdrawing}
        title="Withdraw your resignation?"
        message="You will continue in your role as before."
        confirmLabel="Withdraw"
        loading={withdraw.isPending}
        onConfirm={() => withdraw.mutate()}
        onClose={() => setWithdrawing(false)}
      />
    </div>
  );
}

function ExitInterview({ s }: { s: Separation }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [rating, setRating] = useState(s.exitRating ?? 0);
  const [reason, setReason] = useState(s.exitReason ?? '');
  const [recommend, setRecommend] = useState(s.wouldRecommend ?? true);
  const [feedback, setFeedback] = useState(s.exitFeedback ?? '');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () =>
      separationsApi.exitInterview(s.summary.id, { exitReason: reason || null, rating, wouldRecommend: recommend, feedback: feedback.trim() || null }),
    onSuccess: () => {
      toast.success('Thank you for your feedback');
      qc.invalidateQueries({ queryKey: qk.separations });
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!rating) return setError('Give a rating from 1 to 5.');
    save.mutate();
  };

  return (
    <Card>
      <CardHeader
        title="Exit interview"
        description={s.exitInterviewAtUtc ? `Submitted ${formatDateTimeUtc(s.exitInterviewAtUtc)}; you can update it.` : 'Honest feedback helps the team you leave behind.'}
      />
      <CardBody>
        <form onSubmit={submit} className="space-y-4">
          <FormError message={error} />
          <Field label="How was your time here?">
            <div>
              <Stars value={rating} onChange={setRating} />
            </div>
          </Field>
          <Field label="Main reason for leaving">
            <Select value={reason} onChange={(e) => setReason(e.target.value)}>
              <option value="">Prefer not to say</option>
              {EXIT_REASONS.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </Select>
          </Field>
          <Switch checked={recommend} onChange={setRecommend} label="I would recommend working here to a friend" />
          <Field label="Anything else?">
            <Textarea value={feedback} maxLength={4000} rows={4} onChange={(e) => setFeedback(e.target.value)} placeholder="What went well, what should change?" />
          </Field>
          <div className="flex justify-end">
            <Button type="submit" loading={save.isPending}>
              {s.exitInterviewAtUtc ? 'Update feedback' : 'Submit feedback'}
            </Button>
          </div>
        </form>
      </CardBody>
    </Card>
  );
}

function FinalSummary({ s }: { s: Separation }) {
  const st = s.settlement;
  return (
    <Card>
      <CardHeader title="Final settlement" description={`Closed ${formatDateTimeUtc(s.completedAtUtc)}`} />
      <CardBody>
        {st ? (
          <>
            <p className="text-3xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">{formatCurrency(st.net)}</p>
            <p className="mt-1 text-sm text-zinc-500">
              {st.net >= 0 ? 'Payable to you' : 'Recoverable from you'}, in addition to your last month's salary through payroll.
            </p>
          </>
        ) : (
          <p className="text-sm text-zinc-500">Contact HR for your settlement details.</p>
        )}
        <p className="mt-4 text-sm text-zinc-600 dark:text-zinc-300">
          Ask HR for your experience certificate; it will appear under My documents. Best of luck ahead!
        </p>
      </CardBody>
    </Card>
  );
}
