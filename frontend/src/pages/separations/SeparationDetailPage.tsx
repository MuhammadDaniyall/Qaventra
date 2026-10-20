import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { Check, CheckCircle2, Circle, FileText, Plus, ThumbsDown, ThumbsUp, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { separationsApi } from '../../api/separations';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import {
  Avatar,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  PageHeader,
  PageSpinner,
  StatusBadge,
  Switch,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatDateTimeUtc, formatNumber, todayIso } from '../../lib/format';
import type { Separation, Settlement } from '../../types/workflows';
import { SeparationSteps, Stars, typeLabel } from './shared';

const back = { to: '/separations', label: 'Separations' };

export function SeparationDetailPage() {
  const { id = '' } = useParams();
  const sep = useQuery({ queryKey: qk.separation(id), queryFn: () => separationsApi.get(id) });

  if (sep.isLoading) return <PageSpinner />;
  if (sep.isError || !sep.data) {
    return (
      <>
        <PageHeader title="Separation" back={back} />
        <Card>
          <ErrorState error={sep.error} onRetry={() => sep.refetch()} />
        </Card>
      </>
    );
  }
  const s = sep.data;
  const status = s.summary.status;

  return (
    <>
      <PageHeader
        back={back}
        title={
          <span className="flex items-center gap-3">
            <Avatar name={s.summary.employeeName} />
            {s.summary.employeeName}
          </span>
        }
        description={`${typeLabel(s.summary.type)} · ${[s.summary.designation, s.summary.department].filter(Boolean).join(', ') || s.summary.employeeCode} · joined ${formatDate(s.joiningDate)}`}
        actions={
          <>
            <StatusBadge status={status} />
            {(status === 'Requested' || status === 'InProgress') && <CancelSeparationButton s={s} />}
          </>
        }
      />

      <Card className="mb-6">
        <CardBody className="space-y-5">
          <SeparationSteps s={s} />
          <dl className="grid gap-4 border-t border-zinc-100 pt-5 text-sm sm:grid-cols-4 dark:border-zinc-800">
            <Fact label="Notice given" value={formatDate(s.summary.noticeDate)} />
            <Fact label={s.summary.lastWorkingDay ? 'Last working day' : 'Proposed last day'} value={formatDate(s.summary.lastWorkingDay ?? s.summary.proposedLastDay)} />
            <Fact label="Notice period" value={`${s.noticePeriodDays} days`} />
            <Fact label="Reason" value={s.reason || '—'} />
          </dl>
        </CardBody>
      </Card>

      {status === 'Requested' && (
        <div className="space-y-6">
          <DecisionCard s={s} />
          <Card>
            <CardHeader title="Documents" />
            <CardBody>
              <AttachmentList owner="Separation" ownerId={s.summary.id} emptyText="No documents attached." />
            </CardBody>
          </Card>
        </div>
      )}

      {(status === 'InProgress' || status === 'Completed') && (
        <div className="grid gap-6 xl:grid-cols-5">
          <div className="space-y-6 xl:col-span-2">
            <ClearanceCard s={s} />
            <ExitInterviewCard s={s} />
            <Card>
              <CardHeader title="Documents" description="Resignation letter, clearance forms, settlement receipts" />
              <CardBody>
                <AttachmentList owner="Separation" ownerId={s.summary.id} emptyText="No documents attached." />
              </CardBody>
            </Card>
          </div>
          <div className="xl:col-span-3">{s.settlement && <SettlementCard s={s} st={s.settlement} />}</div>
        </div>
      )}
    </>
  );
}

function CancelSeparationButton({ s }: { s: Separation }) {
  const toast = useToast();
  const refresh = useRefresh(s.summary.id);
  const [open, setOpen] = useState(false);
  const cancel = useMutation({
    mutationFn: () => separationsApi.withdraw(s.summary.id),
    onSuccess: () => {
      toast.success('Separation cancelled', `${s.summary.employeeName} stays employed.`);
      setOpen(false);
      refresh();
    },
    onError: (err) => toast.error(err),
  });
  return (
    <>
      <Button variant="secondary" size="sm" onClick={() => setOpen(true)}>
        Cancel separation
      </Button>
      <ConfirmDialog
        open={open}
        title="Cancel this separation?"
        message={<>{s.summary.employeeName} stays employed and the checklist and settlement are discarded.</>}
        confirmLabel="Cancel separation"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
        onClose={() => setOpen(false)}
      />
    </>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-zinc-500">{label}</dt>
      <dd className="mt-0.5 truncate font-medium text-zinc-900 dark:text-zinc-100" title={value}>
        {value}
      </dd>
    </div>
  );
}

function useRefresh(id: string) {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: qk.separations });
    qc.invalidateQueries({ queryKey: qk.separation(id) });
  };
}

