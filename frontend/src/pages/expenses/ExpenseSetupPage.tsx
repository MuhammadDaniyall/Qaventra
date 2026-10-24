import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Receipt, Trash2 } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { useLookups } from '../../api/employeeFile';
import { expenseSetupApi, type ExpenseType, type SaveExpenseType } from '../../api/expenseSetup';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  Field,
  IconButton,
  Input,
  PageHeader,
  Select,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  Tabs,
  useToast,
} from '../../components/ui';
import { formatNumber, humanize } from '../../lib/format';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from '../settings/shared';
import { EXPENSE_CATEGORIES } from './shared';

const KEY = ['expense-setup'] as const;
type Tab = 'types' | 'distances' | 'advances';

/** HR: expense types (rates, limits, grades), distances between cities, and advances. */
export function ExpenseSetupPage() {
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'types';
  return (
    <>
      <PageHeader
        title="Expense setup"
        description="Types people claim, rates and yearly limits, distances, and advances."
        back={{ to: '/expenses/approvals', label: 'Expenses' }}
      />
      <Tabs<Tab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams({ tab: t }, { replace: true })}
        items={[
          { value: 'types', label: 'Types and limits' },
          { value: 'distances', label: 'Distances' },
          { value: 'advances', label: 'Advances' },
        ]}
      />
      {tab === 'types' && <Types />}
      {tab === 'distances' && <Distances />}
      {tab === 'advances' && <Advances />}
    </>
  );
}

const CALC_LABEL = { Amount: 'Amount entered', PerUnit: 'Rate × units', Distance: 'Rate × distance' } as const;

