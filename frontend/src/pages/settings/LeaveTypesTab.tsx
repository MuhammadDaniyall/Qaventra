import { useState } from 'react';
import { LeaveReasonsModal } from './LeaveReasonsModal';
import { Palmtree, Plus } from 'lucide-react';
import { setupApi } from '../../api/organization';
import { useLeaveTypes } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Badge, Button, Card, CardHeader, Checkbox, EmptyState, ErrorState, Field, Input, Select, SkeletonRows, StatusBadge, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatNumber } from '../../lib/format';
import type { Gender, LeaveAccrual, LeaveType, SaveLeaveTypeRequest } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

export function LeaveTypesTab() {
  const list = useLeaveTypes();
  const [editing, setEditing] = useState<LeaveType | 'new' | null>(null);
  const [deleting, setDeleting] = useState<LeaveType | null>(null);
  const [reasonsFor, setReasonsFor] = useState<LeaveType | null>(null);

  return (
    <Card>
      <CardHeader
        title="Leave types"
        description="Annual quota changes apply to balances created from now on. Only unused types can be deleted; mark used ones inactive."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add leave type
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={4} cols={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState icon={Palmtree} title="No leave types" action={<Button variant="secondary" onClick={() => setEditing('new')}>Add leave type</Button>} />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Name</Th>
              <Th>Code</Th>
              <Th className="text-right">Annual quota</Th>
              <Th>Pay</Th>
              <Th>Policy</Th>
              <Th>Status</Th>
              <Th className="text-right">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((t) => (
              <Tr key={t.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">{t.name}</Td>
                <Td className="font-mono text-xs">{t.code}</Td>
                <Td className="text-right tabular-nums">{t.isCompensatory ? 'From comp-off' : `${formatNumber(t.annualQuota)} days`}</Td>
                <Td>{t.isPaid ? <Badge tone="green">Paid</Badge> : <Badge tone="gray">Unpaid</Badge>}</Td>
                <Td className="max-w-xs whitespace-normal text-xs text-zinc-500">{policySummary(t) || '—'}</Td>
                <Td>
                  <StatusBadge status={t.isActive ? 'Active' : 'Cancelled'} label={t.isActive ? 'Active' : 'Inactive'} />
                </Td>
                <Td>
                  <div className="flex items-center justify-end gap-1">
                    <Button size="sm" variant="ghost" onClick={() => setReasonsFor(t)}>
                      Reasons
                    </Button>
                    <RowActions history={{ type: 'LeaveType', id: t.id }} label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
                  </div>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {reasonsFor && <LeaveReasonsModal type={reasonsFor} onClose={() => setReasonsFor(null)} />}
      {editing && <LeaveTypeModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {/* A used leave type can't be deleted: the 400 message (mark it inactive instead) is shown as a toast. */}
      <DeleteConfirm
        item={deleting}
        name="Leave type"
        remove={setupApi.deleteLeaveType}
        invalidate={[qk.leaveTypes, qk.leave]}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function policySummary(t: LeaveType): string {
  const parts: string[] = [];
  if (t.isCompensatory) parts.push('Comp-off credits');
  if (t.accrual === 'Monthly') parts.push('Earned monthly');
  if (t.carryForwardMax > 0) parts.push(`Carry forward up to ${formatNumber(t.carryForwardMax)}`);
  if (t.maxConsecutiveDays) parts.push(`Max ${t.maxConsecutiveDays} days at a time`);
  if (t.minNoticeDays > 0) parts.push(`${t.minNoticeDays} days notice`);
  if (t.applicableGender) parts.push(`${t.applicableGender} only`);
  if (!t.allowedOnProbation) parts.push('After probation');
  if (!t.allowHalfDay) parts.push('No half days');
  if (t.allowShortLeave) parts.push('Short leave');
  if (t.sandwich) parts.push('Sandwich');
  if (t.documentAfterDays) parts.push(`Document after ${t.documentAfterDays} days`);
  return parts.join(' · ');
}

type Form = {
  name: string;
  code: string;
  annualQuota: string;
  isPaid: boolean;
  isActive: boolean;
  accrual: LeaveAccrual;
  carryForwardMax: string;
  maxConsecutiveDays: string;
  minNoticeDays: string;
  applicableGender: Gender | '';
  allowedOnProbation: boolean;
  allowHalfDay: boolean;
  allowShortLeave: boolean;
  isCompensatory: boolean;
  sandwich: boolean;
  documentAfterDays: string;
};

function toForm(t: LeaveType | null): Form {
  return t
    ? {
        name: t.name,
        code: t.code,
        annualQuota: String(t.annualQuota),
        isPaid: t.isPaid,
        isActive: t.isActive,
        accrual: t.accrual,
        carryForwardMax: String(t.carryForwardMax),
        maxConsecutiveDays: t.maxConsecutiveDays ? String(t.maxConsecutiveDays) : '',
        minNoticeDays: String(t.minNoticeDays),
        applicableGender: t.applicableGender ?? '',
        allowedOnProbation: t.allowedOnProbation,
        allowHalfDay: t.allowHalfDay,
        allowShortLeave: t.allowShortLeave,
        isCompensatory: t.isCompensatory,
        sandwich: t.sandwich,
        documentAfterDays: t.documentAfterDays != null ? String(t.documentAfterDays) : '',
      }
    : {
        name: '', code: '', annualQuota: '10', isPaid: true, isActive: true, accrual: 'Upfront', carryForwardMax: '0', maxConsecutiveDays: '',
        minNoticeDays: '0', applicableGender: '', allowedOnProbation: true, allowHalfDay: true, allowShortLeave: false, isCompensatory: false, sandwich: false, documentAfterDays: '',
      };
}

function LeaveTypeModal({ item, onClose }: { item: LeaveType | null; onClose: () => void }) {
  const [v, setV] = useState<Form>(toForm(item));
  const set = (patch: Partial<Form>) => setV((x) => ({ ...x, ...patch }));
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: SaveLeaveTypeRequest) => (item ? setupApi.updateLeaveType(item.id, b) : setupApi.createLeaveType(b)),
    invalidate: [qk.leaveTypes, qk.leave],
    successMessage: item ? 'Leave type updated' : 'Leave type added',
    onSuccess: onClose,
  });

  return (
    <EditModal
      open
      title={item ? 'Edit leave type' : 'Add leave type'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const q = Number(v.annualQuota);
        const cf = Number(v.carryForwardMax || 0);
        const max = v.maxConsecutiveDays ? Number(v.maxConsecutiveDays) : null;
        const notice = Number(v.minNoticeDays || 0);
        const fe: Record<string, string> = {};
        if (!v.name.trim()) fe.name = 'Name is required.';
        if (!v.code.trim()) fe.code = 'Code is required.';
        if (!v.isCompensatory && (v.annualQuota === '' || !Number.isFinite(q) || q < 0 || q > 365)) fe.annualQuota = 'Between 0 and 365 days.';
        if (!Number.isFinite(cf) || cf < 0 || cf > 365) fe.carryForwardMax = 'Between 0 and 365 days.';
        if (max !== null && (!Number.isInteger(max) || max < 1 || max > 365)) fe.maxConsecutiveDays = 'A whole number from 1 to 365, or empty.';
        if (!Number.isInteger(notice) || notice < 0 || notice > 90) fe.minNoticeDays = 'Between 0 and 90 days.';
        if (v.isCompensatory && !v.isPaid) fe.isCompensatory = 'Compensatory leave must be paid.';
        setFieldErrors(fe);
        if (Object.keys(fe).length) return;
        mutation.mutate({
          name: v.name.trim(),
          code: v.code.trim().toUpperCase(),
          annualQuota: v.isCompensatory ? 0 : q,
          isPaid: v.isPaid,
          isActive: v.isActive,
          accrual: v.accrual,
          carryForwardMax: cf,
          maxConsecutiveDays: max,
          minNoticeDays: notice,
          applicableGender: v.applicableGender || null,
          allowedOnProbation: v.allowedOnProbation,
          allowHalfDay: v.allowHalfDay,
          allowShortLeave: v.allowShortLeave,
          isCompensatory: v.isCompensatory,
          sandwich: v.sandwich,
          documentAfterDays: v.documentAfterDays ? Number(v.documentAfterDays) : null,
        });
      }}
    >
      <div className="grid grid-cols-[1fr_120px] gap-3">
        <Field label="Name" required error={fieldErrors.name}>
          <Input value={v.name} maxLength={100} onChange={(e) => set({ name: e.target.value })} placeholder="Annual leave" />
        </Field>
        <Field label="Code" required error={fieldErrors.code}>
          <Input value={v.code} maxLength={10} onChange={(e) => set({ code: e.target.value.toUpperCase() })} placeholder="AL" />
        </Field>
      </div>
      {!v.isCompensatory && (
        <div className="grid grid-cols-2 gap-3">
          <Field label="Annual quota (days)" required error={fieldErrors.annualQuota}>
            <Input type="number" min={0} max={365} step="0.5" value={v.annualQuota} onChange={(e) => set({ annualQuota: e.target.value })} />
          </Field>
          <Field label="How it is earned">
            <Select value={v.accrual} onChange={(e) => set({ accrual: e.target.value as LeaveAccrual })}>
              <option value="Upfront">All at the start of the year</option>
              <option value="Monthly">A twelfth every month</option>
            </Select>
          </Field>
        </div>
      )}
      <Checkbox label="Paid leave" description="Unpaid leave days are deducted in payroll." checked={v.isPaid} onChange={(e) => set({ isPaid: e.target.checked })} />
      <Checkbox
        label="Compensatory leave (comp-off)"
        description={fieldErrors.isCompensatory ?? 'No yearly quota; days come from approved comp-off claims for weekend or holiday work.'}
        checked={v.isCompensatory}
        onChange={(e) => set({ isCompensatory: e.target.checked })}
      />

      <fieldset className="space-y-3 rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
        <legend className="px-1 text-xs font-semibold tracking-wide text-zinc-500 uppercase">Policy</legend>
        <div className="grid grid-cols-3 gap-3">
          <Field label="Carry forward (max days)" hint="0 = none" error={fieldErrors.carryForwardMax}>
            <Input type="number" min={0} max={365} step="0.5" value={v.carryForwardMax} onChange={(e) => set({ carryForwardMax: e.target.value })} />
          </Field>
          <Field label="Max days at a time" hint="Empty = no limit" error={fieldErrors.maxConsecutiveDays}>
            <Input type="number" min={1} max={365} value={v.maxConsecutiveDays} onChange={(e) => set({ maxConsecutiveDays: e.target.value })} />
          </Field>
          <Field label="Notice (days ahead)" hint="0 = same day ok" error={fieldErrors.minNoticeDays}>
            <Input type="number" min={0} max={90} value={v.minNoticeDays} onChange={(e) => set({ minNoticeDays: e.target.value })} />
          </Field>
        </div>
        <Field label="Document needed for more than (days)" hint="Empty = never; the approver cannot approve until it is attached">
          <Input type="number" min={0.5} max={365} step="0.5" value={v.documentAfterDays} onChange={(e) => set({ documentAfterDays: e.target.value })} />
        </Field>
        <Field label="Who can take it">
          <Select value={v.applicableGender} onChange={(e) => set({ applicableGender: e.target.value as Gender | '' })}>
            <option value="">Everyone</option>
            <option value="Female">Female employees (e.g. maternity)</option>
            <option value="Male">Male employees (e.g. paternity)</option>
          </Select>
        </Field>
        <div className="grid gap-2 sm:grid-cols-2">
          <Checkbox label="Allowed during probation" checked={v.allowedOnProbation} onChange={(e) => set({ allowedOnProbation: e.target.checked })} />
          <Checkbox label="Half days allowed" checked={v.allowHalfDay} onChange={(e) => set({ allowHalfDay: e.target.checked })} />
          <Checkbox label="Short leave (hours) allowed" checked={v.allowShortLeave} onChange={(e) => set({ allowShortLeave: e.target.checked })} />
          <Checkbox
            label="Sandwich rule"
            description="Weekends and holidays between leave days count as leave"
            checked={v.sandwich}
            onChange={(e) => set({ sandwich: e.target.checked })}
          />
          <Checkbox label="Active" checked={v.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
        </div>
      </fieldset>
    </EditModal>
  );
}
