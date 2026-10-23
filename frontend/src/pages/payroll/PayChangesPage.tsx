import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { CalendarSync, Eye, HandCoins, PauseCircle, Plus, TrendingUp, Undo2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup, useLeaveTypes } from '../../api/hooks';
import { qk } from '../../api/keys';
import { payChangesApi, type RecurringItem, type RevisionKind, type RevisionPreview } from '../../api/paychanges';
import { performanceApi } from '../../api/performance';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  Field,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Tabs,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatDateTimeUtc, monthLabel } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';

type Tab = 'revise' | 'recurring' | 'holds' | 'encash' | 'history';

/** HR: changes to pay outside the monthly run. */
export function PayChangesPage() {
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'revise';
  return (
    <>
      <PageHeader
        title="Pay changes"
        description="Salary revisions, monthly allowances and deductions, holds and leave encashment."
        back={{ to: '/payroll', label: 'Payroll' }}
      />
      <Tabs<Tab>
        className="mb-5"
        value={tab}
        onChange={(t) => setParams({ tab: t })}
        items={[
          { value: 'revise', label: 'Salary revision' },
          { value: 'recurring', label: 'Monthly items' },
          { value: 'holds', label: 'Holds' },
          { value: 'encash', label: 'Leave encashment' },
          { value: 'history', label: 'Revision history' },
        ]}
      />
      {tab === 'revise' && <ReviseTab onDone={() => setParams({ tab: 'history' })} />}
      {tab === 'recurring' && <RecurringTab />}
      {tab === 'holds' && <HoldsTab />}
      {tab === 'encash' && <EncashTab />}
      {tab === 'history' && <HistoryTab />}
    </>
  );
}

// ---------- Salary revision ----------

type Row = { employeeId: string; mode: 'percent' | 'amount'; value: string };

