import { useMemo, useRef, useState, type FormEvent } from 'react';
import { OnBehalfField } from '../../components/engine';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Clock, Paperclip, Plus, Receipt, Trash2, Upload, Wallet } from 'lucide-react';
import { expensesApi } from '../../api/expenses';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import {
  Button,
  Card,
  CardHeader,
  Drawer,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  PageHeader,
  SkeletonRows,
  StatCard,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDateTimeUtc, humanize, todayIso } from '../../lib/format';
import type { ExpenseCategory, ReceiptUpload } from '../../types/workflows';
import { CategoryIcon, ClaimDrawer, EXPENSE_CATEGORIES } from './shared';

export function MyExpensesPage() {
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const claims = useQuery({ queryKey: qk.myExpenses, queryFn: expensesApi.my });

  const stats = useMemo(() => {
    const list = claims.data ?? [];
    const sum = (s: string) => list.filter((c) => c.status === s).reduce((t, c) => t + c.total, 0);
    const year = new Date().getFullYear();
    const paidThisYear = list
      .filter((c) => c.status === 'Paid' && c.paidAtUtc?.startsWith(String(year)))
      .reduce((t, c) => t + c.total, 0);
    return { pending: sum('Pending'), approved: sum('Approved'), paidThisYear };
  }, [claims.data]);

  return (
    <>
      <PageHeader
        title="My expenses"
        description="Claim back what you spent for work. Approved claims are paid with your salary."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
            New claim
          </Button>
        }
      />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard label="Waiting for approval" value={formatCurrency(stats.pending)} icon={Clock} tone="amber" loading={claims.isLoading} />
        <StatCard label="Approved, to be paid" value={formatCurrency(stats.approved)} icon={CheckCircle2} tone="green" loading={claims.isLoading} hint="Added to your next payslip" />
        <StatCard label={`Reimbursed in ${new Date().getFullYear()}`} value={formatCurrency(stats.paidThisYear)} icon={Wallet} tone="violet" loading={claims.isLoading} />
      </div>

      <Card className="mt-6">
        <CardHeader title="My claims" />
        {claims.isLoading ? (
          <SkeletonRows rows={4} cols={5} />
        ) : claims.isError ? (
          <ErrorState error={claims.error} onRetry={() => claims.refetch()} />
        ) : !claims.data?.length ? (
          <EmptyState
            icon={Receipt}
            title="No claims yet"
            description="Snap your receipts and submit them in one claim. Your manager approves, payroll pays."
            action={
              <Button variant="secondary" icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
                New claim
              </Button>
            }
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Claim</Th>
                <Th>Submitted</Th>
                <Th className="text-right">Amount</Th>
                <Th>Status</Th>
                <Th>Paid via</Th>
              </tr>
            </THead>
            <TBody>
              {claims.data.map((c) => (
                <Tr key={c.id} className="cursor-pointer" onClick={() => setOpenId(c.id)}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{c.title}</p>
                    <p className="text-xs text-zinc-500">
                      {c.itemCount} item{c.itemCount === 1 ? '' : 's'} · {c.receiptCount} receipt{c.receiptCount === 1 ? '' : 's'}
                    </p>
                  </Td>
                  <Td>{formatDateTimeUtc(c.createdAtUtc)}</Td>
                  <Td className="text-right font-medium tabular-nums">{formatCurrency(c.total)}</Td>
                  <Td>
                    <StatusBadge status={c.status} />
                  </Td>
                  <Td className="text-zinc-500">{c.paidVia ?? '—'}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {creating && <NewClaimDrawer onClose={() => setCreating(false)} />}
      {openId && <ClaimDrawer claimId={openId} onClose={() => setOpenId(null)} />}
    </>
  );
}

interface DraftItem {
  key: number;
  date: string;
  category: ExpenseCategory;
  description: string;
  amount: string;
  receipt: ReceiptUpload | null;
}

const MAX_RECEIPT = 3 * 1024 * 1024;
const ACCEPT = 'image/jpeg,image/png,image/webp,application/pdf';

function readFile(file: File): Promise<ReceiptUpload> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve({ fileName: file.name, contentType: file.type, base64: String(reader.result) });
    reader.onerror = () => reject(new Error('Could not read the file.'));
    reader.readAsDataURL(file);
  });
}

let nextKey = 1;
const blankItem = (): DraftItem => ({ key: nextKey++, date: todayIso(), category: 'Travel', description: '', amount: '', receipt: null });

function NewClaimDrawer({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [title, setTitle] = useState('');
  const [items, setItems] = useState<DraftItem[]>([blankItem()]);
  const [error, setError] = useState<string | null>(null);
  const [forWhom, setForWhom] = useState('');
  const total = items.reduce((t, i) => t + (Number(i.amount) || 0), 0);

  const update = (key: number, patch: Partial<DraftItem>) => setItems((list) => list.map((i) => (i.key === key ? { ...i, ...patch } : i)));

  const submit = useMutation({
    mutationFn: () =>
      expensesApi.submit({
        title: title.trim(),
        items: items.map((i) => ({
          date: i.date,
          category: i.category,
          description: i.description.trim(),
          amount: Number(i.amount),
          receipt: i.receipt,
        })),
        employeeId: forWhom || null,
      }),
    onSuccess: (c) => {
      toast.success('Claim submitted', `${formatCurrency(c.total)} sent for approval.`);
      qc.invalidateQueries({ queryKey: qk.expenses });
      qc.invalidateQueries({ queryKey: qk.approvals });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!title.trim()) return setError('Give the claim a short title, e.g. "Karachi client visit".');
    const bad = items.findIndex((i) => !i.description.trim() || !(Number(i.amount) > 0) || !i.date);
    if (bad >= 0) return setError(`Item ${bad + 1} needs a date, a description and an amount.`);
    if (items.some((i) => i.date > todayIso())) return setError('Expense dates cannot be in the future.');
    submit.mutate();
  };

  return (
    <Drawer
      open
      onClose={onClose}
      width="xl"
      title="New expense claim"
      description="Add one line per receipt. Photos and PDFs up to 3 MB."
      footer={
        <div className="flex w-full flex-wrap items-center justify-between gap-3">
          <p className="text-sm text-zinc-500">
            Total <span className="ml-1 text-lg font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(total)}</span>
          </p>
          <div className="flex gap-2">
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="new-claim-form" loading={submit.isPending}>
              Submit claim
            </Button>
          </div>
        </div>
      }
    >
      <form id="new-claim-form" onSubmit={onSubmit} className="space-y-5">
        <FormError message={error} />
        <OnBehalfField value={forWhom} onChange={setForWhom} />
        <Field label="Title" required>
          <Input value={title} maxLength={150} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Lahore client visit, October" autoFocus />
        </Field>

        <ol className="space-y-4">
          {items.map((item, index) => (
            <li key={item.key} className="rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
              <div className="mb-3 flex items-center justify-between">
                <p className="text-xs font-semibold tracking-wide text-zinc-500 uppercase">Item {index + 1}</p>
                {items.length > 1 && (
                  <IconButton label={`Remove item ${index + 1}`} onClick={() => setItems((l) => l.filter((i) => i.key !== item.key))}>
                    <Trash2 className="size-4" />
                  </IconButton>
                )}
              </div>

              <div className="mb-3 flex flex-wrap gap-1.5" role="radiogroup" aria-label="Category">
                {EXPENSE_CATEGORIES.map((c) => (
                  <button
                    key={c.value}
                    type="button"
                    role="radio"
                    aria-checked={item.category === c.value}
                    onClick={() => update(item.key, { category: c.value })}
                    className={cn(
                      'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium transition-colors',
                      item.category === c.value
                        ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/15 dark:text-accent-200'
                        : 'border-zinc-200 text-zinc-600 hover:border-zinc-300 dark:border-zinc-700 dark:text-zinc-300',
                    )}
                  >
                    <CategoryIcon category={c.value} className="size-3.5" />
                    {humanize(c.value)}
                  </button>
                ))}
              </div>

              <div className="grid gap-3 sm:grid-cols-[1fr_9rem_9rem]">
                <Field label="What was it for?" required>
                  <Input value={item.description} maxLength={500} onChange={(e) => update(item.key, { description: e.target.value })} placeholder="e.g. Careem to client office" />
                </Field>
                <Field label="Date" required>
                  <Input type="date" value={item.date} max={todayIso()} onChange={(e) => update(item.key, { date: e.target.value })} />
                </Field>
                <Field label="Amount (Rs)" required>
                  <Input type="number" inputMode="decimal" min="0" step="1" value={item.amount} onChange={(e) => update(item.key, { amount: e.target.value })} className="text-right tabular-nums" />
                </Field>
              </div>

              <ReceiptPicker
                receipt={item.receipt}
                onChange={(receipt) => update(item.key, { receipt })}
                onError={(m) => toast.error(m)}
              />
            </li>
          ))}
        </ol>

        <Button variant="secondary" icon={<Plus className="size-4" />} onClick={() => setItems((l) => [...l, blankItem()])} disabled={items.length >= 20}>
          Add another item
        </Button>
      </form>
    </Drawer>
  );
}

function ReceiptPicker({
  receipt,
  onChange,
  onError,
}: {
  receipt: ReceiptUpload | null;
  onChange: (r: ReceiptUpload | null) => void;
  onError: (message: string) => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  const pick = async (file: File | undefined) => {
    if (!file) return;
    if (!ACCEPT.split(',').includes(file.type)) return onError('Use a JPG, PNG, WebP or PDF file.');
    if (file.size > MAX_RECEIPT) return onError('Receipts must be smaller than 3 MB.');
    try {
      onChange(await readFile(file));
    } catch (err) {
      onError(errorMessage(err));
    }
  };

  if (receipt) {
    const isImage = receipt.contentType.startsWith('image/');
    return (
      <div className="mt-3 flex items-center gap-3 rounded-lg bg-zinc-50 p-2 dark:bg-zinc-800/50">
        {isImage ? (
          <img src={receipt.base64} alt="" className="size-10 rounded object-cover" />
        ) : (
          <Paperclip className="m-2.5 size-5 text-zinc-400" aria-hidden="true" />
        )}
        <p className="min-w-0 flex-1 truncate text-sm text-zinc-700 dark:text-zinc-300">{receipt.fileName}</p>
        <Button variant="ghost" size="sm" onClick={() => onChange(null)}>
          Remove
        </Button>
      </div>
    );
  }

  return (
    <>
      <button
        type="button"
        onClick={() => input.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          pick(e.dataTransfer.files[0]);
        }}
        className={cn(
          'mt-3 flex w-full items-center justify-center gap-2 rounded-lg border border-dashed px-3 py-3 text-sm transition-colors',
          dragging
            ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10'
            : 'border-zinc-300 text-zinc-500 hover:border-accent-400 hover:text-accent-700 dark:border-zinc-700 dark:text-zinc-400',
        )}
      >
        <Upload className="size-4" aria-hidden="true" />
        Attach receipt <span className="hidden sm:inline">(or drop it here)</span>
      </button>
      <input
        ref={input}
        type="file"
        accept={ACCEPT}
        className="sr-only"
        tabIndex={-1}
        onChange={(e) => {
          pick(e.target.files?.[0]);
          e.target.value = '';
        }}
      />
    </>
  );
}
