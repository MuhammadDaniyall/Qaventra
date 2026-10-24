import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Plus, Scale } from 'lucide-react';
import { attendanceRulesApi, PENALTY_FLAGS, type PenaltyRule, type SavePenaltyRule } from '../../api/attendanceRules';
import { useLeaveTypes } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Badge, Button, Card, CardHeader, Checkbox, EmptyState, Field, Input, Select, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from '../settings/shared';

const flagLabel = (f: string) => PENALTY_FLAGS.find((x) => x.value === f)?.label.toLowerCase() ?? f;

/** Plain-language rule, e.g. "Every 3 late arrivals (after 2 free): ½ day of pay". */
export function describeRule(r: SavePenaltyRule, leaveName?: string) {
  const days = r.days === 0.5 ? '½ day' : `${r.days} day${r.days === 1 ? '' : 's'}`;
  const what = r.action === 'DeductPay' ? `${days} of pay` : `${days} of ${leaveName ?? 'leave'} (pay when leave runs out)`;
  const count = r.everyCount === 1 ? `Each ${flagLabel(r.flag).replace(/s$/, '')}` : `Every ${r.everyCount} ${flagLabel(r.flag)}`;
  const extras = [
    r.freeCount ? `after ${r.freeCount} free` : '',
    r.minMinutes ? `${r.minMinutes}+ minutes` : '',
    r.repeat ? '' : 'once a month at most',
  ].filter(Boolean);
  return `${count}${extras.length ? ` (${extras.join(', ')})` : ''}: ${what}`;
}

/** HR: rules that turn attendance flags into pay or leave deductions. */
export function PenaltyRulesCard() {
  const rules = useQuery({ queryKey: qk.penaltyRules, queryFn: attendanceRulesApi.rules });
  const leaveTypes = useLeaveTypes();
  const [editing, setEditing] = useState<PenaltyRule | 'new' | null>(null);
  const [deleting, setDeleting] = useState<PenaltyRule | null>(null);
  const leaveName = (id: string | null) => leaveTypes.data?.find((t) => t.id === id)?.name;
  return (
    <Card className="mt-5">
      <CardHeader
        title="Penalty rules"
        description="Count late arrivals, early leaves, half and short days, absences or missing check-outs in a month and deduct pay or leave. Pay penalties appear on the payslip; leave penalties come off the balance when the month is closed."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add rule
          </Button>
        }
      />
      {!rules.data?.length ? (
        <EmptyState icon={Scale} title="No penalty rules" description="Without rules, only the late and half-day settings under payroll apply." />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Rule</Th>
              <Th>What happens</Th>
              <Th className="text-end">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {rules.data.map((r) => (
              <Tr key={r.id}>
                <Td className="font-medium">
                  {r.name} {!r.isActive && <Badge className="ms-1">Off</Badge>}
                </Td>
                <Td className="text-sm text-zinc-600 dark:text-zinc-300">{describeRule(r, leaveName(r.leaveTypeId))}</Td>
                <Td>
                  <RowActions label={r.name} onEdit={() => setEditing(r)} onDelete={() => setDeleting(r)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <RuleModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Rule" remove={attendanceRulesApi.deleteRule} invalidate={[qk.penaltyRules]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function RuleModal({ item, onClose }: { item: PenaltyRule | null; onClose: () => void }) {
  const leaveTypes = useLeaveTypes();
  const [v, setV] = useState<SavePenaltyRule>(
    item ?? {
      name: '',
      flag: 'Late',
      everyCount: 3,
      freeCount: 0,
      minMinutes: 0,
      repeat: true,
      action: 'DeductPay',
      days: 0.5,
      leaveTypeId: null,
      isActive: true,
    },
  );
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: SavePenaltyRule) => (item ? attendanceRulesApi.updateRule(item.id, b) : attendanceRulesApi.addRule(b)),
    invalidate: [qk.penaltyRules],
    successMessage: item ? 'Rule saved' : 'Rule added',
    onSuccess: onClose,
  });
  const set = (p: Partial<SavePenaltyRule>) => setV((x) => ({ ...x, ...p }));
  return (
    <EditModal
      open
      title={item ? 'Edit penalty rule' : 'Add penalty rule'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const fe: Record<string, string> = {};
        if (!v.name.trim()) fe.name = 'Name is required.';
        if (v.action === 'DeductLeave' && !v.leaveTypeId) fe.leaveTypeId = 'Choose the leave type.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate({ ...v, name: v.name.trim() });
      }}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={v.name} maxLength={150} placeholder="3 lates = half day" onChange={(e) => set({ name: e.target.value })} />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Count">
          <Select value={v.flag} onChange={(e) => set({ flag: e.target.value as SavePenaltyRule['flag'] })}>
            {PENALTY_FLAGS.map((f) => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Every" hint="occurrences make one penalty">
          <Input type="number" min={1} max={31} value={v.everyCount} onChange={(e) => set({ everyCount: Number(e.target.value) || 1 })} />
        </Field>
        <Field label="Free each month" hint="forgiven before counting">
          <Input type="number" min={0} max={31} value={v.freeCount} onChange={(e) => set({ freeCount: Number(e.target.value) || 0 })} />
        </Field>
        {(v.flag === 'Late' || v.flag === 'EarlyLeave') && (
          <Field label="Only when at least" hint="minutes late / early (0 = any)">
            <Input type="number" min={0} max={600} value={v.minMinutes} onChange={(e) => set({ minMinutes: Number(e.target.value) || 0 })} />
          </Field>
        )}
      </div>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Deduct">
          <Select value={v.action} onChange={(e) => set({ action: e.target.value as SavePenaltyRule['action'] })}>
            <option value="DeductPay">Pay</option>
            <option value="DeductLeave">Leave</option>
          </Select>
        </Field>
        <Field label="Days per penalty">
          <Input type="number" min={0.25} max={31} step={0.25} value={v.days} onChange={(e) => set({ days: Number(e.target.value) || 0.5 })} />
        </Field>
      </div>
      {v.action === 'DeductLeave' && (
        <Field label="From leave type" required error={fieldErrors.leaveTypeId} hint="When the balance runs out, the rest comes off pay.">
          <Select value={v.leaveTypeId ?? ''} onChange={(e) => set({ leaveTypeId: e.target.value || null })}>
            <option value="">Choose…</option>
            {(leaveTypes.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <Checkbox label="Repeat (a penalty for every group, not just once a month)" checked={v.repeat} onChange={(e) => set({ repeat: e.target.checked })} />
      <Checkbox label="In use" checked={v.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
      <p className="rounded-lg bg-zinc-50 px-3 py-2 text-sm text-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-300">{describeRule(v)}</p>
    </EditModal>
  );
}
