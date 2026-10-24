import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BedDouble, BookOpen, Car, Check, FileText, Fuel, HeartPulse, Paperclip, Receipt, Smartphone, Utensils, X, type LucideIcon } from 'lucide-react';
import { expensesApi } from '../../api/expenses';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { ApprovalTimeline } from '../../components/approvals/ApprovalTimeline';
import { Avatar, Button, Drawer, ErrorState, Field, SkeletonRows, StatusBadge, Textarea, useToast } from '../../components/ui';
import { formatCurrency, formatDate, formatDateTimeUtc, humanize } from '../../lib/format';
import type { ExpenseCategory, ExpenseItem } from '../../types/workflows';

export const EXPENSE_CATEGORIES: Array<{ value: ExpenseCategory; icon: LucideIcon }> = [
  { value: 'Travel', icon: Car },
  { value: 'Fuel', icon: Fuel },
  { value: 'Meals', icon: Utensils },
  { value: 'Accommodation', icon: BedDouble },
  { value: 'Medical', icon: HeartPulse },
  { value: 'Mobile', icon: Smartphone },
  { value: 'OfficeSupplies', icon: Paperclip },
  { value: 'Training', icon: BookOpen },
  { value: 'Other', icon: Receipt },
];

export function CategoryIcon({ category, className }: { category: ExpenseCategory; className?: string }) {
  const Icon = EXPENSE_CATEGORIES.find((c) => c.value === category)?.icon ?? Receipt;
  return <Icon className={className} aria-hidden="true" />;
}

/** Opens a receipt (fetched with the bearer token) in a new tab. */
function useOpenReceipt(claimId: string) {
  const toast = useToast();
  const [opening, setOpening] = useState<string | null>(null);
  const open = async (item: ExpenseItem) => {
    if (!item.receiptId) return;
    // Open the tab synchronously so pop-up blockers allow it, then point it at the file.
    const tab = window.open('', '_blank');
    setOpening(item.id);
    try {
      const blob = await expensesApi.receipt(claimId, item.receiptId);
      const url = URL.createObjectURL(blob);
      if (tab) tab.location.href = url;
      else window.location.href = url;
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (err) {
      tab?.close();
      toast.error(err);
    } finally {
      setOpening(null);
    }
  };
  return { open, opening };
}

/** Small image preview for receipts that are pictures. */
function ReceiptThumb({ claimId, item }: { claimId: string; item: ExpenseItem }) {
  const isImage = /\.(png|jpe?g|webp)$/i.test(item.receiptFileName ?? '');
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!isImage || !item.receiptId) return;
    let revoked = false;
    let objectUrl: string | null = null;
    expensesApi
      .receipt(claimId, item.receiptId)
      .then((b) => {
        if (revoked) return;
        objectUrl = URL.createObjectURL(b);
        setUrl(objectUrl);
      })
      .catch(() => undefined);
    return () => {
      revoked = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [claimId, item.receiptId, isImage]);

  if (!isImage) return <FileText className="size-5 text-zinc-500 dark:text-zinc-400" aria-hidden="true" />;
  return url ? <img src={url} alt="" className="size-full object-cover" /> : <div className="size-full animate-pulse bg-zinc-100 dark:bg-zinc-800" />;
}