function DecisionCard({ s }: { s: Separation }) {
  const toast = useToast();
  const refresh = useRefresh(s.summary.id);
  const [lastDay, setLastDay] = useState(s.summary.proposedLastDay);
  const [notice, setNotice] = useState(String(s.noticePeriodDays));
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const decide = useMutation({
    mutationFn: (accept: boolean) =>
      accept
        ? separationsApi.accept(s.summary.id, lastDay, Number(notice) || 0, note.trim() || null)
        : separationsApi.decline(s.summary.id, note.trim() || null),
    onSuccess: (_, accept) => {
      toast.success(accept ? 'Resignation accepted' : 'Resignation declined', s.summary.employeeName);
      refresh();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const served = Math.max(0, Math.round((Date.parse(lastDay) - Date.parse(s.summary.noticeDate)) / 86_400_000) + 1);

  return (
    <Card>
      <CardHeader title="Decide on this resignation" description="Accepting starts the clearance checklist and the exit interview." />
      <CardBody>
        <form
          className="space-y-4"
          onSubmit={(e: FormEvent) => {
            e.preventDefault();
            setError(null);
            decide.mutate(true);
          }}
        >
          <FormError message={error} />
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Agreed last working day" required hint={`${served} day(s) of notice served`}>
              <Input type="date" value={lastDay} min={s.summary.noticeDate} onChange={(e) => setLastDay(e.target.value)} />
            </Field>
            <Field label="Notice period (days)">
              <Input type="number" min="0" max="365" value={notice} onChange={(e) => setNotice(e.target.value)} />
            </Field>
            <Field label="Note to employee">
              <Input value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} placeholder="Optional" />
            </Field>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" loading={decide.isPending && decide.variables === false} onClick={() => decide.mutate(false)}>
              Decline
            </Button>
            <Button type="submit" variant="success" icon={<Check className="size-4" />} loading={decide.isPending && decide.variables === true}>
              Accept resignation
            </Button>
          </div>
        </form>
      </CardBody>
    </Card>
  );
}

function ClearanceCard({ s }: { s: Separation }) {
  const toast = useToast();
  const refresh = useRefresh(s.summary.id);
  const editable = s.summary.status === 'InProgress';
  const [department, setDepartment] = useState('');
  const [title, setTitle] = useState('');
  const toggle = useMutation({
    mutationFn: ({ itemId, cleared }: { itemId: string; cleared: boolean }) => separationsApi.updateClearance(s.summary.id, itemId, cleared, null),
    onSuccess: refresh,
    onError: (err) => toast.error(err),
  });
  const add = useMutation({
    mutationFn: () => separationsApi.addClearance(s.summary.id, department.trim(), title.trim()),
    onSuccess: () => {
      setDepartment('');
      setTitle('');
      refresh();
    },
    onError: (err) => toast.error(err),
  });
  const remove = useMutation({
    mutationFn: (itemId: string) => separationsApi.deleteClearance(s.summary.id, itemId),
    onSuccess: refresh,
    onError: (err) => toast.error(err),
  });
  const done = s.clearance.filter((c) => c.isCleared).length;

  return (
    <Card>
      <CardHeader title="Clearance checklist" description={`${done} of ${s.clearance.length} cleared`} />
      <div className="mx-5 mt-4 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" aria-hidden="true">
        <div className="h-full rounded-full bg-emerald-500 transition-all" style={{ width: `${s.clearance.length ? (done / s.clearance.length) * 100 : 0}%` }} />
      </div>
      <ul className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
        {s.clearance.map((c) => (
          <li key={c.id} className="group flex items-start gap-3 px-5 py-3">
            <button
              type="button"
              disabled={!editable || toggle.isPending}
              onClick={() => toggle.mutate({ itemId: c.id, cleared: !c.isCleared })}
              aria-pressed={c.isCleared}
              aria-label={`${c.isCleared ? 'Mark not cleared' : 'Mark cleared'}: ${c.title}`}
              className="mt-0.5 rounded-full focus-visible:outline-2 focus-visible:outline-accent-500 disabled:cursor-default"
            >
              {c.isCleared ? <CheckCircle2 className="size-5 text-emerald-500" /> : <Circle className="size-5 text-zinc-300 hover:text-emerald-400" />}
            </button>
            <div className="min-w-0 flex-1">
              <p className={cn('text-sm', c.isCleared ? 'text-zinc-500 line-through decoration-zinc-300' : 'text-zinc-900 dark:text-zinc-100')}>{c.title}</p>
              <p className="text-xs text-zinc-500">
                {c.department}
                {c.isCleared && c.clearedByName && <> · {c.clearedByName}, {formatDateTimeUtc(c.clearedAtUtc)}</>}
              </p>
            </div>
            {editable && (
              <IconButton label={`Remove ${c.title}`} onClick={() => remove.mutate(c.id)} className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100">
                <Trash2 className="size-4" />
              </IconButton>
            )}
          </li>
        ))}
      </ul>
      {editable && (
        <form
          className="flex flex-wrap gap-2 border-t border-zinc-100 p-4 dark:border-zinc-800"
          onSubmit={(e) => {
            e.preventDefault();
            if (department.trim() && title.trim()) add.mutate();
          }}
        >
          <Input value={department} onChange={(e) => setDepartment(e.target.value)} placeholder="Department" aria-label="Department" className="w-32" maxLength={100} />
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Add a checklist item" aria-label="Checklist item" className="min-w-40 flex-1" maxLength={200} />
          <Button type="submit" variant="secondary" icon={<Plus className="size-4" />} loading={add.isPending} disabled={!department.trim() || !title.trim()}>
            Add
          </Button>
        </form>
      )}
    </Card>
  );
}

function ExitInterviewCard({ s }: { s: Separation }) {
  return (
    <Card>
      <CardHeader title="Exit interview" description={s.exitInterviewAtUtc ? `Submitted ${formatDateTimeUtc(s.exitInterviewAtUtc)}` : 'Filled in by the employee from their Resignation page'} />
      <CardBody>
        {s.exitInterviewAtUtc ? (
          <dl className="space-y-3 text-sm">
            <div className="flex items-center justify-between">
              <dt className="text-zinc-500">Rating</dt>
              <dd>
                <Stars value={s.exitRating ?? 0} size="sm" />
              </dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-zinc-500">Main reason</dt>
              <dd className="font-medium text-zinc-900 dark:text-zinc-100">{s.exitReason ?? 'Not given'}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-zinc-500">Would recommend us</dt>
              <dd className="inline-flex items-center gap-1 font-medium">
                {s.wouldRecommend ? (
                  <>
                    <ThumbsUp className="size-4 text-emerald-500" aria-hidden="true" /> Yes
                  </>
                ) : (
                  <>
                    <ThumbsDown className="size-4 text-red-500" aria-hidden="true" /> No
                  </>
                )}
              </dd>
            </div>
            {s.exitFeedback && <p className="rounded-lg bg-zinc-50 p-3 text-zinc-700 dark:bg-zinc-800/50 dark:text-zinc-300">“{s.exitFeedback}”</p>}
          </dl>
        ) : (
          <p className="text-sm text-zinc-500">
            {s.summary.type === 'Resignation'
              ? 'Waiting for the employee. Ask them to open Resignation in their menu.'
              : 'Not usually collected for this type of separation.'}
          </p>
        )}
      </CardBody>
    </Card>
  );
}

function SettlementCard({ s, st }: { s: Separation; st: Settlement }) {
  const toast = useToast();
  const refresh = useRefresh(s.summary.id);
  const completed = s.summary.status === 'Completed';
  const [encash, setEncash] = useState(String(st.encashedLeaveDays));
  const [recover, setRecover] = useState(st.recoverNoticeShortfall);
  const [additions, setAdditions] = useState(String(st.otherAdditions));
  const [deductions, setDeductions] = useState(String(st.otherDeductions));
  const [note, setNote] = useState(st.note ?? '');
  const [completing, setCompleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Same rules as the server's SettlementCalculator, so the numbers move as HR types.
  const round = (n: number) => Math.round(n);
  const encashAmount = round(st.dailyRate * (Number(encash) || 0));
  const recovery = recover ? round(st.dailyRate * st.noticeShortfallDays) : 0;
  const net = completed ? st.net : encashAmount + st.expenseReimbursement + (Number(additions) || 0) - recovery - (Number(deductions) || 0);
  const dirty =
    Number(encash) !== st.encashedLeaveDays ||
    recover !== st.recoverNoticeShortfall ||
    Number(additions) !== st.otherAdditions ||
    Number(deductions) !== st.otherDeductions ||
    (note || null) !== (st.note || null);

  const save = useMutation({
    mutationFn: () =>
      separationsApi.saveSettlement(s.summary.id, {
        encashedLeaveDays: Number(encash) || 0,
        recoverNoticeShortfall: recover,
        otherAdditions: Number(additions) || 0,
        otherDeductions: Number(deductions) || 0,
        note: note.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Settlement saved');
      refresh();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const complete = useMutation({
    mutationFn: () => separationsApi.complete(s.summary.id),
    onSuccess: () => {
      toast.success('Separation completed', `${s.summary.employeeName} is marked as left and their login is switched off.`);
      setCompleting(false);
      refresh();
    },
    onError: (err) => {
      setCompleting(false);
      setError(errorMessage(err));
    },
  });

  const lastDay = s.summary.lastWorkingDay ?? s.summary.proposedLastDay;
  const checks = [
    { ok: s.clearance.every((c) => c.isCleared), label: 'All clearance items cleared' },
    { ok: !!st.savedAtUtc && !dirty, label: 'Settlement reviewed and saved' },
    { ok: todayIso() >= lastDay, label: `Last working day reached (${formatDate(lastDay)})` },
  ];
  const leaveLeft = st.leaveBalances.reduce((t, b) => t + b.remaining, 0);

  const Line = ({ label, amount, sub, negative }: { label: string; amount: number; sub?: string; negative?: boolean }) => (
    <div className="flex items-baseline justify-between gap-4 py-2">
      <div>
        <p className="text-sm text-zinc-700 dark:text-zinc-300">{label}</p>
        {sub && <p className="text-xs text-zinc-500">{sub}</p>}
      </div>
      <p className={cn('text-sm font-medium tabular-nums', negative && amount > 0 ? 'text-red-600 dark:text-red-400' : 'text-zinc-900 dark:text-zinc-100')}>
        {negative && amount > 0 ? '− ' : ''}
        {formatCurrency(amount)}
      </p>
    </div>
  );

  return (
    <Card>
      <CardHeader
        title="Final settlement"
        description={`On top of the last month's salary, which payroll pays up to ${formatDate(lastDay)}. Daily rate = gross ÷ 30.`}
      />
      <CardBody className="space-y-6">
        <FormError message={error} />
        {!st.hasSalary && (
          <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
            No salary is set for this employee, so leave encashment and notice recovery are zero.
          </p>
        )}

        {!completed && (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="Leave days to encash"
              hint={st.leaveBalances.length ? `Unused: ${st.leaveBalances.map((b) => `${b.leaveType} ${formatNumber(b.remaining)}`).join(', ')}` : 'No paid leave balance'}
            >
              <div className="flex gap-2">
                <Input type="number" min="0" max="365" step="0.5" value={encash} onChange={(e) => setEncash(e.target.value)} />
                {leaveLeft > 0 && (
                  <Button variant="secondary" onClick={() => setEncash(String(leaveLeft))}>
                    All {formatNumber(leaveLeft)}
                  </Button>
                )}
              </div>
            </Field>
            <div className="space-y-1.5 pt-1">
              <Switch
                checked={recover}
                onChange={setRecover}
                label="Recover notice shortfall"
                description={
                  st.noticeShortfallDays
                    ? `${st.noticeShortfallDays} day(s) short of the ${s.noticePeriodDays}-day notice`
                    : `Full notice served (${st.noticeServedDays} days)`
                }
                disabled={!st.noticeShortfallDays}
              />
            </div>
            <Field label="Other additions (Rs)" hint="Gratuity, bonus, arrears">
              <Input type="number" min="0" value={additions} onChange={(e) => setAdditions(e.target.value)} />
            </Field>
            <Field label="Other deductions (Rs)" hint="Loan balance, unreturned asset">
              <Input type="number" min="0" value={deductions} onChange={(e) => setDeductions(e.target.value)} />
            </Field>
            <Field label="Note" className="sm:col-span-2">
              <Textarea value={note} maxLength={1000} rows={2} onChange={(e) => setNote(e.target.value)} />
            </Field>
          </div>
        )}

        <div className="rounded-xl border border-zinc-200 px-4 dark:border-zinc-800">
          <div className="divide-y divide-zinc-100 dark:divide-zinc-800">
            <Line label="Leave encashment" amount={completed ? st.leaveEncashment : encashAmount} sub={`${completed ? st.encashedLeaveDays : Number(encash) || 0} day(s) × ${formatCurrency(st.dailyRate)}`} />
            <Line
              label="Approved expense claims"
              amount={st.expenseReimbursement}
              sub={st.claims.length ? st.claims.map((c) => c.title).join(', ') : completed ? undefined : 'None outstanding'}
            />
            <Line label="Other additions" amount={completed ? st.otherAdditions : Number(additions) || 0} />
            <Line label="Notice shortfall recovery" amount={completed ? st.noticeRecovery : recovery} negative />
            <Line label="Other deductions" amount={completed ? st.otherDeductions : Number(deductions) || 0} negative />
          </div>
          <div className="flex items-baseline justify-between border-t-2 border-zinc-200 py-3 dark:border-zinc-700">
            <p className="font-semibold text-zinc-900 dark:text-white">{net >= 0 ? 'Net payable' : 'Net recoverable'}</p>
            <p className="text-2xl font-semibold tracking-tight tabular-nums text-zinc-900 dark:text-white">{formatCurrency(Math.abs(net))}</p>
          </div>
        </div>

        {completed ? (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-200">
            <span>Completed {formatDateTimeUtc(s.completedAtUtc)}. Employee marked as left; login switched off.</span>
            <Link to="/letters" className="inline-flex items-center gap-1.5 font-medium underline-offset-2 hover:underline">
              <FileText className="size-4" aria-hidden="true" /> Issue experience letter
            </Link>
          </div>
        ) : (
          <div className="flex flex-wrap items-end justify-between gap-4 border-t border-zinc-100 pt-5 dark:border-zinc-800">
            <ul className="space-y-1 text-sm">
              {checks.map((c) => (
                <li key={c.label} className={cn('flex items-center gap-2', c.ok ? 'text-emerald-700 dark:text-emerald-300' : 'text-zinc-500')}>
                  {c.ok ? <CheckCircle2 className="size-4" aria-hidden="true" /> : <Circle className="size-4" aria-hidden="true" />}
                  {c.label}
                </li>
              ))}
            </ul>
            <div className="flex gap-2">
              <Button variant="secondary" loading={save.isPending} disabled={!dirty && !!st.savedAtUtc} onClick={() => save.mutate()}>
                Save settlement
              </Button>
              <Button disabled={!checks.every((c) => c.ok)} onClick={() => setCompleting(true)}>
                Complete separation
              </Button>
            </div>
          </div>
        )}
      </CardBody>

      <ConfirmDialog
        open={completing}
        title="Complete this separation?"
        message={
          <>
            {s.summary.employeeName} will be marked as left on {formatDate(lastDay)}, their login switched off, and{' '}
            {formatCurrency(Math.abs(net))} recorded as the final settlement. This cannot be undone.
          </>
        }
        confirmLabel="Complete"
        loading={complete.isPending}
        onConfirm={() => complete.mutate()}
        onClose={() => setCompleting(false)}
      />
    </Card>
  );
}
