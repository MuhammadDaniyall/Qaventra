import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, Pencil } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { leaveApi } from '../../api/leave';
import {
  Button,
  Card,
  CardHeader,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatNumber } from '../../lib/format';
import type { LeaveBalance } from '../../types';

/** HR view of one employee's leave balances for a year, with manual adjustments. */
export function LeaveTab({ employeeId }: { employeeId: string }) {
  const [year, setYear] = useState(new Date().getFullYear());
  const [editing, setEditing] = useState<LeaveBalance | null>(null);
  const balances = useQuery({ queryKey: qk.employeeLeave(employeeId, year), queryFn: () => leaveApi.employeeBalances(employeeId, year) });

  return (
    <Card>
      <CardHeader
        title={`Leave balances ${year}`}
        description="Quota, carry forward, comp-off credits and HR adjustments. Adjust when policy or a special case needs it; the reason is kept."
        actions={
          <div className="flex items-center gap-1">
            <IconButton label="Previous year" onClick={() => setYear((y) => y - 1)}>
              <ChevronLeft className="size-4" />
            </IconButton>
            <IconButton label="Next year" onClick={() => setYear((y) => y + 1)} disabled={year >= new Date().getFullYear() + 1}>
              <ChevronRight className="size-4" />
            </IconButton>
          </div>
        }
      />
      {balances.isLoading ? (
        <SkeletonRows rows={4} cols={7} />
      ) : balances.isError ? (
        <ErrorState error={balances.error} onRetry={() => balances.refetch()} />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Leave type</Th>
              <Th className="text-end">Quota</Th>
              <Th className="text-end">Earned</Th>
              <Th className="text-end">Carried</Th>
              <Th className="text-end">Comp-off</Th>
              <Th className="text-end">Adjusted</Th>
              <Th className="text-end">Used</Th>
              <Th className="text-end">Pending</Th>
              <Th className="text-end">Available</Th>
              <Th>
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {balances.data?.map((b) => (
              <Tr key={b.leaveTypeId}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                  {b.leaveType}
                  {b.accrual === 'Monthly' && <span className="ms-1.5 text-xs text-zinc-500 dark:text-zinc-400">monthly</span>}
                </Td>
                <Td className="text-end tabular-nums">{b.isCompensatory ? '—' : formatNumber(b.entitled)}</Td>
                <Td className="text-end tabular-nums">{b.isCompensatory ? '—' : formatNumber(b.earned)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(b.carriedForward)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(b.credited)}</Td>
                <Td className="text-end tabular-nums" title={b.adjustmentNote ?? undefined}>
                  {b.adjustment > 0 ? '+' : ''}
                  {formatNumber(b.adjustment)}
                </Td>
                <Td className="text-end tabular-nums">{formatNumber(b.used)}</Td>
                <Td className="text-end tabular-nums">{formatNumber(b.pending)}</Td>
                <Td className="text-end font-semibold tabular-nums">{formatNumber(b.available)}</Td>
                <Td className="text-end">
                  <IconButton label={`Adjust ${b.leaveType}`} onClick={() => setEditing(b)}>
                    <Pencil className="size-4" />
                  </IconButton>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <AdjustModal employeeId={employeeId} year={year} balance={editing} onClose={() => setEditing(null)} />}
    </Card>
  );
}

function AdjustModal({ employeeId, year, balance, onClose }: { employeeId: string; year: number; balance: LeaveBalance; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [value, setValue] = useState(String(balance.adjustment));
  const [note, setNote] = useState(balance.adjustmentNote ?? '');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => leaveApi.adjust(employeeId, balance.leaveTypeId, year, Number(value), note.trim()),
    onSuccess: () => {
      toast.success('Balance adjusted', balance.leaveType);
      qc.invalidateQueries({ queryKey: qk.leave });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const n = Number(value);
  const newAvailable = balance.available - balance.adjustment + (Number.isFinite(n) ? n : 0);

  return (
    <Modal
      open
      size="sm"
      onClose={onClose}
      title={`Adjust ${balance.leaveType}`}
      description={`${year}. Positive adds days, negative removes them. Replaces the current adjustment.`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!Number.isFinite(n) || n < -365 || n > 365) return setError('Enter a number of days between -365 and 365.');
              if (!note.trim()) return setError('Give a reason; it is kept with the balance.');
              save.mutate();
            }}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <Field label="Adjustment (days)" hint={`Available becomes ${formatNumber(newAvailable)}`}>
          <Input type="number" step="0.5" value={value} onChange={(e) => setValue(e.target.value)} />
        </Field>
        <Field label="Reason" required>
          <Input value={note} maxLength={300} onChange={(e) => setNote(e.target.value)} placeholder="e.g. Extra sick days approved by CEO" />
        </Field>
      </div>
    </Modal>
  );
}
