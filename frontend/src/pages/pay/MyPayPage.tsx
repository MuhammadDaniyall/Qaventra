import { useState } from 'react';
import { MyFundsCard } from '../payroll/FundsPage';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, Clock3, HandCoins } from 'lucide-react';
import { qk } from '../../api/keys';
import { payApi, PAY_KIND_LABELS, type PayRequest } from '../../api/pay';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { Badge, Button, Card, CardHeader, ConfirmDialog, EmptyState, ErrorState, PageHeader, SkeletonRows, StatusBadge, useToast } from '../../components/ui';
import { formatCurrency, formatDate, monthLabel } from '../../lib/format';
import { LoanModal, PayRequestModal } from './shared';

export function MyPayPage() {
  const toast = useToast();
  const qc = useQueryClient();
  const [modal, setModal] = useState<'AdvanceSalary' | 'Overtime' | 'Loan' | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState<{ id: string; loan: boolean } | null>(null);
  const requests = useQuery({ queryKey: qk.payMy, queryFn: payApi.myRequests });
  const loans = useQuery({ queryKey: qk.payLoansMy, queryFn: payApi.myLoans });
  const cancel = useMutation({
    mutationFn: (c: { id: string; loan: boolean }) => (c.loan ? payApi.cancelLoan(c.id) : payApi.cancel(c.id)),
    onSuccess: () => {
      toast.success('Request cancelled');
      setCancelling(null);
      qc.invalidateQueries({ queryKey: qk.pay });
      qc.invalidateQueries({ queryKey: qk.approvals });
    },
    onError: (err) => toast.error(err),
  });

  const paidLabel = (r: PayRequest) =>
    r.paidAtUtc ? <Badge tone="violet">Paid</Badge> : r.inDraftPayroll ? <Badge tone="blue">In draft payroll</Badge> : <StatusBadge status={r.status} />;

  return (
    <>
      <PageHeader
        title="Pay requests"
        description="Advance salary, overtime and loans. Approved amounts are paid or deducted through payroll."
        actions={
          <>
            <Button variant="secondary" icon={<Clock3 className="size-4" />} onClick={() => setModal('Overtime')}>
              Claim overtime
            </Button>
            <Button variant="secondary" icon={<HandCoins className="size-4" />} onClick={() => setModal('Loan')}>
              Apply for loan
            </Button>
            <Button icon={<Banknote className="size-4" />} onClick={() => setModal('AdvanceSalary')}>
              Advance salary
            </Button>
          </>
        }
      />

      {!!loans.data?.length && (
        <div className="mb-6 grid gap-4 md:grid-cols-2">
          {loans.data.map((l) => {
            const pct = l.amount > 0 ? Math.min(100, (l.repaid / l.amount) * 100) : 0;
            return (
              <Card key={l.id} className="p-4">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium text-zinc-900 dark:text-white">{l.title}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {formatCurrency(l.amount)} · {formatCurrency(l.installmentAmount)} × {l.installments} from {monthLabel(l.startYear, l.startMonth)}
                    </p>
                  </div>
                  <StatusBadge status={l.status} label={l.status === 'Approved' && l.remaining <= 0 ? 'Repaid' : undefined} />
                </div>
                {l.status === 'Approved' && (
                  <>
                    <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" aria-hidden="true">
                      <div className="h-full rounded-full bg-emerald-500" style={{ width: `${pct}%` }} />
                    </div>
                    <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">
                      {formatCurrency(l.repaid)} repaid ({l.installmentsTaken}/{l.installments}) · {formatCurrency(l.remaining)} left
                    </p>
                  </>
                )}
                {l.status === 'Pending' && (
                  <Button variant="ghost" size="sm" className="mt-2 -ms-2" onClick={() => setCancelling({ id: l.id, loan: true })}>
                    Cancel request
                  </Button>
                )}
              </Card>
            );
          })}
        </div>
      )}

      <MyFundsCard />
      <Card>
        <CardHeader title="My requests" />
        {requests.isLoading ? (
          <SkeletonRows rows={4} cols={4} />
        ) : requests.isError ? (
          <ErrorState error={requests.error} onRetry={() => requests.refetch()} />
        ) : !requests.data?.length ? (
          <EmptyState
            icon={Banknote}
            title="No pay requests yet"
            description="Ask for an advance, claim overtime or apply for a loan from the buttons above."
          />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {requests.data.map((r) => (
              <li key={r.id} className="px-5 py-3">
                <div className="flex flex-wrap items-center gap-3">
                  <button type="button" onClick={() => setOpen(open === r.id ? null : r.id)} className="min-w-0 flex-1 text-start" aria-expanded={open === r.id}>
                    <span className="block text-sm font-medium text-zinc-900 dark:text-white">
                      {r.kind === 'Overtime' ? `Overtime · ${r.hours} h on ${formatDate(r.workDate)}` : `${PAY_KIND_LABELS[r.kind]} · ${r.title}`}
                    </span>
                    <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                      {r.isEarning ? '' : 'Deducted from '}
                      {monthLabel(r.year, r.month)} payroll{r.reason ? ` · ${r.reason}` : ''}
                    </span>
                  </button>
                  <span
                    className={
                      r.isEarning
                        ? 'font-semibold text-emerald-700 tabular-nums dark:text-emerald-300'
                        : 'font-semibold text-zinc-900 tabular-nums dark:text-white'
                    }
                  >
                    {r.isEarning ? '+' : '−'}
                    {formatCurrency(r.amount)}
                  </span>
                  {paidLabel(r)}
                  {r.status === 'Pending' && (
                    <Button variant="ghost" size="sm" onClick={() => setCancelling({ id: r.id, loan: false })}>
                      Cancel
                    </Button>
                  )}
                </div>
                {open === r.id && (
                  <div className="mt-3">
                    <ApprovalTimeline type="PayRequest" subjectId={r.id} />
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </Card>

      {modal === 'Loan' && <LoanModal onClose={() => setModal(null)} />}
      {(modal === 'AdvanceSalary' || modal === 'Overtime') && <PayRequestModal kind={modal} onClose={() => setModal(null)} />}
      <ConfirmDialog
        open={!!cancelling}
        title="Cancel this request?"
        message="Your approver will no longer see it."
        confirmLabel="Cancel request"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling)}
        onClose={() => setCancelling(null)}
      />
    </>
  );
}
