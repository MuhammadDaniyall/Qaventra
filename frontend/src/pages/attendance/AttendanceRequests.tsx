import { useState, type FormEvent } from 'react';
import { OnBehalfField } from '../../components/engine';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, ClipboardEdit, Clock, Fingerprint, Home, type LucideIcon } from 'lucide-react';
import { attendanceRequestsApi, type AttendanceRequestKind } from '../../api/attendance';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import {
  Button,
  Card,
  CardHeader,
  ConfirmDialog,
  Field,
  FormError,
  Input,
  Modal,
  StatusBadge,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, formatTime, toIsoDate, todayIso } from '../../lib/format';

export const KINDS: Array<{ value: AttendanceRequestKind; label: string; hint: string; icon: LucideIcon }> = [
  { value: 'MissedPunch', label: 'Missed punch', hint: 'You were in office but forgot to check in or out.', icon: Fingerprint },
  { value: 'WorkFromHome', label: 'Work from home', hint: 'You worked remotely that day.', icon: Home },
  { value: 'OfficialDuty', label: 'Official duty', hint: 'Client visit, field work or training outside the office.', icon: Briefcase },
  { value: 'TimeCorrection', label: 'Time correction', hint: 'Your recorded times are wrong.', icon: Clock },
];
export const kindLabel = (k: AttendanceRequestKind) => KINDS.find((x) => x.value === k)?.label ?? k;

/** Ask to add or fix a day's attendance. */
export function RequestAttendanceModal({ date: initialDate, onClose }: { date?: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const yesterday = toIsoDate(new Date(Date.now() - 86_400_000));
  const [kind, setKind] = useState<AttendanceRequestKind>('MissedPunch');
  const [date, setDate] = useState(initialDate ?? yesterday);
  const [checkIn, setCheckIn] = useState('09:00');
  const [checkOut, setCheckOut] = useState('18:00');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [forWhom, setForWhom] = useState('');
  const minDate = toIsoDate(new Date(Date.now() - 45 * 86_400_000));

  const submit = useMutation({
    mutationFn: () =>
      attendanceRequestsApi.submit({
        date,
        kind,
        checkIn: `${checkIn}:00`,
        checkOut: checkOut ? `${checkOut}:00` : null,
        reason: reason.trim(),
        employeeId: forWhom || null,
      }),
    onSuccess: (r) => {
      toast.success('Request sent', r.status === 'Approved' ? 'Approved automatically; your attendance is updated.' : 'Your approver has been notified.');
      qc.invalidateQueries({ queryKey: qk.attendance });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!date || !checkIn) return setError('Choose the date and check-in time.');
    if (checkOut && checkOut <= checkIn) return setError('Check-out must be after check-in.');
    if (!reason.trim()) return setError('Add a short reason for your approver.');
    submit.mutate();
  };

  return (
    <Modal
      open
      onClose={onClose}
      title="Attendance request"
      description="Your approver reviews it; once approved, the day is updated automatically."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="attendance-request-form" loading={submit.isPending}>
            Send request
          </Button>
        </>
      }
    >
      <form id="attendance-request-form" onSubmit={onSubmit} className="space-y-4">
        <FormError message={error} />
        <OnBehalfField value={forWhom} onChange={setForWhom} />
        <div className="grid grid-cols-2 gap-2" role="radiogroup" aria-label="Request type">
          {KINDS.map((k) => (
            <button
              key={k.value}
              type="button"
              role="radio"
              aria-checked={kind === k.value}
              onClick={() => setKind(k.value)}
              className={cn(
                'flex items-start gap-2 rounded-xl border p-3 text-start transition-colors',
                kind === k.value
                  ? 'border-accent-500 bg-accent-50 dark:bg-accent-500/10'
                  : 'border-zinc-200 hover:border-zinc-300 dark:border-zinc-700',
              )}
            >
              <k.icon className={cn('mt-0.5 size-4 shrink-0', kind === k.value ? 'text-accent-600' : 'text-zinc-400')} aria-hidden="true" />
              <span>
                <span className="block text-sm font-medium text-zinc-900 dark:text-white">{k.label}</span>
                <span className="block text-xs text-zinc-500">{k.hint}</span>
              </span>
            </button>
          ))}
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Date" required>
            <Input type="date" value={date} min={minDate} max={todayIso()} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field label="Check in" required>
            <Input type="time" value={checkIn} onChange={(e) => setCheckIn(e.target.value)} />
          </Field>
          <Field label="Check out" hint="Leave empty if you have not left yet">
            <Input type="time" value={checkOut} onChange={(e) => setCheckOut(e.target.value)} />
          </Field>
        </div>
        <Field label="Reason" required>
          <Textarea
            value={reason}
            maxLength={1000}
            rows={3}
            onChange={(e) => setReason(e.target.value)}
            placeholder={kind === 'OfficialDuty' ? 'e.g. Visit to client office in DHA' : kind === 'WorkFromHome' ? 'e.g. Internet installation at home' : 'e.g. Forgot to check out'}
          />
        </Field>
      </form>
    </Modal>
  );
}

/** The employee's own requests with their approval progress. */
export function MyAttendanceRequests() {
  const toast = useToast();
  const qc = useQueryClient();
  const [open, setOpen] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const list = useQuery({ queryKey: qk.attendanceRequestsMy, queryFn: attendanceRequestsApi.my });
  const cancel = useMutation({
    mutationFn: (id: string) => attendanceRequestsApi.cancel(id),
    onSuccess: () => {
      toast.success('Request cancelled');
      setCancelling(null);
      qc.invalidateQueries({ queryKey: qk.attendance });
      qc.invalidateQueries({ queryKey: qk.approvals });
    },
    onError: (err) => toast.error(err),
  });

  if (!list.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="My attendance requests" description="Missed punches, work from home and corrections" />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {list.data.map((r) => (
          <li key={r.id} className="px-5 py-3">
            <div className="flex flex-wrap items-center gap-3">
              <ClipboardEdit className="size-4 text-zinc-500 dark:text-zinc-400" aria-hidden="true" />
              <button type="button" onClick={() => setOpen(open === r.id ? null : r.id)} className="min-w-0 flex-1 text-start" aria-expanded={open === r.id}>
                <span className="block text-sm font-medium text-zinc-900 dark:text-white">
                  {kindLabel(r.kind)} · {formatDate(r.date)}
                </span>
                <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">
                  {formatTime(r.checkIn)}
                  {r.checkOut && ` – ${formatTime(r.checkOut)}`} · {r.reason}
                </span>
              </button>
              <StatusBadge status={r.status} />
              {r.status === 'Pending' && (
                <Button variant="ghost" size="sm" onClick={() => setCancelling(r.id)}>
                  Cancel
                </Button>
              )}
            </div>
            {open === r.id && (
              <div className="mt-3 ms-7">
                <ApprovalTimeline type="Attendance" subjectId={r.id} />
              </div>
            )}
          </li>
        ))}
      </ul>
      <ConfirmDialog
        open={!!cancelling}
        title="Cancel this request?"
        message="Your approver will no longer see it."
        confirmLabel="Cancel request"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling)}
        onClose={() => setCancelling(null)}
      />
    </Card>
  );
}
