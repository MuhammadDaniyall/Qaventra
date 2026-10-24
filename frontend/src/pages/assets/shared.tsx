import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Package } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { ASSET_STATUS_LABELS, assetsApi, CONDITIONS, type AssetCondition, type AssetRequest, type AssetStatus, type Assignment } from '../../api/assets';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Badge, Button, Field, FormError, Input, Modal, Select, Textarea, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, todayIso } from '../../lib/format';

const STATUS_TONES: Record<AssetStatus, 'green' | 'blue' | 'amber' | 'gray' | 'red'> = {
  Available: 'green',
  Assigned: 'blue',
  InRepair: 'amber',
  Retired: 'gray',
  Lost: 'red',
};

export function AssetStatusBadge({ status }: { status: AssetStatus }) {
  return (
    <Badge tone={STATUS_TONES[status]} dot>
      {ASSET_STATUS_LABELS[status]}
    </Badge>
  );
}

export const REQUEST_STATUS_LABELS: Record<AssetRequest['status'], string> = {
  Pending: 'Waiting for approval',
  Approved: 'Approved, waiting for hand-over',
  Fulfilled: 'Done',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
};

export function requestLabel(r: AssetRequest) {
  if (r.kind === 'Return') return r.status === 'Approved' ? 'Waiting to be taken back' : REQUEST_STATUS_LABELS[r.status];
  return REQUEST_STATUS_LABELS[r.status];
}

function useInvalidate() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: qk.assets });
    qc.invalidateQueries({ queryKey: qk.approvals });
    qc.invalidateQueries({ queryKey: qk.notifications });
  };
}

function ConditionSelect({ value, onChange }: { value: AssetCondition; onChange: (c: AssetCondition) => void }) {
  return (
    <Select value={value} onChange={(e) => onChange(e.target.value as AssetCondition)}>
      {CONDITIONS.map((c) => (
        <option key={c} value={c}>
          {c}
        </option>
      ))}
    </Select>
  );
}

