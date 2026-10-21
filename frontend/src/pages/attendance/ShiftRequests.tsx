import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeftRight, Repeat } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useShifts } from '../../api/hooks';
import { qk } from '../../api/keys';
import { schedulingApi, type ShiftRequest } from '../../api/scheduling';
import { useUser } from '../../auth/AuthContext';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { Badge, Button, Card, CardHeader, ConfirmDialog, Field, FormError, Input, Modal, Select, StatusBadge, Textarea, useToast } from '../../components/ui';
import { formatDate, toIsoDate } from '../../lib/format';

const STATUS_LABELS: Partial<Record<ShiftRequest['status'], string>> = { AwaitingColleague: 'Waiting for colleague' };

export function rangeLabel(from: string, to: string) {
  return from === to ? formatDate(from) : `${formatDate(from)} – ${formatDate(to)}`;
}

function describe(r: ShiftRequest, mine: boolean) {
  if (r.kind === 'Change') return `Change to ${r.shiftName ?? 'another shift'}`;
  return mine ? `Swap with ${r.swapWithName}` : `${r.employeeName} wants to swap with you`;
}

/** Ask for a different shift, or to swap days with a colleague (who agrees first). */
export function ShiftRequestModal({ kind, onClose }: { kind: 'Change' | 'Swap'; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const shifts = useShifts();
  const colleagues = useQuery({ queryKey: [...qk.attendance, 'colleagues'], queryFn: schedulingApi.colleagues, enabled: kind === 'Swap' });
  const tomorrow = toIsoDate(new Date(Date.now() + 86_400_000));
  const [from, setFrom] = useState(tomorrow);
  const [to, setTo] = useState(tomorrow);
  const [shiftId, setShiftId] = useState('');
  const [colleagueId, setColleagueId] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const submit = useMutation({
    mutationFn: () =>
      kind === 'Change'
        ? schedulingApi.requestChange({ from, to, shiftId, reason: reason.trim() })
        : schedulingApi.requestSwap({ colleagueId, from, to, reason: reason.trim() }),
    onSuccess: (r) => {
      toast.success(
        r.status === 'Approved' ? 'Shift updated' : 'Request sent',
        r.status === 'AwaitingColleague' ? `${r.swapWithName} has to agree first, then it goes to your approver.` : undefined,
      );
      qc.invalidateQueries({ queryKey: qk.attendance });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={kind === 'Change' ? 'Request a different shift' : 'Swap shifts with a colleague'}
      description={
        kind === 'Change'
          ? 'For the working days you choose. Your days off stay off.'
          : 'You take their schedule on these days and they take yours. They agree first, then your approver decides.'
      }
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={submit.isPending}
            onClick={() => {
              setError(null);
              if (kind === 'Change' && !shiftId) return setError('Choose the shift you want.');
              if (kind === 'Swap' && !colleagueId) return setError('Choose who to swap with.');
              if (!reason.trim()) return setError('Give a reason.');
              submit.mutate();
            }}
          >
            Send request
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2">
          <FormError message={error} />
        </div>
        {kind === 'Change' ? (
          <Field label="Shift" required className="col-span-2">
            <Select value={shiftId} onChange={(e) => setShiftId(e.target.value)}>
              <option value="">Choose…</option>
              {shifts.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </Select>
          </Field>
        ) : (
          <Field label="Colleague" required className="col-span-2">
            <Select value={colleagueId} onChange={(e) => setColleagueId(e.target.value)}>
              <option value="">Choose…</option>
              {colleagues.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <Field label="From" required>
          <Input type="date" value={from} min={toIsoDate(new Date())} onChange={(e) => setFrom(e.target.value)} />
        </Field>
        <Field label="To" required>
          <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
        </Field>
        <Field label="Reason" required className="col-span-2">
          <Textarea rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

/** My shift requests, and swaps colleagues asked me for (with accept / decline). Hidden when there are none. */
export function MyShiftRequests() {
  const me = useUser();
  const toast = useToast();
  const qc = useQueryClient();
  const [cancelling, setCancelling] = useState<ShiftRequest | null>(null);
  const list = useQuery({ queryKey: qk.shiftRequestsMy, queryFn: schedulingApi.myShiftRequests });
  const done = (msg: string) => {
    toast.success(msg);
    setCancelling(null);
    qc.invalidateQueries({ queryKey: qk.attendance });
    qc.invalidateQueries({ queryKey: qk.approvals });
  };
  const respond = useMutation({
    mutationFn: ({ id, accept }: { id: string; accept: boolean }) => (accept ? schedulingApi.accept(id) : schedulingApi.decline(id)),
    onSuccess: (_, v) => done(v.accept ? 'Swap accepted. It now goes for approval.' : 'Swap declined'),
    onError: (err) => toast.error(err),
  });
  const cancel = useMutation({
    mutationFn: (id: string) => schedulingApi.cancel(id),
    onSuccess: () => done('Request cancelled'),
    onError: (err) => toast.error(err),
  });
  if (!list.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="Shift requests" description="Shift changes and swaps" />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {list.data.slice(0, 10).map((r) => {
          const mine = r.employeeId === me.employeeId;
          const askingMe = !mine && r.status === 'AwaitingColleague';
          return (
            <li key={r.id} className="space-y-2 px-5 py-3">
              <div className="flex flex-wrap items-center gap-3">
                {r.kind === 'Swap' ? <ArrowLeftRight className="size-4 text-zinc-400" /> : <Repeat className="size-4 text-zinc-400" />}
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-zinc-900 dark:text-white">
                    {describe(r, mine)} · {rangeLabel(r.fromDate, r.toDate)}
                  </p>
                  <p className="truncate text-xs text-zinc-500">
                    {r.reason}
                    {r.decisionNote ? ` · Note: ${r.decisionNote}` : ''}
                  </p>
                </div>
                {askingMe ? (
                  <Badge tone="blue">Needs your answer</Badge>
                ) : (
                  <StatusBadge status={r.status} label={STATUS_LABELS[r.status]} />
                )}
                {askingMe && (
                  <div className="flex gap-2">
                    <Button size="sm" variant="secondary" loading={respond.isPending} onClick={() => respond.mutate({ id: r.id, accept: false })}>
                      Decline
                    </Button>
                    <Button size="sm" variant="success" loading={respond.isPending} onClick={() => respond.mutate({ id: r.id, accept: true })}>
                      Accept
                    </Button>
                  </div>
                )}
                {mine && (r.status === 'Pending' || r.status === 'AwaitingColleague') && (
                  <Button variant="ghost" size="sm" onClick={() => setCancelling(r)}>
                    Cancel
                  </Button>
                )}
              </div>
              {mine && r.status === 'Pending' && <ApprovalTimeline type="ShiftChange" subjectId={r.id} />}
            </li>
          );
        })}
      </ul>
      <ConfirmDialog
        open={!!cancelling}
        title="Cancel this request?"
        message="Your shifts stay as they are."
        confirmLabel="Cancel request"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling.id)}
        onClose={() => setCancelling(null)}
      />
    </Card>
  );
}
