import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HandCoins } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { expenseSetupApi } from '../../api/expenseSetup';
import { OnBehalfField } from '../../components/engine';
import { Button, Card, CardHeader, Field, FormError, Input, Modal, StatusBadge, Table, TBody, Td, Tr, useToast } from '../../components/ui';
import { formatDateTimeUtc, formatNumber } from '../../lib/format';

const KEY = ['expense-setup'] as const;

/** "Ask for an advance" on My expenses. */
export function AdvanceButton() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button variant="secondary" icon={<HandCoins className="size-4" />} onClick={() => setOpen(true)}>
        Ask for advance
      </Button>
      {open && <AdvanceModal onClose={() => setOpen(false)} />}
    </>
  );
}

function AdvanceModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [purpose, setPurpose] = useState('');
  const [amount, setAmount] = useState('');
  const [forWhom, setForWhom] = useState('');
  const save = useMutation({
    mutationFn: () => expenseSetupApi.requestAdvance({ purpose: purpose.trim(), amount: Number(amount), employeeId: forWhom || null }),
    onSuccess: () => {
      toast.success('Sent for approval');
      qc.invalidateQueries({ queryKey: KEY });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Ask for an expense advance"
      description="Paid with your salary once approved. Set your claims against it later; what is not spent is recovered."
      footer={
        <Button loading={save.isPending} disabled={!purpose.trim() || !(Number(amount) > 0)} onClick={() => save.mutate()}>
          Send
        </Button>
      }
    >
      <div className="space-y-4">
        <OnBehalfField value={forWhom} onChange={setForWhom} />
        <Field label="What for" required>
          <Input value={purpose} maxLength={300} placeholder="Karachi client visit" onChange={(e) => setPurpose(e.target.value)} />
        </Field>
        <Field label="Amount (Rs)" required>
          <Input type="number" min={1} value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Field>
        <FormError message={save.isError ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}

/** The signed-in person's advances. */
export function MyAdvances() {
  const toast = useToast();
  const qc = useQueryClient();
  const mine = useQuery({ queryKey: [...KEY, 'my-advances'], queryFn: expenseSetupApi.myAdvances });
  const cancel = useMutation({
    mutationFn: expenseSetupApi.cancelAdvance,
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
    onError: (e) => toast.error(e),
  });
  if (!mine.data?.length) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="My advances" />
      <Table>
        <TBody>
          {mine.data.map((a) => (
            <Tr key={a.id}>
              <Td className="text-sm">{formatDateTimeUtc(a.createdAtUtc)}</Td>
              <Td>{a.purpose}</Td>
              <Td className="text-right tabular-nums">{formatNumber(a.amount)}</Td>
              <Td className="text-right text-sm text-zinc-500">
                {a.closedAtUtc
                  ? a.recovered
                    ? `${formatNumber(a.recovered)} recovered`
                    : 'Closed'
                  : a.status === 'Approved'
                    ? `${formatNumber(a.outstanding)} open`
                    : ''}
              </Td>
              <Td>
                <StatusBadge status={a.closedAtUtc ? 'Closed' : a.status} />
              </Td>
              <Td className="text-right">
                {a.status === 'Pending' && (
                  <Button size="sm" variant="ghost" onClick={() => cancel.mutate(a.id)}>
                    Cancel
                  </Button>
                )}
              </Td>
            </Tr>
          ))}
        </TBody>
      </Table>
    </Card>
  );
}
