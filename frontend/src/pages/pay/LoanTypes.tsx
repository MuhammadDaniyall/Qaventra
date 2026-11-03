import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { payApi, type LoanType } from '../../api/pay';
import { Badge, Button, Card, CardHeader, Checkbox, Field, FormError, IconButton, Input, Modal, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatCurrency } from '../../lib/format';

/** HR: the kinds of loan offered, with limits, markup and fee. Once any exist, every loan request picks one. */
export function LoanTypesCard() {
  const types = useQuery({ queryKey: [...qk.pay, 'loan-types'], queryFn: payApi.loanTypes });
  const [editing, setEditing] = useState<LoanType | 'new' | null>(null);
  return (
    <Card className="mb-4">
      <CardHeader
        title="Loan types"
        description="Limits and charges per kind of loan. When there are types, every loan request picks one."
        actions={
          <Button size="sm" variant="secondary" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            Add type
          </Button>
        }
      />
      {!!types.data?.length && (
        <Table>
          <THead>
            <tr>
              <Th>Type</Th>
              <Th className="text-end">Up to</Th>
              <Th className="text-end">Installments</Th>
              <Th className="text-end">Markup</Th>
              <Th className="text-end">Fee</Th>
              <Th>After service</Th>
              <Th />
            </tr>
          </THead>
          <TBody>
            {types.data.map((t) => (
              <Tr key={t.id}>
                <Td className="font-medium">
                  {t.name} {!t.isActive && <Badge tone="gray">Off</Badge>}
                </Td>
                <Td className="text-end">{t.maxAmount ? formatCurrency(t.maxAmount) : '—'}</Td>
                <Td className="text-end">{t.maxInstallments ?? '—'}</Td>
                <Td className="text-end">{t.markupPercent ? `${t.markupPercent}%` : 'None'}</Td>
                <Td className="text-end">{t.processingFee ? formatCurrency(t.processingFee) : '—'}</Td>
                <Td>{t.minServiceMonths ? `${t.minServiceMonths} months` : 'Any'}</Td>
                <Td className="text-end">
                  <IconButton label={`Edit ${t.name}`} onClick={() => setEditing(t)}>
                    <Pencil className="size-4" />
                  </IconButton>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <LoanTypeModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </Card>
  );
}

function LoanTypeModal({ item, onClose }: { item: LoanType | null; onClose: () => void }) {
  const qc = useQueryClient();
  const [f, setF] = useState({
    name: item?.name ?? '',
    maxAmount: item?.maxAmount ? String(item.maxAmount) : '',
    maxInstallments: item?.maxInstallments ? String(item.maxInstallments) : '',
    markupPercent: String(item?.markupPercent ?? 0),
    processingFee: String(item?.processingFee ?? 0),
    minServiceMonths: String(item?.minServiceMonths ?? 0),
    isActive: item?.isActive ?? true,
  });
  const set = (k: keyof typeof f, v: string | boolean) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () =>
      payApi.saveLoanType(item?.id ?? null, {
        name: f.name.trim(),
        maxAmount: f.maxAmount ? Number(f.maxAmount) : null,
        maxInstallments: f.maxInstallments ? Number(f.maxInstallments) : null,
        markupPercent: Number(f.markupPercent) || 0,
        processingFee: Number(f.processingFee) || 0,
        minServiceMonths: Number(f.minServiceMonths) || 0,
        isActive: f.isActive,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [...qk.pay, 'loan-types'] });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? `Edit ${item.name}` : 'Add loan type'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.name.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-2 gap-3">
        <div className="col-span-2">
          <FormError message={save.isError ? errorMessage(save.error) : null} />
        </div>
        <Field label="Name" required className="col-span-2">
          <Input value={f.name} maxLength={100} onChange={(e) => set('name', e.target.value)} placeholder="Car loan" />
        </Field>
        <Field label="Up to (Rs)" hint="Empty = no limit">
          <Input type="number" min={1} value={f.maxAmount} onChange={(e) => set('maxAmount', e.target.value)} />
        </Field>
        <Field label="Installments at most" hint="Empty = up to 60">
          <Input type="number" min={1} max={60} value={f.maxInstallments} onChange={(e) => set('maxInstallments', e.target.value)} />
        </Field>
        <Field label="Markup %" hint="Added once to what is repaid">
          <Input type="number" min={0} max={100} step="0.5" value={f.markupPercent} onChange={(e) => set('markupPercent', e.target.value)} />
        </Field>
        <Field label="Processing fee (Rs)" hint="With the first installment">
          <Input type="number" min={0} value={f.processingFee} onChange={(e) => set('processingFee', e.target.value)} />
        </Field>
        <Field label="After months of service">
          <Input type="number" min={0} max={600} value={f.minServiceMonths} onChange={(e) => set('minServiceMonths', e.target.value)} />
        </Field>
        <div className="col-span-2">
          <Checkbox label="Offered" checked={f.isActive} onChange={(e) => set('isActive', e.target.checked)} />
        </div>
      </div>
    </Modal>
  );
}