/** Things someone holds now (or held), as cards. */
export function AssignmentCards({ items, action }: { items: Assignment[]; action?: (a: Assignment) => React.ReactNode }) {
  return (
    <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {items.map((a) => (
        <li key={a.id} className={cn('rounded-xl border bg-white p-4 dark:bg-zinc-900', a.isOverdue ? 'border-red-300 dark:border-red-500/40' : 'border-zinc-200 dark:border-zinc-800')}>
          <div className="flex items-start gap-3">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-accent-50 text-accent-600 dark:bg-accent-500/10 dark:text-accent-300">
              <Package className="size-4" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium text-zinc-900 dark:text-zinc-100">{a.assetName}</p>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                <span className="font-mono">{a.tag}</span> · {a.type}
                {a.serialNumber ? ` · SN ${a.serialNumber}` : ''}
              </p>
            </div>
          </div>
          <dl className="mt-3 space-y-1 text-xs text-zinc-600 dark:text-zinc-400">
            <div>
              Since {formatDate(a.assignedOn)}
              {a.handedOverByName ? ` · from ${a.handedOverByName}` : ''}
            </div>
            {a.dueBack && !a.returnedOn && (
              <div className={cn(a.isOverdue && 'flex items-center gap-1 font-medium text-red-600 dark:text-red-400')}>
                {a.isOverdue && <AlertTriangle className="size-3.5" />}Return by {formatDate(a.dueBack)}
              </div>
            )}
            {a.returnedOn && (
              <div>
                Returned {formatDate(a.returnedOn)}
                {a.conditionIn ? ` · ${a.conditionIn.toLowerCase()}` : ''}
              </div>
            )}
            {a.notes && <div className="whitespace-pre-line text-zinc-500 dark:text-zinc-400">{a.notes}</div>}
          </dl>
          {(a.returnRequested || action) && (
            <div className="mt-3 flex items-center justify-between gap-2">
              {a.returnRequested ? <Badge tone="amber">Return requested</Badge> : <span />}
              {action?.(a)}
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}

/** Employee: ask for an asset. */
export function RequestAssetModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const invalidate = useInvalidate();
  const types = useQuery({ queryKey: qk.assetTypes, queryFn: assetsApi.types });
  const [typeId, setTypeId] = useState('');
  const [reason, setReason] = useState('');
  const [neededBy, setNeededBy] = useState('');
  const [temporary, setTemporary] = useState(false);
  const [returnBy, setReturnBy] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => assetsApi.requestNew({ typeId, reason: reason.trim(), neededBy: neededBy || null, returnBy: temporary ? returnBy || null : null }),
    onSuccess: (r) => {
      toast.success('Request sent', r.status === 'Pending' ? 'Your approver has been notified.' : 'The team will hand it over.');
      invalidate();
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title="Request an asset"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!typeId) return setError('Choose what you need.');
              if (!reason.trim()) return setError('Say why you need it.');
              if (temporary && !returnBy) return setError('Set when you will return it.');
              save.mutate();
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
        <Field label="What" required className="col-span-2">
          <Select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
            <option value="">Choose…</option>
            {types.data?.filter((t) => t.isActive).map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Why" required className="col-span-2">
          <Textarea rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Field>
        <Field label="Needed by">
          <Input type="date" min={todayIso()} value={neededBy} onChange={(e) => setNeededBy(e.target.value)} />
        </Field>
        <Field label="For how long">
          <Select value={temporary ? 'temp' : 'keep'} onChange={(e) => setTemporary(e.target.value === 'temp')}>
            <option value="keep">To keep</option>
            <option value="temp">For a while</option>
          </Select>
        </Field>
        {temporary && (
          <Field label="Return by" required className="col-span-2">
            <Input type="date" min={neededBy || todayIso()} value={returnBy} onChange={(e) => setReturnBy(e.target.value)} />
          </Field>
        )}
      </div>
    </Modal>
  );
}

/** Handler: hand an asset to someone, directly or for an approved request. */
export function HandOverModal({
  assetId,
  request,
  onClose,
}: {
  assetId?: string;
  request?: AssetRequest;
  onClose: () => void;
}) {
  const toast = useToast();
  const invalidate = useInvalidate();
  const people = useEmployeeLookup(!request);
  const available = useQuery({
    queryKey: [...qk.assets, 'available', request?.typeId],
    queryFn: () => assetsApi.list({ status: 'Available', typeId: request?.typeId }),
    enabled: !!request,
  });
  const [employeeId, setEmployeeId] = useState(request?.employeeId ?? '');
  const [chosen, setChosen] = useState(assetId ?? '');
  const [on, setOn] = useState(todayIso());
  const [dueBack, setDueBack] = useState(request?.returnBy ?? '');
  const [condition, setCondition] = useState<AssetCondition>('Good');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () =>
      request
        ? assetsApi.fulfill(request.id, { assetId: chosen, on, condition, notes: notes.trim() || null })
        : assetsApi.assign(chosen, { employeeId, assignedOn: on, dueBack: dueBack || null, condition, notes: notes.trim() || null }),
    onSuccess: () => {
      toast.success('Handed over');
      invalidate();
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={request ? `Hand over a ${request.type.toLowerCase()} to ${request.employeeName}` : 'Hand over'}
      description={request?.reason}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!chosen) return setError('Choose the asset.');
              if (!employeeId) return setError('Choose who gets it.');
              save.mutate();
            }}
          >
            Hand over
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2">
          <FormError message={error} />
        </div>
        {request ? (
          <Field label="Asset" required className="col-span-2" hint={available.data && !available.data.length ? `No ${request.type.toLowerCase()} is available. Add one to the inventory first.` : undefined}>
            <Select value={chosen} onChange={(e) => setChosen(e.target.value)}>
              <option value="">Choose…</option>
              {available.data?.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.tag} · {a.name}
                  {a.serialNumber ? ` · ${a.serialNumber}` : ''}
                </option>
              ))}
            </Select>
          </Field>
        ) : (
          <Field label="To" required className="col-span-2">
            <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
              <option value="">Choose…</option>
              {people.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <Field label="Handed over on">
          <Input type="date" value={on} onChange={(e) => setOn(e.target.value)} />
        </Field>
        <Field label="Condition">
          <ConditionSelect value={condition} onChange={setCondition} />
        </Field>
        {!request && (
          <Field label="Return by" hint="Only for a temporary loan" className="col-span-2">
            <Input type="date" min={on} value={dueBack} onChange={(e) => setDueBack(e.target.value)} />
          </Field>
        )}
        <Field label="Notes" hint="Accessories, chargers, anything to remember" className="col-span-2">
          <Textarea rows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

/** Handler: take an asset back, directly or for a return request. */
export function TakeBackModal({ assignmentId, request, label, onClose }: { assignmentId?: string; request?: AssetRequest; label: string; onClose: () => void }) {
  const toast = useToast();
  const invalidate = useInvalidate();
  const [on, setOn] = useState(todayIso());
  const [condition, setCondition] = useState<AssetCondition>('Good');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () =>
      request
        ? assetsApi.fulfill(request.id, { assetId: null, on, condition, notes: notes.trim() || null })
        : assetsApi.takeBack(assignmentId!, { returnedOn: on, condition, notes: notes.trim() || null }),
    onSuccess: () => {
      toast.success('Taken back', condition === 'Damaged' ? 'Marked for repair.' : undefined);
      invalidate();
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Take back ${label}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Take back
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Returned on">
          <Input type="date" value={on} onChange={(e) => setOn(e.target.value)} />
        </Field>
        <Field label="Condition" hint={condition === 'Damaged' ? 'Goes to repair' : undefined}>
          <ConditionSelect value={condition} onChange={setCondition} />
        </Field>
        <Field label="Notes" className="col-span-2">
          <Textarea rows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}
