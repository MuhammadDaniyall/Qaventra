import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Receipt } from 'lucide-react';
import { expensesApi } from '../../api/expenses';
import { qk } from '../../api/keys';
import {
  Avatar,
  Card,
  EmptyState,
  ErrorState,
  PageHeader,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Tabs,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { formatCurrency, formatDateTimeUtc } from '../../lib/format';
import type { ExpenseStatus } from '../../types/workflows';
import { ClaimDrawer } from './shared';

type TabValue = Extract<ExpenseStatus, 'Pending' | 'Approved' | 'Paid' | 'Rejected'>;

export function ExpenseApprovalsPage() {
  const [status, setStatus] = useState<TabValue>('Pending');
  const [openId, setOpenId] = useState<string | null>(null);
  const list = useQuery({ queryKey: qk.expenseApprovals(status), queryFn: () => expensesApi.approvals(status) });
  const pending = useQuery({ queryKey: qk.expenseApprovals('Pending'), queryFn: () => expensesApi.approvals('Pending') });
  const pendingTotal = (pending.data ?? []).reduce((t, c) => t + c.total, 0);

  return (
    <>
      <PageHeader
        title="Expense approvals"
        description={
          pending.data?.length
            ? `${pending.data.length} claim${pending.data.length === 1 ? '' : 's'} worth ${formatCurrency(pendingTotal)} waiting for you.`
            : 'Review expense claims from your team. Approved claims are paid with the next payroll.'
        }
      />
      <Tabs<TabValue>
        className="mb-4"
        value={status}
        onChange={setStatus}
        items={[
          {
            value: 'Pending',
            label: (
              <>
                Pending
                {!!pending.data?.length && (
                  <span className="ml-1 rounded-full bg-amber-100 px-1.5 text-xs text-amber-800 dark:bg-amber-500/20 dark:text-amber-200">
                    {pending.data.length}
                  </span>
                )}
              </>
            ),
          },
          { value: 'Approved', label: 'Approved, unpaid' },
          { value: 'Paid', label: 'Paid' },
          { value: 'Rejected', label: 'Rejected' },
        ]}
      />
      <Card>
        {list.isLoading ? (
          <SkeletonRows rows={5} cols={5} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !list.data?.length ? (
          <EmptyState
            icon={Receipt}
            title={status === 'Pending' ? 'All caught up' : `No ${status === 'Approved' ? 'unpaid approved' : status.toLowerCase()} claims`}
            description={status === 'Pending' ? 'There are no expense claims waiting for you.' : undefined}
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Claim</Th>
                <Th>Submitted</Th>
                <Th className="text-right">Amount</Th>
                <Th>{status === 'Paid' ? 'Paid via' : 'Status'}</Th>
              </tr>
            </THead>
            <TBody>
              {list.data.map((c) => (
                <Tr key={c.id} className="cursor-pointer" onClick={() => setOpenId(c.id)}>
                  <Td>
                    <div className="flex items-center gap-3">
                      <Avatar name={c.employeeName} size="sm" />
                      <div>
                        <p className="font-medium text-zinc-900 dark:text-zinc-100">{c.employeeName}</p>
                        <p className="font-mono text-xs text-zinc-500">{c.employeeCode}</p>
                      </div>
                    </div>
                  </Td>
                  <Td>
                    <p className="text-zinc-900 dark:text-zinc-100">{c.title}</p>
                    <p className="text-xs text-zinc-500">
                      {c.itemCount} item{c.itemCount === 1 ? '' : 's'} · {c.receiptCount} receipt{c.receiptCount === 1 ? '' : 's'}
                    </p>
                  </Td>
                  <Td>{formatDateTimeUtc(c.createdAtUtc)}</Td>
                  <Td className="text-right font-medium tabular-nums">{formatCurrency(c.total)}</Td>
                  <Td>{status === 'Paid' ? c.paidVia : <StatusBadge status={c.status} />}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {openId && <ClaimDrawer claimId={openId} onClose={() => setOpenId(null)} />}
    </>
  );
}
