import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarPlus } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { compOffApi } from '../../api/leave';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { Button, Card, CardHeader, ConfirmDialog, Field, FormError, Input, Modal, Select, StatusBadge, Textarea, useToast } from '../../components/ui';
import { formatDate, formatMinutes, formatNumber, toIsoDate, todayIso } from '../../lib/format';

/** Claim compensatory leave for work on a weekly off day or a holiday. */
export function ClaimCompOffModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [workedOn, setWorkedOn] = useState('');
  const [days, setDays] = useState('1');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const submit = useMutation({
    mutationFn: () => compOffApi.submit(workedOn, Number(days), reason.trim()),
    onSuccess: (c) => {
      toast.success('Comp-off claimed', c.status === 'Approved' ? `${formatNumber(c.days)} day added to your compensatory leave.` : 'Sent for approval.');
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!workedOn) return setError('Choose the day you worked.');
    if (!reason.trim()) return setError('Say what you worked on.');
    submit.mutate();
  };

  return (
    <Modal
      open
      onClose={onClose}
      title="Claim comp-off"
      description="Worked on a weekend or a holiday? Once approved, the day is added to your compensatory leave."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="comp-off-form" loading={submit.isPending}>
            Send claim
          </Button>
        </>
      }
    >
      <form id="comp-off-form" onSubmit={onSubmit} className="space-y-4">
        <FormError message={error} />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Day worked" required hint="A weekly off day or a holiday, with attendance">
            <Input type="date" value={workedOn} min={toIsoDate(new Date(Date.now() - 60 * 86_400_000))} max={todayIso()} onChange={(e) => setWorkedOn(e.target.value)} />
          </Field>
          <Field label="Claim">
            <Select value={days} onChange={(e) => setDays(e.target.value)}>
              <option value="1">Full day</option>
              <option value="0.5">Half day</option>
            </Select>
          </Field>
        </div>
        <Field label="What did you work on?" required>
          <Textarea value={reason} rows={3} maxLength={1000} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Production release over the weekend" />
        </Field>
      </form>
    </Modal>
  );
}

export function MyCompOffClaims() {
  const toast = useToast();
  const qc = useQueryClient();
  const [open, setOpen] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const list = useQuery({ queryKey: qk.compOffMy, queryFn: compOffApi.my });
  const cancel = useMutation({
    mutationFn: (id: string) => compOffApi.cancel(id),
    onSuccess: () => {
      toast.success('Claim cancelled');
      setCancelling(null);
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.approvals });
    },
    onError: (err) => toast.error(err),
  });
  if (!list.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="Comp-off claims" description="Weekend and holiday work turned into leave" />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {list.data.map((c) => (
          <li key={c.id} className="px-5 py-3">
            <div className="flex flex-wrap items-center gap-3">
              <CalendarPlus className="size-4 text-zinc-400" aria-hidden="true" />
              <button type="button" className="min-w-0 flex-1 text-left" onClick={() => setOpen(open === c.id ? null : c.id)} aria-expanded={open === c.id}>
                <span className="block text-sm font-medium text-zinc-900 dark:text-white">
                  {c.days === 1 ? 'Full day' : 'Half day'} for {formatDate(c.workedOn)}
                </span>
                <span className="block truncate text-xs text-zinc-500">
                  {c.workedMinutes ? `Worked ${formatMinutes(c.workedMinutes)} · ` : ''}
                  {c.reason}
                </span>
              </button>
              <StatusBadge status={c.status} />
              {c.status === 'Pending' && (
                <Button variant="ghost" size="sm" onClick={() => setCancelling(c.id)}>
                  Cancel
                </Button>
              )}
            </div>
            {open === c.id && (
              <div className="mt-3 ml-7">
                <ApprovalTimeline type="CompOff" subjectId={c.id} />
              </div>
            )}
          </li>
        ))}
      </ul>
      <ConfirmDialog
        open={!!cancelling}
        title="Cancel this claim?"
        message="Your approver will no longer see it."
        confirmLabel="Cancel claim"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling)}
        onClose={() => setCancelling(null)}
      />
    </Card>
  );
}