function ReviseTab({ onDone }: { onDone: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const cycles = useQuery({ queryKey: qk.performanceCycles, queryFn: performanceApi.cycles });
  const now = new Date();
  const [effective, setEffective] = useState(new Date(now.getFullYear(), now.getMonth() + 1, 1).toLocaleDateString('en-CA'));
  const [kind, setKind] = useState<RevisionKind>('Increment');
  const [allowances, setAllowances] = useState(true);
  const [reason, setReason] = useState('');
  const [rows, setRows] = useState<Row[]>([]);
  const [adding, setAdding] = useState('');
  const [preview, setPreview] = useState<RevisionPreview[] | null>(null);
  const body = () => ({
    effectiveFrom: effective,
    kind,
    includeAllowances: allowances,
    reason: reason.trim() || null,
    lines: rows.map((r) => ({
      employeeId: r.employeeId,
      percent: r.mode === 'percent' ? Number(r.value) : null,
      amount: r.mode === 'amount' ? Number(r.value) : null,
    })),
  });
  const look = useMutation({ mutationFn: () => payChangesApi.preview(body()), onSuccess: setPreview, onError: (e) => toast.error(e) });
  const apply = useMutation({
    mutationFn: () => payChangesApi.revise(body()),
    onSuccess: (r) => {
      toast.success(`${r.revised} salaries revised`, 'Each person has been told.');
      qc.invalidateQueries({ queryKey: qk.payChanges });
      qc.invalidateQueries({ queryKey: qk.employees });
      onDone();
    },
    onError: (e) => toast.error(e),
  });
  const fromCycle = useMutation({
    mutationFn: async (cycleId: string) => (await performanceApi.calibration(cycleId)).reviews.filter((r) => r.suggestedRaise != null),
    onSuccess: (list) => {
      setRows((x) => {
        const map = new Map(x.map((r) => [r.employeeId, r]));
        for (const r of list) map.set(r.employeeId, { employeeId: r.employeeId, mode: 'percent', value: String(r.suggestedRaise) });
        return [...map.values()];
      });
      setPreview(null);
      toast.success(`${list.length} raises loaded from the review cycle`);
    },
    onError: (e) => toast.error(e),
  });
  const name = (id: string) => people.data?.find((p) => p.id === id)?.name ?? id;
  const ready = rows.length > 0 && rows.every((r) => r.value !== '' && Number.isFinite(Number(r.value)));
  const problems = preview?.filter((p) => p.problem).length ?? 0;
  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title="Who and how much" description="Percent applies to basic (and allowances if chosen); a fixed amount goes on the basic." />
        <div className="grid gap-4 px-5 py-4 sm:grid-cols-4">
          <Field label="Effective from">
            <Input type="date" value={effective} onChange={(e) => (setEffective(e.target.value), setPreview(null))} />
          </Field>
          <Field label="Kind">
            <Select value={kind} onChange={(e) => setKind(e.target.value as RevisionKind)}>
              {(['Increment', 'Promotion', 'Decrement', 'Correction'] as RevisionKind[]).map((k) => (
                <option key={k}>{k}</option>
              ))}
            </Select>
          </Field>
          <Field label="Reason" className="sm:col-span-2">
            <Input value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} placeholder="Annual review 2026" />
          </Field>
          <Checkbox
            className="sm:col-span-4"
            label="Raise allowances by the same percentage"
            checked={allowances}
            onChange={(e) => (setAllowances(e.target.checked), setPreview(null))}
          />
        </div>
        <div className="flex flex-wrap items-end gap-2 border-t border-zinc-100 px-5 py-3 dark:border-zinc-800">
          <Field label="Add a person">
            <Select className="w-72" value={adding} onChange={(e) => setAdding(e.target.value)}>
              <option value="">Choose…</option>
              {people.data
                ?.filter((p) => !rows.some((r) => r.employeeId === p.id))
                .map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
            </Select>
          </Field>
          <Button
            variant="secondary"
            icon={<Plus className="size-4" />}
            disabled={!adding}
            onClick={() => {
              setRows((x) => [...x, { employeeId: adding, mode: 'percent', value: '10' }]);
              setAdding('');
              setPreview(null);
            }}
          >
            Add
          </Button>
          <Button
            variant="ghost"
            onClick={() => {
              setRows((people.data ?? []).map((p) => ({ employeeId: p.id, mode: 'percent', value: '10' })));
              setPreview(null);
            }}
          >
            Everyone at 10%
          </Button>
          {!!cycles.data?.length && (
            <Field label="Or take raises from a review cycle">
              <Select className="w-64" value="" onChange={(e) => e.target.value && fromCycle.mutate(e.target.value)}>
                <option value="">Choose a cycle…</option>
                {cycles.data.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
        </div>
        {rows.length > 0 && (
          <Table>
            <THead>
              <Tr>
                <Th>Employee</Th>
                <Th className="w-40">Change</Th>
                <Th className="w-36">Value</Th>
                {preview && (
                  <>
                    <Th className="text-right">Gross now</Th>
                    <Th className="text-right">New gross</Th>
                    <Th className="text-right">Change</Th>
                  </>
                )}
                <Th />
              </Tr>
            </THead>
            <TBody>
              {rows.map((r, i) => {
                const p = preview?.find((x) => x.employeeId === r.employeeId);
                return (
                  <Tr key={r.employeeId}>
                    <Td>
                      {name(r.employeeId)}
                      {p?.problem && <p className="text-xs text-red-600">{p.problem}</p>}
                    </Td>
                    <Td>
                      <Select
                        aria-label="Change type"
                        value={r.mode}
                        onChange={(e) => (setRows((x) => x.map((y, j) => (j === i ? { ...y, mode: e.target.value as Row['mode'] } : y))), setPreview(null))}
                      >
                        <option value="percent">Percent</option>
                        <option value="amount">Amount (PKR)</option>
                      </Select>
                    </Td>
                    <Td>
                      <Input
                        aria-label="Value"
                        type="number"
                        step="any"
                        value={r.value}
                        onChange={(e) => (setRows((x) => x.map((y, j) => (j === i ? { ...y, value: e.target.value } : y))), setPreview(null))}
                      />
                    </Td>
                    {preview && (
                      <>
                        <Td className="text-right tabular-nums">{p ? formatCurrency(p.oldGross) : ''}</Td>
                        <Td className="text-right font-medium tabular-nums">{p ? formatCurrency(p.newGross) : ''}</Td>
                        <Td className={cn('text-right tabular-nums', p && p.changePercent < 0 && 'text-red-600')}>{p ? `${p.changePercent}%` : ''}</Td>
                      </>
                    )}
                    <Td>
                      <Button size="sm" variant="ghost" onClick={() => (setRows((x) => x.filter((_, j) => j !== i)), setPreview(null))}>
                        Remove
                      </Button>
                    </Td>
                  </Tr>
                );
              })}
            </TBody>
          </Table>
        )}
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-zinc-100 px-5 py-3 dark:border-zinc-800">
          {preview && (
            <p className="mr-auto text-sm text-zinc-600 dark:text-zinc-300">
              Monthly cost {formatCurrency(preview.reduce((a, p) => a + p.oldGross, 0))} → {formatCurrency(preview.reduce((a, p) => a + p.newGross, 0))}
              {problems > 0 && <span className="ml-2 text-red-600">{problems} to fix</span>}
            </p>
          )}
          <Button variant="secondary" icon={<Eye className="size-4" />} disabled={!ready} loading={look.isPending} onClick={() => look.mutate()}>
            Preview
          </Button>
          <Button icon={<TrendingUp className="size-4" />} disabled={!preview || problems > 0} loading={apply.isPending} onClick={() => apply.mutate()}>
            Apply {rows.length}
          </Button>
        </div>
      </Card>
    </div>
  );
}

// ---------- Monthly items ----------

function RecurringTab() {
  const items = useQuery({ queryKey: qk.recurringItems, queryFn: payChangesApi.recurring });
  const [editing, setEditing] = useState<RecurringItem | 'new' | null>(null);
  const [deleting, setDeleting] = useState<RecurringItem | null>(null);
  const period = (y: number, m: number) => monthLabel(y, m);
  return (
    <Card>
      <CardHeader
        title="Monthly allowances and deductions"
        description="Added to every payslip in their months: car allowance, mobile, canteen, union fee…"
        actions={
          <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            Add
          </Button>
        }
      />
      {items.isLoading ? (
        <SkeletonRows rows={4} cols={5} />
      ) : !items.data?.length ? (
        <EmptyState icon={CalendarSync} title="None yet" />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Employee</Th>
              <Th>Item</Th>
              <Th className="text-right">Amount</Th>
              <Th>Months</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {items.data.map((r) => (
              <Tr key={r.id}>
                <Td>
                  {r.name} <span className="text-xs text-zinc-500">{r.code}</span>
                </Td>
                <Td>
                  {r.title} <Badge tone={r.isEarning ? 'green' : 'red'}>{r.isEarning ? (r.isTaxable ? 'Earning, taxable' : 'Earning') : 'Deduction'}</Badge>
                  {!r.active && <Badge className="ml-1">Not this month</Badge>}
                </Td>
                <Td className="text-right tabular-nums">{formatCurrency(r.amount)}</Td>
                <Td className="text-sm">
                  {period(r.startYear, r.startMonth)} – {r.endYear && r.endMonth ? period(r.endYear, r.endMonth) : 'until stopped'}
                </Td>
                <Td>
                  <RowActions label={r.title} onEdit={() => setEditing(r)} onDelete={() => setDeleting(r)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <RecurringModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name={deleting?.title ?? ''}
        remove={payChangesApi.deleteRecurring}
        invalidate={[qk.payChanges]}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function RecurringModal({ item, onClose }: { item: RecurringItem | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const now = new Date();
  const ym = (y?: number | null, m?: number | null) => (y && m ? `${y}-${String(m).padStart(2, '0')}` : '');
  const [f, setF] = useState({
    employeeId: item?.employeeId ?? '',
    title: item?.title ?? '',
    earning: item?.isEarning ?? true,
    amount: item?.amount?.toString() ?? '',
    taxable: item?.isTaxable ?? true,
    start: ym(item?.startYear ?? now.getFullYear(), item?.startMonth ?? now.getMonth() + 1),
    end: ym(item?.endYear, item?.endMonth),
    notes: item?.notes ?? '',
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () => {
      const [sy, sm] = f.start.split('-').map(Number);
      const [ey, em] = f.end ? f.end.split('-').map(Number) : [null, null];
      const b = {
        employeeId: f.employeeId,
        title: f.title.trim(),
        isEarning: f.earning,
        amount: Number(f.amount),
        isTaxable: f.earning && f.taxable,
        startYear: sy,
        startMonth: sm,
        endYear: ey,
        endMonth: em,
        notes: f.notes.trim() || null,
      };
      return item ? payChangesApi.updateRecurring(item.id, b) : payChangesApi.addRecurring(b);
    },
    onSuccess: () => {
      toast.success('Saved');
      qc.invalidateQueries({ queryKey: qk.payChanges });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? 'Edit monthly item' : 'New monthly item'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.employeeId || !f.title.trim() || !(Number(f.amount) > 0) || !f.start} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Employee" required className="sm:col-span-2">
          <Select value={f.employeeId} onChange={(e) => set('employeeId', e.target.value)}>
            <option value="">Choose…</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Title" required>
          <Input value={f.title} maxLength={150} onChange={(e) => set('title', e.target.value)} placeholder="Car allowance" />
        </Field>
        <Field label="Amount per month (PKR)" required>
          <Input type="number" min={1} value={f.amount} onChange={(e) => set('amount', e.target.value)} />
        </Field>
        <Field label="Type">
          <Select value={f.earning ? 'e' : 'd'} onChange={(e) => set('earning', e.target.value === 'e')}>
            <option value="e">Earning</option>
            <option value="d">Deduction</option>
          </Select>
        </Field>
        {f.earning && (
          <div className="flex items-end pb-2">
            <Checkbox label="Taxable" checked={f.taxable} onChange={(e) => set('taxable', e.target.checked)} />
          </div>
        )}
        <Field label="From month">
          <Input type="month" value={f.start} onChange={(e) => set('start', e.target.value)} />
        </Field>
        <Field label="Until month" hint="Empty: until stopped">
          <Input type="month" min={f.start} value={f.end} onChange={(e) => set('end', e.target.value)} />
        </Field>
        <Field label="Notes" className="sm:col-span-2">
          <Input value={f.notes} maxLength={500} onChange={(e) => set('notes', e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Holds ----------

function HoldsTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const holds = useQuery({ queryKey: qk.salaryHolds, queryFn: payChangesApi.holds });
  const people = useEmployeeLookup();
  const now = new Date();
  const [f, setF] = useState({ employeeId: '', month: `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`, reason: '' });
  const hold = useMutation({
    mutationFn: () => {
      const [y, m] = f.month.split('-').map(Number);
      return payChangesApi.hold({ employeeId: f.employeeId, year: y, month: m, reason: f.reason.trim() });
    },
    onSuccess: () => {
      toast.success('Salary on hold', 'It is left out of the bank advice until released.');
      setF((x) => ({ ...x, employeeId: '', reason: '' }));
      qc.invalidateQueries({ queryKey: qk.payChanges });
      qc.invalidateQueries({ queryKey: qk.payroll });
    },
    onError: (e) => toast.error(e),
  });
  const release = useMutation({
    mutationFn: payChangesApi.release,
    onSuccess: () => {
      toast.success('Released');
      qc.invalidateQueries({ queryKey: qk.payChanges });
      qc.invalidateQueries({ queryKey: qk.payroll });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader title="Salary holds" description="E.g. clearance pending, missing documents, absconding. Payslips are still made." />
      <div className="flex flex-wrap items-end gap-2 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <Field label="Employee">
          <Select className="w-64" value={f.employeeId} onChange={(e) => setF({ ...f, employeeId: e.target.value })}>
            <option value="">Choose…</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Month">
          <Input type="month" value={f.month} onChange={(e) => setF({ ...f, month: e.target.value })} />
        </Field>
        <Field label="Reason">
          <Input className="w-64" value={f.reason} maxLength={500} onChange={(e) => setF({ ...f, reason: e.target.value })} />
        </Field>
        <Button icon={<PauseCircle className="size-4" />} disabled={!f.employeeId || !f.reason.trim()} loading={hold.isPending} onClick={() => hold.mutate()}>
          Hold
        </Button>
      </div>
      {!holds.data?.length ? (
        <EmptyState icon={PauseCircle} title="No holds" />
      ) : (
        <Table>
          <TBody>
            {holds.data.map((h) => (
              <Tr key={h.id}>
                <Td>
                  {h.name} <span className="text-xs text-zinc-500">{h.code}</span>
                </Td>
                <Td>{monthLabel(h.year, h.month)}</Td>
                <Td className="text-sm">
                  {h.reason}
                  <p className="text-xs text-zinc-500">
                    by {h.heldByName}, {formatDateTimeUtc(h.createdAtUtc)}
                  </p>
                </Td>
                <Td className="text-right">
                  {h.releasedAtUtc ? (
                    <Badge tone="green">Released {formatDateTimeUtc(h.releasedAtUtc)}</Badge>
                  ) : (
                    <Button
                      size="sm"
                      variant="secondary"
                      icon={<Undo2 className="size-3.5" />}
                      loading={release.isPending && release.variables === h.id}
                      onClick={() => release.mutate(h.id)}
                    >
                      Release
                    </Button>
                  )}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}

// ---------- Leave encashment ----------

function EncashTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const types = useLeaveTypes();
  const [f, setF] = useState({ employeeId: '', leaveTypeId: '', days: '', note: '' });
  const ready = !!f.employeeId && !!f.leaveTypeId && Number(f.days) > 0;
  const quote = useQuery({
    queryKey: qk.encashQuote(f.employeeId, f.leaveTypeId, f.days),
    queryFn: () => payChangesApi.quote(f.employeeId, f.leaveTypeId, Number(f.days)),
    enabled: ready,
    retry: false,
  });
  const encash = useMutation({
    mutationFn: () => payChangesApi.encash({ employeeId: f.employeeId, leaveTypeId: f.leaveTypeId, days: Number(f.days), note: f.note.trim() || null }),
    onSuccess: (r) => {
      toast.success(`PKR ${r.amount.toLocaleString()} added to this month's salary`);
      setF({ employeeId: '', leaveTypeId: '', days: '', note: '' });
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.pay });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card className="max-w-2xl">
      <CardHeader title="Pay out unused leave" description="The days come off this year's balance; the amount is paid with this month's salary." />
      <div className="grid gap-3 px-5 py-4 sm:grid-cols-2">
        <Field label="Employee" className="sm:col-span-2">
          <Select value={f.employeeId} onChange={(e) => setF({ ...f, employeeId: e.target.value })}>
            <option value="">Choose…</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Leave">
          <Select value={f.leaveTypeId} onChange={(e) => setF({ ...f, leaveTypeId: e.target.value })}>
            <option value="">Choose…</option>
            {types.data
              ?.filter((t) => t.isPaid)
              .map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
          </Select>
        </Field>
        <Field label="Days" hint={quote.data ? `${quote.data.remaining} left` : undefined}>
          <Input type="number" min={0.5} step={0.5} value={f.days} onChange={(e) => setF({ ...f, days: e.target.value })} />
        </Field>
        <Field label="Note" className="sm:col-span-2">
          <Textarea rows={2} value={f.note} maxLength={500} onChange={(e) => setF({ ...f, note: e.target.value })} />
        </Field>
        {quote.data && (
          <p className="rounded-lg bg-zinc-50 px-3 py-2 text-sm sm:col-span-2 dark:bg-zinc-800/50">
            {f.days} days × {formatCurrency(quote.data.dayRate)} ({quote.data.basis}) = <b>{formatCurrency(quote.data.amount)}</b>
          </p>
        )}
        <FormError message={quote.isError ? errorMessage(quote.error) : null} />
      </div>
      <div className="flex justify-end border-t border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <Button icon={<HandCoins className="size-4" />} disabled={!ready || !quote.data} loading={encash.isPending} onClick={() => encash.mutate()}>
          Encash
        </Button>
      </div>
    </Card>
  );
}

// ---------- History ----------

function HistoryTab() {
  const list = useQuery({ queryKey: qk.revisions, queryFn: payChangesApi.revisions });
  return (
    <Card>
      {list.isLoading ? (
        <SkeletonRows rows={5} cols={6} />
      ) : !list.data?.length ? (
        <EmptyState icon={TrendingUp} title="No revisions yet" />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>From</Th>
              <Th>Employee</Th>
              <Th>Kind</Th>
              <Th className="text-right">Gross before</Th>
              <Th className="text-right">Gross after</Th>
              <Th>Reason</Th>
            </Tr>
          </THead>
          <TBody>
            {list.data.map((r) => (
              <Tr key={r.id}>
                <Td className="whitespace-nowrap">{formatDate(r.effectiveFrom)}</Td>
                <Td>
                  {r.name} <span className="text-xs text-zinc-500">{r.code}</span>
                </Td>
                <Td>{r.kind}</Td>
                <Td className="text-right tabular-nums">{formatCurrency(r.oldGross)}</Td>
                <Td className="text-right font-medium tabular-nums">
                  {formatCurrency(r.newGross)}
                  {r.oldGross > 0 && <span className="ml-1 text-xs text-zinc-500">({(((r.newGross - r.oldGross) / r.oldGross) * 100).toFixed(1)}%)</span>}
                </Td>
                <Td className="text-sm">
                  {r.reason ?? '—'}
                  <p className="text-xs text-zinc-500">{r.byName}</p>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}
