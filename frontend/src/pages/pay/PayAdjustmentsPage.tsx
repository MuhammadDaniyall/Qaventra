import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, HandCoins, Plus, Wallet } from 'lucide-react';
import { qk } from '../../api/keys';
import { payApi, PAY_KIND_LABELS, type PayRequest } from '../../api/pay';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  IconButton,
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
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate, monthLabel } from '../../lib/format';
import { LoanModal, PayRequestModal } from './shared';

type TabValue = 'items' | 'loans';

/** HR: one-off pay items per payroll month, and all loans. */
export function PayAdjustmentsPage() {
  const [tab, setTab] = useState<TabValue>('items');
  const [adding, setAdding] = useState<'item' | 'loan' | null>(null);
  return (
    <>
      <PageHeader
        back={{ to: '/payroll', label: 'Payroll' }}
        title="Pay adjustments"
        description="Allowances, bonuses, deductions, overtime, advances and loans. Payroll picks them up automatically."
        actions={
          <>
            <Button variant="secondary" icon={<HandCoins className="size-4" />} onClick={() => setAdding('loan')}>
              Record loan
            </Button>
            <Button icon={<Plus className="size-4" />} onClick={() => setAdding('item')}>
              Add item
            </Button>
          </>
        }
      />
      <Tabs<TabValue>
        className="mb-4"
        value={tab}
        onChange={setTab}
        items={[
          { value: 'items', label: 'Monthly items' },
          { value: 'loans', label: 'Loans' },
        ]}
      />
      {tab === 'items' ? <MonthItems /> : <Loans />}
      {adding === 'item' && <PayRequestModal kind="Allowance" hr onClose={() => setAdding(null)} />}
      {adding === 'loan' && <LoanModal hr onClose={() => setAdding(null)} />}
    </>
  );
}

