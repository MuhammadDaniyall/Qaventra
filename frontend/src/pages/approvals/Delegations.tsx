import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { UserRoundCheck } from 'lucide-react';
import { approvalsApi, REQUEST_LABELS, type ApprovalRequestType } from '../../api/approvals';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Badge, Button, Card, CardHeader, Checkbox, Field, FormError, Input, Modal, Select, Textarea, useToast } from '../../components/ui';
import { formatDate, todayIso } from '../../lib/format';

/** Hand approvals to someone else while away; see who stands in for whom. */
export function Delegations() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: qk.delegations, queryFn: approvalsApi.delegations });
  const [open, setOpen] = useState(false);
  const cancel = useMutation({
    mutationFn: approvalsApi.cancelDelegation,
    onSuccess: () => {
      toast.success('Delegation ended');
      qc.invalidateQueries({ queryKey: qk.approvals });
    },
    onError: (e) => toast.error(e),
  });
  const rows = (list.data ?? []).filter((d) => !d.isCancelled && d.toDate >= todayIso());
  return (
    <Card className="mt-5">
      <CardHeader
        title="Delegation"
        description="Away? Hand your approvals to a colleague for those days. They approve in your name, and it shows who did."
        actions={
          <Button size="sm" variant="secondary" icon={<UserRoundCheck className="size-4" />} onClick={() => setOpen(true)}>
            Delegate approvals
          </Button>
        }
      />
      {rows.length ? (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {rows.map((d) => (
            <li key={d.id} className="flex flex-wrap items-center gap-3 px-5 py-3 text-sm">
              <div className="min-w-0 flex-1">
                <p>
                  <span className="font-medium">{d.to}</span> approves for <span className="font-medium">{d.from}</span>
                  {d.isActive && (
                    <Badge tone="green" className="ml-2">
                      Now
                    </Badge>
                  )}
                </p>
                <p className="text-xs text-zinc-500">
                  {formatDate(d.fromDate)} – {formatDate(d.toDate)} ·{' '}
                  {d.requestTypes.length ? d.requestTypes.map((t) => REQUEST_LABELS[t]).join(', ') : 'All requests'}
                  {d.reason ? ` · ${d.reason}` : ''}
                </p>
              </div>
              {d.canCancel && (
                <Button size="sm" variant="ghost" loading={cancel.isPending && cancel.variables === d.id} onClick={() => cancel.mutate(d.id)}>
                  End
                </Button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="px-5 py-4 text-sm text-zinc-500">No delegations running.</p>
      )}
      {open && <DelegateModal onClose={() => setOpen(false)} />}
    </Card>
  );
}

function DelegateModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const user = useUser();
  const hr = isHr(user);
  const people = useEmployeeLookup();
  const [from, setFrom] = useState(hr ? '' : (user.employeeId ?? ''));
  const [to, setTo] = useState('');
  const [start, setStart] = useState(todayIso());
  const [end, setEnd] = useState(todayIso());
  const [all, setAll] = useState(true);
  const [types, setTypes] = useState<ApprovalRequestType[]>([]);
  const [reason, setReason] = useState('');
  const save = useMutation({
    mutationFn: () =>
      approvalsApi.delegate({
        fromEmployeeId: hr && from ? from : null,
        toEmployeeId: to,
        fromDate: start,
        toDate: end,
        requestTypes: all ? [] : types,
        reason: reason.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Approvals delegated', 'Both of you have been told.');
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title="Delegate approvals"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            disabled={!to || !start || !end || (!all && !types.length) || (hr && !from && !user.employeeId)}
            onClick={() => save.mutate()}
          >
            Delegate
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        {hr && (
          <Field label="Whose approvals" hint="HR can set this up for anyone" className="sm:col-span-2">
            <Select value={from} onChange={(e) => setFrom(e.target.value)}>
              <option value="">{user.employeeId ? 'Mine' : 'Choose…'}</option>
              {people.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <Field label="Approved by" required className="sm:col-span-2">
          <Select value={to} onChange={(e) => setTo(e.target.value)}>
            <option value="">Choose a colleague…</option>
            {people.data
              ?.filter((p) => p.id !== (from || user.employeeId))
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
          </Select>
        </Field>
        <Field label="From">
          <Input type="date" min={todayIso()} value={start} onChange={(e) => setStart(e.target.value)} />
        </Field>
        <Field label="To">
          <Input type="date" min={start} value={end} onChange={(e) => setEnd(e.target.value)} />
        </Field>
        <div className="space-y-2 sm:col-span-2">
          <Checkbox label="All kinds of requests" checked={all} onChange={(e) => setAll(e.target.checked)} />
          {!all && (
            <div className="grid grid-cols-2 gap-1.5 pl-6">
              {(Object.keys(REQUEST_LABELS) as ApprovalRequestType[]).map((t) => (
                <Checkbox
                  key={t}
                  label={REQUEST_LABELS[t]}
                  checked={types.includes(t)}
                  onChange={(e) => setTypes((x) => (e.target.checked ? [...x, t] : x.filter((y) => y !== t)))}
                />
              ))}
            </div>
          )}
        </div>
        <Field label="Reason" className="sm:col-span-2">
          <Textarea rows={2} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} placeholder="Annual leave" />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
