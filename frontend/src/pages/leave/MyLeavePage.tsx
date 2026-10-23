import { useState, type FormEvent } from 'react';
import { OnBehalfField } from '../../components/engine';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, CalendarPlus, Paperclip, Plus, X } from 'lucide-react';
import { ATTACHMENT_ACCEPT, attachmentProblem, attachmentsApi } from '../../api/attachments';
import { leaveApi } from '../../api/leave';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import { ClaimCompOffModal, MyCompOffClaims } from './CompOff';
import { errorMessage } from '../../api/client';
import { useLeaveTypes } from '../../api/hooks';
import { qk } from '../../api/keys';
import { leaveRulesApi } from '../../api/leaveRules';
import {
  Button,
  Card,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  Skeleton,
  SkeletonRows,
  Spinner,
  StatusBadge,
  Table,
  TBody,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDate, formatNumber, formatTime, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import type { HalfDayPart, LeaveBalance, LeaveDuration, LeaveRequest } from '../../types';

export function MyLeavePage() {
  const toast = useToast();
  const qc = useQueryClient();
  const [applyOpen, setApplyOpen] = useState(false);
  const [cancelling, setCancelling] = useState<LeaveRequest | null>(null);
  const [filesFor, setFilesFor] = useState<LeaveRequest | null>(null);
  const [claiming, setClaiming] = useState(false);
  const balances = useQuery({ queryKey: qk.leaveBalances, queryFn: () => leaveApi.balances() });
  const requests = useQuery({ queryKey: qk.leaveMy, queryFn: leaveApi.my });

  const cancel = useMutation({
    mutationFn: (id: string) => leaveApi.cancel(id),
    onSuccess: () => {
      toast.success('Leave request cancelled');
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.approvals });
      setCancelling(null);
    },
    onError: (err) => toast.error(err),
  });

  return (
    <>
      <PageHeader
        title="My leave"
        description={`Balances for ${new Date().getFullYear()}.`}
        actions={
          <>
            <Button variant="secondary" icon={<CalendarPlus className="size-4" />} onClick={() => setClaiming(true)}>
              Claim comp-off
            </Button>
            <Button icon={<Plus className="size-4" />} onClick={() => setApplyOpen(true)}>
              Apply for leave
            </Button>
          </>
        }
      />

      {balances.isError ? (
        <Card>
          <ErrorState error={balances.error} onRetry={() => balances.refetch()} />
        </Card>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {balances.isLoading
            ? Array.from({ length: 4 }).map((_, i) => (
                <Card key={i} className="p-4">
                  <Skeleton className="h-4 w-24" />
                  <Skeleton className="mt-3 h-8 w-16" />
                  <Skeleton className="mt-4 h-2 w-full" />
                </Card>
              ))
            : (balances.data ?? []).map((b) => <BalanceCard key={b.leaveTypeId} b={b} />)}
          {balances.data?.length === 0 && (
            <Card className="sm:col-span-2 xl:col-span-4">
              <EmptyState icon={CalendarDays} title="No leave types set up" description="Ask HR to configure leave types." />
            </Card>
          )}
        </div>
      )}

      <Card className="mt-6">
        <CardHeader title="My requests" />
        {requests.isLoading ? (
          <SkeletonRows rows={4} cols={5} />
        ) : requests.isError ? (
          <ErrorState error={requests.error} onRetry={() => requests.refetch()} />
        ) : !requests.data?.length ? (
          <EmptyState
            icon={CalendarDays}
            title="No leave requests yet"
            description="When you apply for leave it shows up here with its status."
            action={
              <Button variant="secondary" icon={<Plus className="size-4" />} onClick={() => setApplyOpen(true)}>
                Apply for leave
              </Button>
            }
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Type</Th>
                <Th>Dates</Th>
                <Th className="text-right">Days</Th>
                <Th>Status</Th>
                <Th>Reason</Th>
                <Th>Decision note</Th>
                <Th>Files</Th>
                <Th className="text-right">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </THead>
            <TBody>
              {requests.data.map((r) => (
                <Tr key={r.id}>
                  <Td className="font-medium text-zinc-900 dark:text-zinc-100">{r.leaveType}</Td>
                  <Td>
                    {formatDate(r.fromDate)}
                    {r.toDate !== r.fromDate && <> – {formatDate(r.toDate)}</>}
                    {r.duration !== 'FullDay' && <span className="block text-xs text-zinc-500">{durationLabel(r)}</span>}
                  </Td>
                  <Td className="text-right tabular-nums">{formatNumber(r.days)}</Td>
                  <Td>
                    <StatusBadge status={r.status} />
                  </Td>
                  <Td className="max-w-xs truncate" title={r.reason ?? undefined}>{r.reason ?? '—'}</Td>
                  <Td className="max-w-xs truncate" title={r.decisionNote ?? undefined}>{r.decisionNote ?? '—'}</Td>
                  <Td>
                    {(r.attachmentCount > 0 || r.status === 'Pending') && (
                      <Button variant="ghost" size="sm" icon={<Paperclip className="size-3.5" />} onClick={() => setFilesFor(r)}>
                        {r.attachmentCount > 0 ? r.attachmentCount : 'Add'}
                      </Button>
                    )}
                  </Td>
                  <Td className="text-right">
                    {r.status === 'Pending' && (
                      <Button variant="ghost" size="sm" onClick={() => setCancelling(r)}>
                        Cancel
                      </Button>
                    )}
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      <MyCompOffClaims />
      {applyOpen && <ApplyLeaveModal balances={balances.data ?? []} onClose={() => setApplyOpen(false)} />}
      {claiming && <ClaimCompOffModal onClose={() => setClaiming(false)} />}
      {filesFor && (
        <Modal open onClose={() => setFilesFor(null)} title="Files" description={`${filesFor.leaveType} · ${formatDate(filesFor.fromDate)}`}>
          <AttachmentList owner="Leave" ownerId={filesFor.id} emptyText="No files attached to this request." />
        </Modal>
      )}

      <ConfirmDialog
        open={!!cancelling}
        title="Cancel leave request?"
        message={
          cancelling && (
            <>
              Your {cancelling.leaveType.toLowerCase()} request for {formatDate(cancelling.fromDate)}
              {cancelling.toDate !== cancelling.fromDate && <> – {formatDate(cancelling.toDate)}</>} will be withdrawn.
            </>
          )
        }
        confirmLabel="Cancel request"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling.id)}
        onClose={() => setCancelling(null)}
      />
    </>
  );
}

function BalanceCard({ b }: { b: LeaveBalance }) {
  const total = b.isCompensatory ? b.credited + b.adjustment : b.entitled + b.carriedForward + b.adjustment + b.credited;
  const usedPct = total > 0 ? Math.min(100, (b.used / total) * 100) : 0;
  const pendingPct = total > 0 ? Math.min(100 - usedPct, (b.pending / total) * 100) : 0;
  const extras = [
    b.accrual === 'Monthly' && !b.isCompensatory && b.earned < b.entitled ? `${formatNumber(b.earned)} of ${formatNumber(b.entitled)} earned so far` : null,
    b.carriedForward > 0 ? `${formatNumber(b.carriedForward)} carried forward` : null,
    b.credited > 0 ? `${formatNumber(b.credited)} from comp-off` : null,
    b.adjustment !== 0 ? `${b.adjustment > 0 ? '+' : ''}${formatNumber(b.adjustment)} adjusted by HR` : null,
  ].filter(Boolean);
  return (
    <Card className="p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="truncate text-sm font-medium text-zinc-700 dark:text-zinc-300">{b.leaveType}</p>
        <span className="rounded bg-zinc-100 px-1.5 py-0.5 font-mono text-[10px] text-zinc-500 dark:bg-zinc-800">{b.code}</span>
      </div>
      <p className="mt-2 text-3xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">
        {formatNumber(b.available)}
        <span className="ml-1 text-sm font-normal text-zinc-500">/ {formatNumber(total)} days</span>
      </p>
      <div className="mt-3 flex h-1.5 gap-0.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" aria-hidden="true">
        <div className="h-full rounded-full bg-accent-500" style={{ width: `${usedPct}%` }} />
        <div className="h-full rounded-full bg-amber-400" style={{ width: `${pendingPct}%` }} />
      </div>
      <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">
        {formatNumber(b.used)} used · {formatNumber(b.pending)} pending
      </p>
      {extras.length > 0 && <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400" title={b.adjustmentNote ?? undefined}>{extras.join(' · ')}</p>}
    </Card>
  );
}

export function durationLabel(r: { duration: LeaveDuration; halfDayPart: HalfDayPart | null; startTime: string | null; endTime: string | null }) {
  if (r.duration === 'HalfDay') return r.halfDayPart === 'SecondHalf' ? 'Half day · second half' : 'Half day · first half';
  if (r.duration === 'ShortLeave') return `Short leave · ${formatTime(r.startTime)} – ${formatTime(r.endTime)}`;
  return 'Full day';
}

function ApplyLeaveModal({ balances, onClose }: { balances: LeaveBalance[]; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const leaveTypes = useLeaveTypes();
  const active = (leaveTypes.data ?? []).filter((t) => t.isActive);
  const [leaveTypeId, setLeaveTypeId] = useState('');
  const [from, setFrom] = useState(todayIso());
  const [to, setTo] = useState(todayIso());
  const [reason, setReason] = useState('');
  const [reasonId, setReasonId] = useState('');
  const allReasons = useQuery({ queryKey: qk.leaveReasons, queryFn: leaveRulesApi.reasons });
  const [files, setFiles] = useState<File[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [duration, setDuration] = useState<LeaveDuration>('FullDay');
  const [part, setPart] = useState<HalfDayPart>('FirstHalf');
  const [startTime, setStartTime] = useState('10:00');
  const [endTime, setEndTime] = useState('12:00');
  const [forWhom, setForWhom] = useState('');

  const typeId = leaveTypeId || active[0]?.id || '';
  const type = active.find((t) => t.id === typeId);
  const durations: Array<{ value: LeaveDuration; label: string }> = [
    { value: 'FullDay', label: 'Full day(s)' },
    ...(type?.allowHalfDay ? [{ value: 'HalfDay' as const, label: 'Half day' }] : []),
    ...(type?.allowShortLeave ? [{ value: 'ShortLeave' as const, label: 'Short leave' }] : []),
  ];
  const mode: LeaveDuration = durations.some((d) => d.value === duration) ? duration : 'FullDay';
  const single = mode !== 'FullDay';
  const range = useDebounced({ from, to }, 250);
  const validRange = !!range.from && !!range.to && range.to >= range.from;
  const days = useQuery({
    queryKey: ['leave', 'count', range.from, range.to],
    queryFn: () => leaveApi.countDays(range.from, range.to),
    enabled: validRange,
    retry: false,
  });
  const reasons = (allReasons.data ?? []).filter((r) => r.leaveTypeId === typeId && r.isActive);
  const chosenReason = reasons.find((r) => r.id === reasonId);
  const balance = forWhom ? undefined : balances.find((b) => b.leaveTypeId === typeId);
  const needsDocument = !!chosenReason?.requiresDocument || (!!type?.documentAfterDays && !!days.data && days.data.days > type.documentAfterDays);
  const over = balance && days.data && days.data.days > balance.available;

  const apply = useMutation({
    mutationFn: async () => {
      const r = await leaveApi.apply({
        leaveTypeId: typeId,
        fromDate: from,
        toDate: single ? from : to,
        reason: reason.trim() || null,
        reasonId: reasons.length ? reasonId || null : null,
        duration: mode,
        halfDayPart: mode === 'HalfDay' ? part : null,
        startTime: mode === 'ShortLeave' ? `${startTime}:00` : null,
        endTime: mode === 'ShortLeave' ? `${endTime}:00` : null,
        employeeId: forWhom || null,
      });
      // The request exists now; a failed upload must not hide that, so it is reported separately.
      const failed: string[] = [];
      for (const f of files) await attachmentsApi.upload('Leave', r.id, f).catch(() => failed.push(f.name));
      return { r, failed };
    },
    onSuccess: ({ r, failed }) => {
      toast.success('Leave requested', `${r.duration === 'FullDay' ? `${formatNumber(r.days)} day(s)` : durationLabel(r)} of ${r.leaveType} sent for approval.`);
      if (failed.length) toast.error('Some files did not upload', `${failed.join(', ')}. Add them from the Files button.`);
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.approvals });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!typeId) return setError('Choose a leave type.');
    if (!from || !to) return setError('Choose the dates.');
    if (!single && to < from) return setError('The end date cannot be before the start date.');
    if (mode === 'ShortLeave' && endTime <= startTime) return setError('Short leave must end after it starts.');
    apply.mutate();
  };

  return (
    <Modal
      open
      onClose={onClose}
      title="Apply for leave"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="apply-leave-form" loading={apply.isPending}>
            Submit request
          </Button>
        </>
      }
    >
      <form id="apply-leave-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <OnBehalfField value={forWhom} onChange={setForWhom} />
        <Field label="Leave type" required hint={balance ? `${formatNumber(balance.available)} day(s) available` : undefined}>
          <Select value={typeId} onChange={(e) => setLeaveTypeId(e.target.value)}>
            {active.length === 0 && <option value="">No leave types available</option>}
            {active.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
                {t.isPaid ? '' : ' (unpaid)'}
              </option>
            ))}
          </Select>
        </Field>
        {durations.length > 1 && (
          <div className="inline-flex rounded-lg bg-zinc-100 p-1 dark:bg-zinc-800" role="radiogroup" aria-label="Length">
            {durations.map((d) => (
              <button
                key={d.value}
                type="button"
                role="radio"
                aria-checked={mode === d.value}
                onClick={() => setDuration(d.value)}
                className={
                  mode === d.value
                    ? 'rounded-md bg-white px-3 py-1.5 text-sm font-medium text-zinc-900 shadow-sm dark:bg-zinc-900 dark:text-white'
                    : 'rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:text-zinc-900 dark:text-zinc-300'
                }
              >
                {d.label}
              </button>
            ))}
          </div>
        )}
        {single ? (
          <div className="grid grid-cols-2 gap-3">
            <Field label="Date" required>
              <Input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setTo(e.target.value); }} />
            </Field>
            {mode === 'HalfDay' ? (
              <Field label="Which half">
                <Select value={part} onChange={(e) => setPart(e.target.value as HalfDayPart)}>
                  <option value="FirstHalf">First half (morning)</option>
                  <option value="SecondHalf">Second half (afternoon)</option>
                </Select>
              </Field>
            ) : (
              <div className="grid grid-cols-2 gap-2">
                <Field label="From">
                  <Input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} />
                </Field>
                <Field label="To" hint="Up to 4 hours">
                  <Input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} />
                </Field>
              </div>
            )}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3">
              <Field label="From" required>
                <Input
                  type="date"
                  value={from}
                  onChange={(e) => {
                    setFrom(e.target.value);
                    if (to < e.target.value) setTo(e.target.value);
                  }}
                />
              </Field>
              <Field label="To" required>
                <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
              </Field>
            </div>
            <div
              className="flex items-center justify-between rounded-lg bg-zinc-50 px-3 py-2.5 text-sm dark:bg-zinc-800/50"
              aria-live="polite"
            >
              <span className="text-zinc-600 dark:text-zinc-300">Working days</span>
              <span className="font-semibold text-zinc-900 tabular-nums dark:text-white">
                {!validRange ? '—' : days.isFetching ? <Spinner className="size-4" /> : days.isError ? '—' : formatNumber(days.data?.days)}
              </span>
            </div>
          </>
        )}
        {type && (type.minNoticeDays > 0 || type.maxConsecutiveDays) && (
          <p className="text-xs text-zinc-500">
            {[type.minNoticeDays > 0 ? `Apply at least ${type.minNoticeDays} day(s) ahead` : null, type.maxConsecutiveDays ? `at most ${type.maxConsecutiveDays} day(s) at a time` : null]
              .filter(Boolean)
              .join(' · ')}
            .
          </p>
        )}
        {days.isError && <p className="text-xs text-red-600 dark:text-red-400">{errorMessage(days.error)}</p>}
        {over && !single && (
          <p className="text-xs text-amber-700 dark:text-amber-300">This is more than your available balance.</p>
        )}
        {reasons.length > 0 && (
          <Field label="Reason type" required hint={chosenReason?.maxDays ? `Up to ${chosenReason.maxDays} days` : undefined}>
            <Select value={reasonId} onChange={(e) => setReasonId(e.target.value)}>
              <option value="">Choose…</option>
              {reasons.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
        {type?.sandwich && !single && <p className="text-xs text-zinc-500">Weekends and holidays between your leave days also count as leave.</p>}
        <Field label="Reason">
          <Textarea value={reason} maxLength={1000} onChange={(e) => setReason(e.target.value)} placeholder="Optional" />
        </Field>
        {needsDocument && (
          <p className="rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
            Attach a supporting document; your manager cannot approve this leave without it.
          </p>
        )}
        <div className="space-y-2">
          <p className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
            Supporting documents <span className="font-normal text-zinc-500">(e.g. medical certificate)</span>
          </p>
          {files.map((f) => (
            <div key={f.name} className="flex items-center gap-2 rounded-lg bg-zinc-50 px-3 py-2 text-sm dark:bg-zinc-800/50">
              <Paperclip className="size-4 text-zinc-400" aria-hidden="true" />
              <span className="min-w-0 flex-1 truncate">{f.name}</span>
              <IconButton label={`Remove ${f.name}`} onClick={() => setFiles((l) => l.filter((x) => x !== f))}>
                <X className="size-4" />
              </IconButton>
            </div>
          ))}
          <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-dashed border-zinc-300 px-3 py-2 text-sm text-zinc-600 hover:border-accent-400 hover:text-accent-700 dark:border-zinc-700 dark:text-zinc-300">
            <Paperclip className="size-4" aria-hidden="true" />
            Attach file
            <input
              type="file"
              multiple
              accept={ATTACHMENT_ACCEPT}
              className="sr-only"
              onChange={(e) => {
                const chosen = Array.from(e.target.files ?? []);
                const problem = chosen.map(attachmentProblem).find(Boolean);
                if (problem) setError(problem);
                else setFiles((l) => [...l, ...chosen.filter((c) => !l.some((x) => x.name === c.name))]);
                e.target.value = '';
              }}
            />
          </label>
        </div>
      </form>
    </Modal>
  );
}