function MonthItems() {
  const toast = useToast();
  const qc = useQueryClient();
  const now = new Date();
  const [ym, setYm] = useState({ y: now.getFullYear(), m: now.getMonth() + 1 });
  const [removing, setRemoving] = useState<PayRequest | null>(null);
  const items = useQuery({ queryKey: qk.payMonth(ym.y, ym.m), queryFn: () => payApi.requests(ym.y, ym.m) });
  const remove = useMutation({
    mutationFn: (id: string) => payApi.cancel(id),
    onSuccess: () => {
      toast.success('Item removed');
      setRemoving(null);
      qc.invalidateQueries({ queryKey: qk.pay });
    },
    onError: (err) => toast.error(err),
  });
  const shift = (d: number) =>
    setYm(({ y, m }) => {
      const x = new Date(y, m - 1 + d, 1);
      return { y: x.getFullYear(), m: x.getMonth() + 1 };
    });
  const live = (items.data ?? []).filter((r) => r.status === 'Approved');
  const earnings = live.filter((r) => r.isEarning).reduce((t, r) => t + r.amount, 0);
  const deductions = live.filter((r) => !r.isEarning).reduce((t, r) => t + r.amount, 0);

  return (
    <Card>
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <div className="flex items-center gap-1">
          <IconButton label="Previous month" onClick={() => shift(-1)}>
            <ChevronLeft className="size-4" />
          </IconButton>
          <p className="min-w-36 text-center font-medium text-zinc-900 dark:text-white">{monthLabel(ym.y, ym.m)}</p>
          <IconButton label="Next month" onClick={() => shift(1)}>
            <ChevronRight className="size-4" />
          </IconButton>
        </div>
        <p className="text-sm text-zinc-500 dark:text-zinc-400">
          <span className="text-emerald-700 dark:text-emerald-300">+{formatCurrency(earnings)}</span> earnings ·{' '}
          <span className="text-zinc-900 dark:text-white">−{formatCurrency(deductions)}</span> deductions (approved)
        </p>
      </div>
      {items.isLoading ? (
        <SkeletonRows rows={5} cols={6} />
      ) : items.isError ? (
        <ErrorState error={items.error} onRetry={() => items.refetch()} />
      ) : !items.data?.length ? (
        <EmptyState
          icon={Wallet}
          title="Nothing for this month"
          description="Bonuses, allowances, deductions and approved overtime or advances for this payroll month appear here."
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Employee</Th>
              <Th>Item</Th>
              <Th className="text-end">Amount</Th>
              <Th>Status</Th>
              <Th>
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {items.data.map((r) => (
              <Tr key={r.id}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.employeeName}</p>
                  <p className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{r.employeeCode}</p>
                </Td>
                <Td>
                  <p className="text-zinc-900 dark:text-zinc-100">
                    {PAY_KIND_LABELS[r.kind]}
                    {r.kind === 'Overtime' ? ` · ${r.hours} h on ${formatDate(r.workDate)}` : r.title !== PAY_KIND_LABELS[r.kind] ? ` · ${r.title}` : ''}
                  </p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {r.isEarning ? (r.isTaxable ? 'Taxable' : 'Not taxable') : 'Deduction'}
                    {r.reason ? ` · ${r.reason}` : ''}
                  </p>
                </Td>
                <Td
                  className={r.isEarning ? 'text-end font-medium text-emerald-700 tabular-nums dark:text-emerald-300' : 'text-end font-medium tabular-nums'}
                >
                  {r.isEarning ? '+' : '−'}
                  {formatCurrency(r.amount)}
                </Td>
                <Td>
                  {r.paidAtUtc ? (
                    <Badge tone="violet">Paid</Badge>
                  ) : r.inDraftPayroll ? (
                    <Badge tone="blue">In draft payroll</Badge>
                  ) : (
                    <StatusBadge status={r.status} />
                  )}
                </Td>
                <Td className="text-end">
                  {!r.paidAtUtc && (r.status === 'Approved' || r.status === 'Pending') && (
                    <Button variant="ghost" size="sm" onClick={() => setRemoving(r)}>
                      Remove
                    </Button>
                  )}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      <ConfirmDialog
        open={!!removing}
        title="Remove this item?"
        message={
          removing && (
            <>
              {PAY_KIND_LABELS[removing.kind]} of {formatCurrency(removing.amount)} for {removing.employeeName} will not be paid. Reprocess a draft payroll to
              update it.
            </>
          )
        }
        confirmLabel="Remove"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing.id)}
        onClose={() => setRemoving(null)}
      />
    </Card>
  );
}

function Loans() {
  const loans = useQuery({ queryKey: qk.payLoans, queryFn: payApi.loans });
  return (
    <Card>
      {loans.isLoading ? (
        <SkeletonRows rows={5} cols={6} />
      ) : loans.isError ? (
        <ErrorState error={loans.error} onRetry={() => loans.refetch()} />
      ) : !loans.data?.length ? (
        <EmptyState icon={HandCoins} title="No loans" description="Loans employees apply for (or you record) appear here with what is left to repay." />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Employee</Th>
              <Th>Loan</Th>
              <Th className="text-end">Amount</Th>
              <Th className="text-end">Installment</Th>
              <Th>Repaid</Th>
              <Th className="text-end">Left</Th>
              <Th>Status</Th>
            </tr>
          </THead>
          <TBody>
            {loans.data.map((l) => (
              <Tr key={l.id}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{l.employeeName}</p>
                  <p className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{l.employeeCode}</p>
                </Td>
                <Td>
                  {l.title}
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">from {monthLabel(l.startYear, l.startMonth)}</p>
                </Td>
                <Td className="text-end tabular-nums">{formatCurrency(l.amount)}</Td>
                <Td className="text-end tabular-nums">{formatCurrency(l.installmentAmount)}</Td>
                <Td>
                  <div className="flex items-center gap-2">
                    <div className="h-1.5 w-20 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" aria-hidden="true">
                      <div className="h-full rounded-full bg-emerald-500" style={{ width: `${l.amount ? Math.min(100, (l.repaid / l.amount) * 100) : 0}%` }} />
                    </div>
                    <span className="text-xs text-zinc-500 dark:text-zinc-400 tabular-nums">
                      {l.installmentsTaken}/{l.installments}
                    </span>
                  </div>
                </Td>
                <Td className="text-end font-medium tabular-nums">{formatCurrency(l.remaining)}</Td>
                <Td>
                  <StatusBadge status={l.status} label={l.status === 'Approved' && l.remaining <= 0 ? 'Repaid' : undefined} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}