function Types() {
  const list = useQuery({ queryKey: [...KEY, 'types'], queryFn: expenseSetupApi.types });
  const [editing, setEditing] = useState<ExpenseType | 'new' | null>(null);
  const [deleting, setDeleting] = useState<ExpenseType | null>(null);
  return (
    <Card>
      <CardHeader
        title="Expense types"
        description="Without types, people pick a category and enter amounts. With types, rates and limits are applied for them."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add type
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : !list.data?.length ? (
        <EmptyState icon={Receipt} title="No expense types" />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Type</Th>
              <Th>Worked out</Th>
              <Th className="text-end">Per item</Th>
              <Th className="text-end">A year</Th>
              <Th className="text-end">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((t) => (
              <Tr key={t.id}>
                <Td className="font-medium">
                  {t.name} <span className="text-xs font-normal text-zinc-500 dark:text-zinc-400">{humanize(t.category)}</span>
                  {!t.isActive && <Badge className="ms-2">Off</Badge>}
                  {t.receiptRequired && <Badge className="ms-2">Receipt</Badge>}
                </Td>
                <Td className="text-sm">
                  {CALC_LABEL[t.calculation]}
                  {t.rate != null && ` · Rs ${formatNumber(t.rate)}/${t.unit ?? 'unit'}`}
                  {t.gradeRates.length > 0 && <span className="text-xs text-zinc-500 dark:text-zinc-400"> · {t.gradeRates.length} grade rate(s)</span>}
                </Td>
                <Td className="text-end tabular-nums">{t.perClaimLimit != null ? formatNumber(t.perClaimLimit) : '—'}</Td>
                <Td className="text-end tabular-nums">{t.yearlyLimit != null ? formatNumber(t.yearlyLimit) : '—'}</Td>
                <Td>
                  <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <TypeModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Type" remove={expenseSetupApi.deleteType} invalidate={[KEY]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function TypeModal({ item, onClose }: { item: ExpenseType | null; onClose: () => void }) {
  const lookups = useLookups();
  const grades = (lookups.data ?? []).filter((l) => l.kind === 'Grade');
  const [v, setV] = useState<SaveExpenseType>(
    item
      ? (({ id: _id, ...rest }) => rest)(item)
      : {
          name: '',
          category: 'Travel',
          calculation: 'Amount',
          unit: null,
          rate: null,
          perClaimLimit: null,
          yearlyLimit: null,
          receiptRequired: false,
          gradeIds: [],
          isActive: true,
          gradeRates: [],
        },
  );
  const num = (s: string) => (s === '' ? null : Number(s));
  const set = (p: Partial<SaveExpenseType>) => setV((x) => ({ ...x, ...p }));
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: () => (item ? expenseSetupApi.updateType(item.id, v) : expenseSetupApi.addType(v)),
    invalidate: [KEY],
    successMessage: 'Type saved',
    onSuccess: onClose,
  });
  const gradeRate = (gradeId: string) => v.gradeRates.find((r) => r.gradeId === gradeId);
  const setGradeRate = (gradeId: string, p: Partial<{ rate: number | null; yearlyLimit: number | null }>) =>
    set({
      gradeRates: [...v.gradeRates.filter((r) => r.gradeId !== gradeId), { gradeId, rate: null, yearlyLimit: null, ...gradeRate(gradeId), ...p }],
    });
  return (
    <EditModal
      open
      title={item ? 'Edit expense type' : 'Add expense type'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const fe: Record<string, string> = {};
        if (!v.name.trim()) fe.name = 'Name is required.';
        if (v.calculation !== 'Amount' && !(v.rate && v.rate > 0)) fe.rate = 'Enter the rate.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate(undefined);
      }}
    >
      <div className="grid grid-cols-2 gap-3">
        <Field label="Name" required error={fieldErrors.name}>
          <Input value={v.name} maxLength={150} placeholder="Mileage, Hotel, Meals" onChange={(e) => set({ name: e.target.value })} />
        </Field>
        <Field label="Category (for reports)">
          <Select value={v.category} onChange={(e) => set({ category: e.target.value as SaveExpenseType['category'] })}>
            {EXPENSE_CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {humanize(c.value)}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Worked out by">
          <Select value={v.calculation} onChange={(e) => set({ calculation: e.target.value as SaveExpenseType['calculation'] })}>
            <option value="Amount">Amount entered</option>
            <option value="PerUnit">Rate × units (nights, meals, km driven)</option>
            <option value="Distance">Rate × distance between cities</option>
          </Select>
        </Field>
        {v.calculation !== 'Amount' && (
          <div className="grid grid-cols-2 gap-2">
            <Field label="Rate (Rs)" error={fieldErrors.rate}>
              <Input type="number" min={0} value={v.rate ?? ''} onChange={(e) => set({ rate: num(e.target.value) })} />
            </Field>
            <Field label="Per">
              <Input
                value={v.unit ?? ''}
                maxLength={30}
                placeholder={v.calculation === 'Distance' ? 'km' : 'night'}
                onChange={(e) => set({ unit: e.target.value || null })}
              />
            </Field>
          </div>
        )}
        <Field label="Limit per item (Rs)">
          <Input type="number" min={0} value={v.perClaimLimit ?? ''} onChange={(e) => set({ perClaimLimit: num(e.target.value) })} />
        </Field>
        <Field label="Limit a year (Rs)">
          <Input type="number" min={0} value={v.yearlyLimit ?? ''} onChange={(e) => set({ yearlyLimit: num(e.target.value) })} />
        </Field>
      </div>
      <Checkbox label="Receipt required" checked={v.receiptRequired} onChange={(e) => set({ receiptRequired: e.target.checked })} />
      <Checkbox label="In use" checked={v.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
      {grades.length > 0 && (
        <Field label="Grades" hint="Tick to open it only to some grades; give a grade its own rate or yearly limit.">
          <div className="space-y-2">
            {grades.map((g) => (
              <div key={g.id} className="grid grid-cols-[1fr_7rem_8rem] items-center gap-2">
                <Checkbox
                  label={g.name}
                  checked={v.gradeIds.includes(g.id)}
                  onChange={() => set({ gradeIds: v.gradeIds.includes(g.id) ? v.gradeIds.filter((x) => x !== g.id) : [...v.gradeIds, g.id] })}
                />
                <Input
                  type="number"
                  min={0}
                  placeholder="Rate"
                  aria-label={`Rate for ${g.name}`}
                  value={gradeRate(g.id)?.rate ?? ''}
                  onChange={(e) => setGradeRate(g.id, { rate: num(e.target.value) })}
                />
                <Input
                  type="number"
                  min={0}
                  placeholder="Yearly limit"
                  aria-label={`Yearly limit for ${g.name}`}
                  value={gradeRate(g.id)?.yearlyLimit ?? ''}
                  onChange={(e) => setGradeRate(g.id, { yearlyLimit: num(e.target.value) })}
                />
              </div>
            ))}
          </div>
        </Field>
      )}
    </EditModal>
  );
}

function Distances() {
  const toast = useToast();
  const qc = useQueryClient();
  const routes = useQuery({ queryKey: [...KEY, 'routes'], queryFn: expenseSetupApi.routes });
  const [f, setF] = useState({ fromCity: '', toCity: '', km: '' });
  const add = useMutation({
    mutationFn: () => expenseSetupApi.addRoute({ fromCity: f.fromCity.trim(), toCity: f.toCity.trim(), km: Number(f.km) }),
    onSuccess: () => {
      setF({ fromCity: '', toCity: '', km: '' });
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });
  const remove = useMutation({ mutationFn: expenseSetupApi.deleteRoute, onSuccess: () => qc.invalidateQueries({ queryKey: KEY }) });
  return (
    <Card>
      <CardHeader title="Distances between cities" description="Used by distance-based types; works both ways round." />
      <div className="grid gap-2 border-b border-zinc-100 p-4 sm:grid-cols-[1fr_1fr_8rem_auto] dark:border-zinc-800">
        <Input placeholder="From city" aria-label="From city" value={f.fromCity} onChange={(e) => setF({ ...f, fromCity: e.target.value })} />
        <Input placeholder="To city" aria-label="To city" value={f.toCity} onChange={(e) => setF({ ...f, toCity: e.target.value })} />
        <Input type="number" min={0.1} placeholder="Km" aria-label="Km" value={f.km} onChange={(e) => setF({ ...f, km: e.target.value })} />
        <Button disabled={!f.fromCity.trim() || !f.toCity.trim() || !(Number(f.km) > 0)} loading={add.isPending} onClick={() => add.mutate()}>
          Add
        </Button>
      </div>
      <Table>
        <TBody>
          {(routes.data ?? []).map((r) => (
            <Tr key={r.id}>
              <Td>
                {r.fromCity} ↔ {r.toCity}
              </Td>
              <Td className="text-end tabular-nums">{formatNumber(r.km)} km</Td>
              <Td className="text-end">
                <IconButton label="Delete" onClick={() => remove.mutate(r.id)}>
                  <Trash2 className="size-4" />
                </IconButton>
              </Td>
            </Tr>
          ))}
        </TBody>
      </Table>
    </Card>
  );
}

function Advances() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: [...KEY, 'advances'], queryFn: () => expenseSetupApi.advances() });
  const act = useMutation({
    mutationFn: ({ id, what }: { id: string; what: 'approve' | 'reject' | 'close' }) =>
      what === 'approve'
        ? expenseSetupApi.approveAdvance(id, null)
        : what === 'reject'
          ? expenseSetupApi.rejectAdvance(id, null)
          : expenseSetupApi.closeAdvance(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
    onError: (e) => toast.error(e),
  });
  if (list.isLoading) return <SkeletonRows rows={4} cols={5} />;
  if (!list.data?.length) return <EmptyState icon={Receipt} title="No advances" />;
  return (
    <Card>
      <CardHeader
        title="Expense advances"
        description="Approved advances are paid with salary. Claims set against them are not paid again; closing recovers what was not spent."
      />
      <Table>
        <THead>
          <tr>
            <Th>Employee</Th>
            <Th>Purpose</Th>
            <Th className="text-end">Amount</Th>
            <Th className="text-end">Claimed</Th>
            <Th className="text-end">Open</Th>
            <Th>Status</Th>
            <Th />
          </tr>
        </THead>
        <TBody>
          {list.data.map((a) => (
            <Tr key={a.id}>
              <Td>
                {a.employeeName} <span className="text-xs text-zinc-500 dark:text-zinc-400">{a.employeeCode}</span>
              </Td>
              <Td className="text-sm">{a.purpose}</Td>
              <Td className="text-end tabular-nums">{formatNumber(a.amount)}</Td>
              <Td className="text-end tabular-nums">{formatNumber(a.adjusted)}</Td>
              <Td className="text-end tabular-nums">{a.closedAtUtc ? `recovered ${formatNumber(a.recovered)}` : formatNumber(a.outstanding)}</Td>
              <Td>
                <StatusBadge status={a.closedAtUtc ? 'Closed' : a.status} />
              </Td>
              <Td className="text-end">
                {a.status === 'Pending' && (
                  <div className="flex justify-end gap-1">
                    <Button size="sm" variant="ghost" onClick={() => act.mutate({ id: a.id, what: 'reject' })}>
                      Reject
                    </Button>
                    <Button size="sm" onClick={() => act.mutate({ id: a.id, what: 'approve' })}>
                      Approve
                    </Button>
                  </div>
                )}
                {a.status === 'Approved' && !a.closedAtUtc && (
                  <Button size="sm" variant="secondary" onClick={() => act.mutate({ id: a.id, what: 'close' })}>
                    Close
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
