import { useState } from 'react';
import { Palmtree, Plus } from 'lucide-react';
import { setupApi } from '../../api/organization';
import { useLeaveTypes } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Badge, Button, Card, CardHeader, Checkbox, EmptyState, ErrorState, Field, Input, SkeletonRows, StatusBadge, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatNumber } from '../../lib/format';
import type { LeaveType, SaveLeaveTypeRequest } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

export function LeaveTypesTab() {
  const list = useLeaveTypes();
  const [editing, setEditing] = useState<LeaveType | 'new' | null>(null);
  const [deleting, setDeleting] = useState<LeaveType | null>(null);

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
                <Td className="text-right tabular-nums">{formatNumber(t.annualQuota)} days</Td>
                <Td>{t.isPaid ? <Badge tone="green">Paid</Badge> : <Badge tone="gray">Unpaid</Badge>}</Td>
                <Td>
                  <StatusBadge status={t.isActive ? 'Active' : 'Cancelled'} label={t.isActive ? 'Active' : 'Inactive'} />
                </Td>
                <Td>
                  <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
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

function LeaveTypeModal({ item, onClose }: { item: LeaveType | null; onClose: () => void }) {
  const [v, setV] = useState<Omit<SaveLeaveTypeRequest, 'annualQuota'> & { annualQuota: string }>(
    item
      ? { name: item.name, code: item.code, annualQuota: String(item.annualQuota), isPaid: item.isPaid, isActive: item.isActive }
      : { name: '', code: '', annualQuota: '10', isPaid: true, isActive: true },
  );
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
        const fe: Record<string, string> = {};
        if (!v.name.trim()) fe.name = 'Name is required.';
        if (!v.code.trim()) fe.code = 'Code is required.';
        if (v.annualQuota === '' || !Number.isFinite(q) || q < 0 || q > 365) fe.annualQuota = 'Between 0 and 365 days.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length)
          mutation.mutate({ name: v.name.trim(), code: v.code.trim().toUpperCase(), annualQuota: q, isPaid: v.isPaid, isActive: v.isActive });
      }}
    >
      <div className="grid grid-cols-[1fr_120px] gap-3">
        <Field label="Name" required error={fieldErrors.name}>
          <Input value={v.name} maxLength={100} onChange={(e) => setV({ ...v, name: e.target.value })} placeholder="Annual leave" />
        </Field>
        <Field label="Code" required error={fieldErrors.code}>
          <Input value={v.code} maxLength={10} onChange={(e) => setV({ ...v, code: e.target.value.toUpperCase() })} placeholder="AL" />
        </Field>
      </div>
      <Field label="Annual quota (days)" required error={fieldErrors.annualQuota}>
        <Input type="number" min={0} max={365} step="0.5" value={v.annualQuota} onChange={(e) => setV({ ...v, annualQuota: e.target.value })} />
      </Field>
      <Checkbox label="Paid leave" description="Unpaid leave days are deducted in payroll." checked={v.isPaid} onChange={(e) => setV({ ...v, isPaid: e.target.checked })} />
      <Checkbox label="Active" description="Inactive types can't be applied for." checked={v.isActive} onChange={(e) => setV({ ...v, isActive: e.target.checked })} />
    </EditModal>
  );
}