export function ClaimDrawer({ claimId, onClose }: { claimId: string; onClose: () => void }) {
  const user = useUser();
  const toast = useToast();
  const qc = useQueryClient();
  const [note, setNote] = useState('');
  const detail = useQuery({ queryKey: qk.expense(claimId), queryFn: () => expensesApi.get(claimId) });
  const { open, opening } = useOpenReceipt(claimId);

  const done = (message: string) => {
    toast.success(message);
    qc.invalidateQueries({ queryKey: qk.expenses });
    qc.invalidateQueries({ queryKey: qk.approvals });
    onClose();
  };
  const decide = useMutation({
    mutationFn: (approve: boolean) => (approve ? expensesApi.approve : expensesApi.reject)(claimId, note.trim() || null),
    onSuccess: (_, approve) => done(approve ? 'Claim approved' : 'Claim rejected'),
    onError: (err) => toast.error(err),
  });
  const cancel = useMutation({
    mutationFn: () => expensesApi.cancel(claimId),
    onSuccess: () => done('Claim cancelled'),
    onError: (err) => toast.error(err),
  });

  const d = detail.data;
  const mine = d?.claim.employeeId === user.employeeId;

  return (
    <Drawer
      open
      onClose={onClose}
      width="lg"
      title={d?.claim.title ?? 'Expense claim'}
      description={d && `Submitted ${formatDateTimeUtc(d.claim.createdAtUtc)}`}
      footer={
        d?.canDecide ? (
          <>
            <Button
              variant="secondary"
              icon={<X className="size-4" />}
              loading={decide.isPending && decide.variables === false}
              onClick={() => decide.mutate(false)}
            >
              Reject
            </Button>
            <Button
              variant="success"
              icon={<Check className="size-4" />}
              loading={decide.isPending && decide.variables === true}
              onClick={() => decide.mutate(true)}
            >
              Approve {formatCurrency(d.claim.total)}
            </Button>
          </>
        ) : mine && d?.claim.status === 'Pending' ? (
          <Button variant="secondary" loading={cancel.isPending} onClick={() => cancel.mutate()}>
            Cancel claim
          </Button>
        ) : undefined
      }
    >
      {detail.isLoading ? (
        <SkeletonRows rows={4} cols={3} />
      ) : detail.isError || !d ? (
        <ErrorState error={detail.error} onRetry={() => detail.refetch()} />
      ) : (
        <div className="space-y-6">
          <div className="flex items-center justify-between gap-4 rounded-xl bg-zinc-50 p-4 dark:bg-zinc-800/50">
            <div className="flex min-w-0 items-center gap-3">
              <Avatar name={d.claim.employeeName} />
              <div className="min-w-0">
                <p className="truncate font-medium text-zinc-900 dark:text-white">{d.claim.employeeName}</p>
                <p className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{d.claim.employeeCode}</p>
              </div>
            </div>
            <div className="text-end">
              <p className="text-2xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">{formatCurrency(d.claim.total)}</p>
              <StatusBadge status={d.claim.status} />
            </div>
          </div>

          <ul className="divide-y divide-zinc-100 rounded-xl border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {d.items.map((item) => (
              <li key={item.id} className="flex items-center gap-3 p-3">
                <button
                  type="button"
                  disabled={!item.receiptId}
                  onClick={() => open(item)}
                  title={item.receiptId ? `Open ${item.receiptFileName}` : 'No receipt'}
                  className="flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-zinc-200 bg-zinc-50 enabled:hover:ring-2 enabled:hover:ring-accent-500/40 disabled:cursor-default dark:border-zinc-700 dark:bg-zinc-800"
                >
                  {item.receiptId ? (
                    opening === item.id ? (
                      <span className="text-[10px] text-zinc-500 dark:text-zinc-400">Opening…</span>
                    ) : (
                      <ReceiptThumb claimId={claimId} item={item} />
                    )
                  ) : (
                    <CategoryIcon category={item.category} className="size-5 text-zinc-500 dark:text-zinc-400" />
                  )}
                </button>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{item.description}</p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {humanize(item.category)} · {formatDate(item.date)}
                    {!item.receiptId && ' · no receipt'}
                  </p>
                </div>
                <p className="text-sm font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(item.amount)}</p>
              </li>
            ))}
          </ul>

          <section>
            <p className="mb-3 text-xs font-medium tracking-wide text-zinc-500 dark:text-zinc-400 uppercase">Approval</p>
            <ApprovalTimeline type="Expense" subjectId={claimId} />
          </section>

          {(d.claim.decisionNote || d.claim.decidedAtUtc) && (
            <div className="rounded-xl border border-zinc-200 p-4 text-sm dark:border-zinc-800">
              <p className="text-xs font-medium tracking-wide text-zinc-500 dark:text-zinc-400 uppercase">Decision</p>
              <p className="mt-1 text-zinc-700 dark:text-zinc-300">
                {humanize(d.claim.status)} on {formatDateTimeUtc(d.claim.decidedAtUtc)}
              </p>
              {d.claim.decisionNote && <p className="mt-1 text-zinc-600 dark:text-zinc-400">“{d.claim.decisionNote}”</p>}
            </div>
          )}
          {d.claim.paidVia && (
            <p className="rounded-xl bg-violet-50 p-4 text-sm text-violet-800 dark:bg-violet-500/10 dark:text-violet-200">
              Reimbursed through {d.claim.paidVia} on {formatDateTimeUtc(d.claim.paidAtUtc)}.
            </p>
          )}
          {d.claim.status === 'Approved' && !d.claim.paidVia && (
            <p className="rounded-xl bg-emerald-50 p-4 text-sm text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-200">
              Approved. It will be paid with the next payroll.
            </p>
          )}

          {d.canDecide && (
            <Field label="Note to employee" hint="Optional, shown with your decision">
              <Textarea value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
            </Field>
          )}
        </div>
      )}
    </Drawer>
  );
}
