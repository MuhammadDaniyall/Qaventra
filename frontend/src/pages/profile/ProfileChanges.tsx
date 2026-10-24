import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { profileChangesApi } from '../../api/employees';
import { qk } from '../../api/keys';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { Button, Card, CardHeader, ConfirmDialog, Field, FormError, Input, Modal, Select, Spinner, StatusBadge, Textarea, useToast } from '../../components/ui';
import { formatDateTimeUtc } from '../../lib/format';
import { BLOOD_GROUPS, MARITAL_STATUSES, type Employee, type FieldChange, type ProfileChange } from '../../types';

function show(v: string | null) {
  return v ?? <span className="text-zinc-500 dark:text-zinc-400">empty</span>;
}

export function ChangeList({ changes }: { changes: FieldChange[] }) {
  return (
    <dl className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 bg-white text-sm dark:divide-zinc-800 dark:border-zinc-800 dark:bg-zinc-900">
      {changes.map((c) => (
        <div key={c.field} className="grid gap-1 px-3 py-2 sm:grid-cols-[150px_1fr]">
          <dt className="text-zinc-500 dark:text-zinc-400">{c.label}</dt>
          <dd className="flex flex-wrap items-center gap-1.5">
            <span className="text-zinc-500 dark:text-zinc-400 line-through decoration-zinc-300 dark:decoration-zinc-600">{show(c.from)}</span>
            <ArrowRight className="size-3.5 text-zinc-500 dark:text-zinc-400" aria-hidden="true" />
            <span className="font-medium text-zinc-900 dark:text-zinc-100">{show(c.to)}</span>
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** What an approver is asked to approve: each detail, old and new. */
export function ProfileChangeDetails({ id }: { id: string }) {
  const pending = useQuery({ queryKey: [...qk.employees, 'profile-changes', 'Pending'], queryFn: () => profileChangesApi.list('Pending') });
  const request = pending.data?.find((r) => r.id === id);
  if (pending.isLoading) return <Spinner />;
  if (!request) return null;
  return (
    <div className="space-y-2">
      <ChangeList changes={request.changes} />
      {request.reason && <p className="text-sm text-zinc-600 dark:text-zinc-400">Reason: {request.reason}</p>}
    </div>
  );
}

type Draft = Record<string, string>;

const TEXT_FIELDS: { key: string; label: string; type?: string; placeholder?: string; wide?: boolean }[] = [
  { key: 'firstName', label: 'First name' },
  { key: 'lastName', label: 'Last name' },
  { key: 'phone', label: 'Phone', type: 'tel', placeholder: '0300-1234567' },
  { key: 'personalEmail', label: 'Personal email', type: 'email' },
  { key: 'cnic', label: 'CNIC', placeholder: '12345-1234567-1' },
  { key: 'dateOfBirth', label: 'Date of birth', type: 'date' },
  { key: 'bankName', label: 'Bank' },
  { key: 'bankAccount', label: 'Bank account / IBAN' },
  { key: 'address', label: 'Address', wide: true },
];

function draftOf(e: Employee): Draft {
  return {
    firstName: e.firstName,
    lastName: e.lastName,
    phone: e.phone ?? '',
    personalEmail: e.personalEmail ?? '',
    cnic: e.cnic ?? '',
    dateOfBirth: e.dateOfBirth ?? '',
    bankName: e.bankName ?? '',
    bankAccount: e.bankAccount ?? '',
    address: e.address ?? '',
    maritalStatus: e.maritalStatus ?? '',
    bloodGroup: e.bloodGroup ?? '',
  };
}

/** Employee: edit details and send them for approval. Only what changed is sent. */
export function ProfileChangeModal({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const original = draftOf(employee);
  const [draft, setDraft] = useState<Draft>(original);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const changed = Object.keys(draft).filter((k) => draft[k].trim() !== original[k]);
  const submit = useMutation({
    mutationFn: () => profileChangesApi.submit(Object.fromEntries(changed.map((k) => [k, draft[k].trim() || null])), reason.trim() || null),
    onSuccess: (r) => {
      toast.success(r.status === 'Approved' ? 'Details updated' : 'Change request sent', r.status === 'Approved' ? undefined : 'You will be notified once it is approved.');
      qc.invalidateQueries({ queryKey: qk.employees });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const set = (key: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => setDraft((d) => ({ ...d, [key]: e.target.value }));

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Request a change to my details"
      description="Edit what is wrong or out of date. Your approver checks it before your record changes."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={submit.isPending} disabled={!changed.length} onClick={() => submit.mutate()}>
            {changed.length ? `Send ${changed.length} change${changed.length === 1 ? '' : 's'}` : 'No changes yet'}
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        {TEXT_FIELDS.map((f) =>
          f.wide ? (
            <Field key={f.key} label={f.label} className="sm:col-span-2">
              <Textarea rows={2} maxLength={500} value={draft[f.key]} onChange={set(f.key)} />
            </Field>
          ) : (
            <Field key={f.key} label={f.label}>
              <Input type={f.type ?? 'text'} placeholder={f.placeholder} value={draft[f.key]} onChange={set(f.key)} />
            </Field>
          ),
        )}
        <Field label="Marital status">
          <Select value={draft.maritalStatus} onChange={set('maritalStatus')}>
            <option value="">Not specified</option>
            {MARITAL_STATUSES.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Blood group">
          <Select value={draft.bloodGroup} onChange={set('bloodGroup')}>
            <option value="">Not specified</option>
            {BLOOD_GROUPS.map((b) => (
              <option key={b} value={b}>
                {b}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Reason" hint="Optional, e.g. new bank account or name after marriage" className="sm:col-span-2">
          <Textarea rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

/** The employee's own change requests, newest first. Hidden when there are none. */
export function MyProfileChanges() {
  const toast = useToast();
  const qc = useQueryClient();
  const [cancelling, setCancelling] = useState<ProfileChange | null>(null);
  const list = useQuery({ queryKey: qk.profileChangesMy, queryFn: profileChangesApi.my });
  const cancel = useMutation({
    mutationFn: (id: string) => profileChangesApi.cancel(id),
    onSuccess: () => {
      toast.success('Request cancelled');
      setCancelling(null);
      qc.invalidateQueries({ queryKey: qk.employees });
    },
    onError: (err) => toast.error(err),
  });
  if (!list.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="My change requests" />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {list.data.slice(0, 5).map((r) => (
          <li key={r.id} className="space-y-3 px-5 py-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2 text-sm">
                <StatusBadge status={r.status} />
                <span className="text-zinc-500 dark:text-zinc-400">sent {formatDateTimeUtc(r.createdAtUtc)}</span>
              </div>
              {r.status === 'Pending' && (
                <Button variant="ghost" size="sm" onClick={() => setCancelling(r)}>
                  Cancel request
                </Button>
              )}
            </div>
            <ChangeList changes={r.changes} />
            {r.decisionNote && <p className="text-sm text-zinc-600 dark:text-zinc-400">Note: {r.decisionNote}</p>}
            {r.status === 'Pending' && <ApprovalTimeline type="ProfileChange" subjectId={r.id} />}
          </li>
        ))}
      </ul>
      <ConfirmDialog
        open={!!cancelling}
        title="Cancel this request?"
        message="Your details stay as they are. You can send a new request any time."
        confirmLabel="Cancel request"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling.id)}
        onClose={() => setCancelling(null)}
      />
    </Card>
  );
}
