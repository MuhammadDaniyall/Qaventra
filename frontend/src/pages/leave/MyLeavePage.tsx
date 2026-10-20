import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarDays, Plus } from 'lucide-react';
import { leaveApi } from '../../api/leave';
import { errorMessage } from '../../api/client';
import { useLeaveTypes } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  Button,
  Card,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  FormError,
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
import { formatDate, formatNumber, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import type { LeaveBalance, LeaveRequest } from '../../types';

export function MyLeavePage() {
  const toast = useToast();
  const qc = useQueryClient();
  const [applyOpen, setApplyOpen] = useState(false);
  const [cancelling, setCancelling] = useState<LeaveRequest | null>(null);
  const balances = useQuery({ queryKey: qk.leaveBalances, queryFn: () => leaveApi.balances() });
  const requests = useQuery({ queryKey: qk.leaveMy, queryFn: leaveApi.my });

  const cancel = useMutation({
    mutationFn: (id: string) => leaveApi.cancel(id),
    onSuccess: () => {
      toast.success('Leave request cancelled');
      qc.invalidateQueries({ queryKey: qk.leave });
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
          <Button icon={<Plus className="size-4" />} onClick={() => setApplyOpen(true)}>
            Apply for leave
          </Button>
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
                  </Td>
                  <Td className="text-right tabular-nums">{formatNumber(r.days)}</Td>
                  <Td>
                    <StatusBadge status={r.status} />
                  </Td>
                  <Td className="max-w-xs truncate" title={r.reason ?? undefined}>{r.reason ?? '—'}</Td>
                  <Td className="max-w-xs truncate" title={r.decisionNote ?? undefined}>{r.decisionNote ?? '—'}</Td>
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

      {applyOpen && <ApplyLeaveModal balances={balances.data ?? []} onClose={() => setApplyOpen(false)} />}

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
  const usedPct = b.entitled > 0 ? Math.min(100, (b.used / b.entitled) * 100) : 0;
  const pendingPct = b.entitled > 0 ? Math.min(100 - usedPct, (b.pending / b.entitled) * 100) : 0;
  return (
    <Card className="p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="truncate text-sm font-medium text-zinc-700 dark:text-zinc-300">{b.leaveType}</p>
        <span className="rounded bg-zinc-100 px-1.5 py-0.5 font-mono text-[10px] text-zinc-500 dark:bg-zinc-800">{b.code}</span>
      </div>
      <p className="mt-2 text-3xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">
        {formatNumber(b.available)}
        <span className="ml-1 text-sm font-normal text-zinc-500">/ {formatNumber(b.entitled)} days</span>
      </p>
      <div className="mt-3 flex h-1.5 gap-0.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" aria-hidden="true">
        <div className="h-full rounded-full bg-accent-500" style={{ width: `${usedPct}%` }} />
        <div className="h-full rounded-full bg-amber-400" style={{ width: `${pendingPct}%` }} />
      </div>
      <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">
        {formatNumber(b.used)} used · {formatNumber(b.pending)} pending
      </p>
    </Card>
  );
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
  const [error, setError] = useState<string | null>(null);

  const typeId = leaveTypeId || active[0]?.id || '';
  const range = useDebounced({ from, to }, 250);
  const validRange = !!range.from && !!range.to && range.to >= range.from;
  const days = useQuery({
    queryKey: ['leave', 'count', range.from, range.to],
    queryFn: () => leaveApi.countDays(range.from, range.to),
    enabled: validRange,
    retry: false,
  });
  const balance = balances.find((b) => b.leaveTypeId === typeId);
  const over = balance && days.data && days.data.days > balance.available;

  const apply = useMutation({
    mutationFn: () => leaveApi.apply({ leaveTypeId: typeId, fromDate: from, toDate: to, reason: reason.trim() || null }),
    onSuccess: (r) => {
      toast.success('Leave requested', `${formatNumber(r.days)} day(s) of ${r.leaveType} sent for approval.`);
      qc.invalidateQueries({ queryKey: qk.leave });
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
    if (to < from) return setError('The end date cannot be before the start date.');
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
        {days.isError && <p className="text-xs text-red-600 dark:text-red-400">{errorMessage(days.error)}</p>}
        {over && (
          <p className="text-xs text-amber-700 dark:text-amber-300">This is more than your available balance.</p>
        )}
        <Field label="Reason">
          <Textarea value={reason} maxLength={1000} onChange={(e) => setReason(e.target.value)} placeholder="Optional" />
        </Field>
      </form>
    </Modal>
  );
}
