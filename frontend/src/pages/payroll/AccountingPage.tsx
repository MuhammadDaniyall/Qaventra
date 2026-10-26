import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Pencil, Plus, Trash2 } from 'lucide-react';
import { accountingApi, type GlAccount, type PayrollGlLine } from '../../api/accounting';
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  useToast,
} from '../../components/ui';
import { tx } from '../../lib/i18n';

const ACCOUNTS = ['payroll', 'gl', 'accounts'] as const;
const MAPPING = ['payroll', 'gl', 'mapping'] as const;

/** Chart of accounts and where each payroll amount posts, for the monthly journal voucher export. */
export function AccountingPage() {
  return (
    <>
      <PageHeader
        title="Accounting"
        description="Your chart of accounts and where each payroll amount posts. Each payroll run then exports a journal voucher (CSV) for your accounting software."
        back={{ to: '/payroll', label: 'Payroll' }}
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <AccountsCard />
        <MappingCard />
      </div>
    </>
  );
}

function AccountsCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ACCOUNTS, queryFn: accountingApi.accounts });
  const [editing, setEditing] = useState<GlAccount | 'new' | null>(null);
  const [removing, setRemoving] = useState<GlAccount | null>(null);
  const remove = useMutation({
    mutationFn: (a: GlAccount) => accountingApi.removeAccount(a.id),
    onSuccess: () => {
      setRemoving(null);
      qc.invalidateQueries({ queryKey: ACCOUNTS });
    },
    onError: (e) => toast.error(e),
  });

  return (
    <Card>
      <CardHeader
        title="Chart of accounts"
        description="Only the accounts payroll posts to"
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add account
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={4} cols={2} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState icon={BookOpen} title="No accounts yet" description="Add the salary, payable and tax accounts from your accounting software." className="py-8" />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data.map((a) => (
            <li key={a.id} className="flex items-center gap-3 px-5 py-2.5">
              <span className="w-20 shrink-0 font-mono text-sm text-zinc-500 dark:text-zinc-400" dir="ltr">
                {a.code}
              </span>
              <span className="min-w-0 flex-1 truncate text-sm text-zinc-900 dark:text-zinc-100">{a.name}</span>
              <IconButton label="Edit" onClick={() => setEditing(a)}>
                <Pencil className="size-4" />
              </IconButton>
              <IconButton label="Delete" onClick={() => setRemoving(a)}>
                <Trash2 className="size-4" />
              </IconButton>
            </li>
          ))}
        </ul>
      )}
      {editing && <AccountModal account={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!removing}
        title="Delete this account?"
        message="Accounts used in the payroll mapping can't be deleted."
        confirmLabel="Delete"
        loading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing)}
        onClose={() => setRemoving(null)}
      />
    </Card>
  );
}

function AccountModal({ account, onClose }: { account: GlAccount | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [code, setCode] = useState(account?.code ?? '');
  const [name, setName] = useState(account?.name ?? '');
  const save = useMutation({
    mutationFn: () => {
      const b = { code: code.trim(), name: name.trim() };
      return account ? accountingApi.updateAccount(account.id, b) : accountingApi.addAccount(b);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ACCOUNTS });
      qc.invalidateQueries({ queryKey: MAPPING });
      onClose();
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={account ? 'Edit account' : 'Add account'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!code.trim() || !name.trim()} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Code" required>
          <Input value={code} maxLength={50} onChange={(e) => setCode(e.target.value)} placeholder="5100" dir="ltr" />
        </Field>
        <Field label="Name" required>
          <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} placeholder="Salaries and wages" />
        </Field>
      </div>
    </Modal>
  );
}

function MappingCard() {
  const toast = useToast();
  const qc = useQueryClient();
  const accounts = useQuery({ queryKey: ACCOUNTS, queryFn: accountingApi.accounts });
  const mapping = useQuery({ queryKey: MAPPING, queryFn: accountingApi.mapping });
  const [draft, setDraft] = useState<Record<string, string>>({});
  useEffect(() => {
    if (mapping.data) setDraft(Object.fromEntries(mapping.data.map((m) => [m.line, m.glAccountId ?? ''])));
  }, [mapping.data]);
  const save = useMutation({
    mutationFn: () =>
      accountingApi.saveMapping(Object.entries(draft).map(([line, id]) => ({ line: line as PayrollGlLine, glAccountId: id || null }))),
    onSuccess: (m) => {
      qc.setQueryData(MAPPING, m);
      toast.success('Mapping saved');
    },
    onError: (e) => toast.error(e),
  });

  return (
    <Card>
      <CardHeader
        title="Payroll mapping"
        description="Expenses are split by department in the voucher"
        actions={
          <Button size="sm" loading={save.isPending} disabled={!mapping.data} onClick={() => save.mutate()}>
            Save
          </Button>
        }
      />
      <CardBody className="space-y-3">
        {mapping.isLoading ? (
          <SkeletonRows rows={6} cols={2} />
        ) : mapping.isError ? (
          <ErrorState error={mapping.error} onRetry={() => mapping.refetch()} />
        ) : (
          mapping.data!.map((m) => (
            <Field key={m.line} label={m.label}>
              <Select value={draft[m.line] ?? ''} onChange={(e) => setDraft((d) => ({ ...d, [m.line]: e.target.value }))}>
                <option value="">{tx('Not mapped')}</option>
                {(accounts.data ?? []).map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.code} · {a.name}
                  </option>
                ))}
              </Select>
            </Field>
          ))
        )}
      </CardBody>
    </Card>
  );
}
