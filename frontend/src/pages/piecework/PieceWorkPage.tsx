import { useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { Check, ClipboardList, Factory, Plus, Trash2, X } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { pieceApi, type PieceEntry, type PieceItem, type PieceReport } from '../../api/piecework';
import { useUser } from '../../auth/AuthContext';
import { isApprover, isHr } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  StatusBadge,
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
import { formatCurrency, formatDate, monthLabel, todayIso } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';

type Tab = 'mine' | 'sheet' | 'approve' | 'report' | 'setup';

/** Work paid per unit: own entries, team production sheets and approvals, reports, and (HR) items and rates. */
export function PieceWorkPage() {
  const user = useUser();
  const hr = isHr(user);
  const lead = isApprover(user);
  const [params, setParams] = useSearchParams();
  const allowed: Tab[] = [
    ...(user.employeeId ? (['mine'] as Tab[]) : []),
    ...(lead ? (['sheet', 'approve', 'report'] as Tab[]) : []),
    ...(hr ? (['setup'] as Tab[]) : []),
  ];
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && allowed.includes(raw) ? raw : allowed[0];
  const labels: Record<Tab, string> = { mine: 'My work', sheet: 'Production sheet', approve: 'To approve', report: 'Report', setup: 'Items and rates' };
  const now = new Date();
  const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 });
  const months = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    return { year: d.getFullYear(), month: d.getMonth() + 1 };
  });
  const monthPicker = (
    <Select
      className="w-44"
      value={`${period.year}-${period.month}`}
      onChange={(e) => {
        const [y, m] = e.target.value.split('-').map(Number);
        setPeriod({ year: y, month: m });
      }}
    >
      {months.map((p) => (
        <option key={`${p.year}-${p.month}`} value={`${p.year}-${p.month}`}>
          {monthLabel(p.year, p.month)}
        </option>
      ))}
    </Select>
  );
  return (
    <>
      <PageHeader title="Piece work" description="Work paid per unit. Approved work is paid with that month's salary." />
      <Tabs<Tab> className="mb-5" value={tab} onChange={(t) => setParams({ tab: t })} items={allowed.map((t) => ({ value: t, label: labels[t] }))} />
      {tab === 'mine' && <MineTab period={period} picker={monthPicker} />}
      {tab === 'sheet' && <SheetTab />}
      {tab === 'approve' && <ApproveTab period={period} picker={monthPicker} />}
      {tab === 'report' && <ReportTab period={period} picker={monthPicker} />}
      {tab === 'setup' && <SetupTab />}
    </>
  );
}

function EntriesTable({ rows, showPerson, select }: { rows: PieceEntry[]; showPerson: boolean; select?: { ids: string[]; set: (ids: string[]) => void } }) {
  const qc = useQueryClient();
  const [deleting, setDeleting] = useState<PieceEntry | null>(null);
  return (
    <>
      <Table>
        <THead>
          <Tr>
            {select && <Th className="w-8" />}
            <Th>Date</Th>
            {showPerson && <Th>Employee</Th>}
            <Th>Work</Th>
            <Th className="text-right">Qty</Th>
            <Th className="text-right">Amount</Th>
            <Th>Status</Th>
            <Th />
          </Tr>
        </THead>
        <TBody>
          {rows.map((e) => (
            <Tr key={e.id}>
              {select && (
                <Td>
                  {e.canDecide && (
                    <input
                      type="checkbox"
                      aria-label={`Select ${e.employeeName}`}
                      className="size-4 accent-accent-600"
                      checked={select.ids.includes(e.id)}
                      onChange={(ev) => select.set(ev.target.checked ? [...select.ids, e.id] : select.ids.filter((x) => x !== e.id))}
                    />
                  )}
                </Td>
              )}
              <Td className="text-sm whitespace-nowrap">{formatDate(e.date)}</Td>
              {showPerson && (
                <Td>
                  <p className="font-medium">{e.employeeName}</p>
                  <p className="text-xs text-zinc-500">{e.employeeCode}</p>
                </Td>
              )}
              <Td>
                {e.item}
                <p className="text-xs text-zinc-500">
                  @ {formatCurrency(e.rate)} / {e.unit}
                  {e.efficiency != null && ` · ${e.efficiency}% of target`}
                  {e.notes ? ` · ${e.notes}` : ''}
                </p>
              </Td>
              <Td className="text-right tabular-nums">{e.quantity}</Td>
              <Td className="text-right tabular-nums">{formatCurrency(e.amount)}</Td>
              <Td>
                {e.paid ? <Badge tone="green">Paid</Badge> : <StatusBadge status={e.status} />}
                {e.decisionNote && <p className="text-xs text-zinc-500">“{e.decisionNote}”</p>}
              </Td>
              <Td className="text-right">
                {e.canDelete && (
                  <IconButton label="Remove" onClick={() => setDeleting(e)}>
                    <Trash2 className="size-4" />
                  </IconButton>
                )}
              </Td>
            </Tr>
          ))}
        </TBody>
      </Table>
      <DeleteConfirm
        item={deleting}
        name={deleting ? `${deleting.item} on ${formatDate(deleting.date)}` : ''}
        remove={pieceApi.remove}
        invalidate={[qk.piece]}
        onClose={() => {
          setDeleting(null);
          qc.invalidateQueries({ queryKey: qk.piece });
        }}
      />
    </>
  );
}

function MineTab({ period, picker }: { period: { year: number; month: number }; picker: ReactNode }) {
  const user = useUser();
  const entries = useQuery({ queryKey: qk.pieceEntries(period.year, period.month, false), queryFn: () => pieceApi.entries(period) });
  const [adding, setAdding] = useState(false);
  const rows = entries.data ?? [];
  const approved = rows.filter((e) => e.status === 'Approved');
  return (
    <Card>
      <CardHeader
        title="My work"
        description={rows.length ? `${formatCurrency(approved.reduce((a, e) => a + e.amount, 0))} approved this month` : undefined}
        actions={
          <>
            {picker}
            <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setAdding(true)}>
              Add work
            </Button>
          </>
        }
      />
      {entries.isLoading ? (
        <SkeletonRows rows={4} cols={5} />
      ) : entries.isError ? (
        <ErrorState error={entries.error} onRetry={() => entries.refetch()} />
      ) : !rows.length ? (
        <EmptyState icon={Factory} title="No work this month" description="Add what you made each day; your manager approves it." />
      ) : (
        <EntriesTable rows={rows} showPerson={false} />
      )}
      {adding && user.employeeId && <OwnEntryModal employeeId={user.employeeId} onClose={() => setAdding(false)} />}
    </Card>
  );
}

function OwnEntryModal({ employeeId, onClose }: { employeeId: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const items = useQuery({ queryKey: qk.pieceItems, queryFn: pieceApi.items });
  const [itemId, setItemId] = useState('');
  const [date, setDate] = useState(todayIso());
  const [qty, setQty] = useState('');
  const [notes, setNotes] = useState('');
  const item = items.data?.find((i) => i.id === itemId);
  const save = useMutation({
    mutationFn: () => pieceApi.save({ itemId, date, lines: [{ employeeId, quantity: Number(qty), notes: notes.trim() || null }] }),
    onSuccess: (r) => {
      toast.success('Work added', r.approved ? undefined : 'Waiting for your manager.');
      qc.invalidateQueries({ queryKey: qk.piece });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Add work"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!itemId || !(Number(qty) > 0)} onClick={() => save.mutate()}>
            Add
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <Field label="Work">
          <Select value={itemId} onChange={(e) => setItemId(e.target.value)}>
            <option value="">Choose…</option>
            {items.data
              ?.filter((i) => i.isActive)
              .map((i) => (
                <option key={i.id} value={i.id}>
                  {i.name} ({formatCurrency(i.rate)} / {i.unit})
                </option>
              ))}
          </Select>
        </Field>
        <Field label="Day">
          <Input type="date" max={todayIso()} value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Field label={item ? `Quantity (${item.unit})` : 'Quantity'} hint={item?.dailyTarget ? `Daily target ${item.dailyTarget}` : undefined}>
          <Input type="number" min={0} step="any" value={qty} onChange={(e) => setQty(e.target.value)} />
        </Field>
        <Field label="Notes">
          <Input value={notes} maxLength={500} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function SheetTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const items = useQuery({ queryKey: qk.pieceItems, queryFn: pieceApi.items });
  const team = useQuery({ queryKey: qk.pieceTeam, queryFn: pieceApi.team });
  const [itemId, setItemId] = useState('');
  const [date, setDate] = useState(todayIso());
  const [qty, setQty] = useState<Record<string, string>>({});
  const item = items.data?.find((i) => i.id === itemId);
  const filled = Object.entries(qty).filter(([, v]) => Number(v) > 0);
  const save = useMutation({
    mutationFn: () => pieceApi.save({ itemId, date, lines: filled.map(([employeeId, v]) => ({ employeeId, quantity: Number(v), notes: null })) }),
    onSuccess: (r) => {
      toast.success(`${r.added} entries saved`, r.approved ? 'Approved and ready for payroll.' : undefined);
      setQty({});
      qc.invalidateQueries({ queryKey: qk.piece });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader title="Production sheet" description="One item and one day for the whole team. Entered here, it counts as approved." />
      <div className="flex flex-wrap items-end gap-3 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <Field label="Work">
          <Select className="w-72" value={itemId} onChange={(e) => setItemId(e.target.value)}>
            <option value="">Choose…</option>
            {items.data
              ?.filter((i) => i.isActive)
              .map((i) => (
                <option key={i.id} value={i.id}>
                  {i.name} ({formatCurrency(i.rate)} / {i.unit})
                </option>
              ))}
          </Select>
        </Field>
        <Field label="Day">
          <Input type="date" max={todayIso()} value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Button loading={save.isPending} disabled={!itemId || !filled.length} onClick={() => save.mutate()}>
          Save {filled.length || ''}
        </Button>
      </div>
      {!team.data?.length ? (
        <EmptyState icon={ClipboardList} title="No team" description="People who report to you show up here." />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Employee</Th>
              <Th className="w-40">{item ? `Quantity (${item.unit})` : 'Quantity'}</Th>
              <Th className="text-right">Amount</Th>
            </Tr>
          </THead>
          <TBody>
            {team.data.map((p) => (
              <Tr key={p.id}>
                <Td>{p.name}</Td>
                <Td>
                  <Input
                    type="number"
                    min={0}
                    step="any"
                    aria-label={`Quantity for ${p.name}`}
                    value={qty[p.id] ?? ''}
                    onChange={(e) => setQty((q) => ({ ...q, [p.id]: e.target.value }))}
                  />
                </Td>
                <Td className="text-right text-sm text-zinc-500 tabular-nums">
                  {item && Number(qty[p.id]) > 0 ? `≈ ${formatCurrency(Number(qty[p.id]) * item.rate)}` : ''}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      <p className="px-5 py-2 text-xs text-zinc-500">Amounts use the standard rate; people with their own rate are paid at that rate.</p>
    </Card>
  );
}

function ApproveTab({ period, picker }: { period: { year: number; month: number }; picker: ReactNode }) {
  const toast = useToast();
  const qc = useQueryClient();
  const entries = useQuery({ queryKey: qk.pieceEntries(period.year, period.month, true), queryFn: () => pieceApi.entries({ ...period, team: true }) });
  const [ids, setIds] = useState<string[]>([]);
  const [rejecting, setRejecting] = useState(false);
  const [note, setNote] = useState('');
  const decide = useMutation({
    mutationFn: (approve: boolean) => pieceApi.decide(ids, approve, approve ? null : note.trim()),
    onSuccess: (r, approve) => {
      toast.success(`${r.decided} ${approve ? 'approved' : 'rejected'}`);
      setIds([]);
      setRejecting(false);
      setNote('');
      qc.invalidateQueries({ queryKey: qk.piece });
    },
    onError: (e) => toast.error(e),
  });
  const rows = entries.data ?? [];
  const waiting = rows.filter((e) => e.canDecide);
  return (
    <Card>
      <CardHeader
        title="Team work"
        description={waiting.length ? `${waiting.length} waiting for you` : 'Nothing waiting'}
        actions={
          <>
            {picker}
            {waiting.length > 0 && (
              <Button size="sm" variant="ghost" onClick={() => setIds(ids.length === waiting.length ? [] : waiting.map((e) => e.id))}>
                {ids.length === waiting.length ? 'Clear' : 'Select all waiting'}
              </Button>
            )}
            <Button size="sm" variant="secondary" icon={<X className="size-3.5" />} disabled={!ids.length} onClick={() => setRejecting(true)}>
              Reject
            </Button>
            <Button
              size="sm"
              variant="success"
              icon={<Check className="size-3.5" />}
              disabled={!ids.length}
              loading={decide.isPending && decide.variables}
              onClick={() => decide.mutate(true)}
            >
              Approve {ids.length || ''}
            </Button>
          </>
        }
      />
      {entries.isLoading ? (
        <SkeletonRows rows={5} cols={6} />
      ) : !rows.length ? (
        <EmptyState icon={Factory} title="No work this month" />
      ) : (
        <EntriesTable rows={rows} showPerson select={{ ids, set: setIds }} />
      )}
      {rejecting && (
        <Modal
          open
          onClose={() => setRejecting(false)}
          size="sm"
          title={`Reject ${ids.length}`}
          footer={
            <Button variant="danger" disabled={!note.trim()} loading={decide.isPending} onClick={() => decide.mutate(false)}>
              Reject
            </Button>
          }
        >
          <Field label="Why?" required>
            <Textarea rows={2} value={note} maxLength={500} onChange={(e) => setNote(e.target.value)} />
          </Field>
        </Modal>
      )}
    </Card>
  );
}

function ReportTab({ period, picker }: { period: { year: number; month: number }; picker: ReactNode }) {
  const r = useQuery({ queryKey: qk.pieceReport(period.year, period.month), queryFn: () => pieceApi.report(period.year, period.month) });
  const d: PieceReport | undefined = r.data;
  const table = (title: string, rows: PieceReport['byItem']) => (
    <Card>
      <CardHeader title={title} />
      {!rows.length ? (
        <p className="px-5 py-6 text-sm text-zinc-500">No approved work.</p>
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th />
              <Th className="text-right">Quantity</Th>
              <Th className="text-right">Days</Th>
              <Th className="text-right">Of target</Th>
              <Th className="text-right">Amount</Th>
            </Tr>
          </THead>
          <TBody>
            {rows.map((x) => (
              <Tr key={x.name}>
                <Td>{x.name}</Td>
                <Td className="text-right tabular-nums">{x.quantity}</Td>
                <Td className="text-right tabular-nums">{x.days}</Td>
                <Td className="text-right tabular-nums">{x.efficiency != null ? `${x.efficiency}%` : '—'}</Td>
                <Td className="text-right tabular-nums">{formatCurrency(x.amount)}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
  return (
    <div className="space-y-5">
      <div className="flex justify-end">{picker}</div>
      {r.isLoading || !d ? (
        <SkeletonRows rows={4} cols={4} />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {[
              ['Approved pay', formatCurrency(d.amount)],
              ['Units', d.quantity],
              ['People', d.people],
              ['Waiting approval', `${d.pending} · ${formatCurrency(d.pendingAmount)}`],
            ].map(([k, v]) => (
              <Card key={k as string} className="p-4">
                <p className="text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{v}</p>
                <p className="text-xs text-zinc-500">{k}</p>
              </Card>
            ))}
          </div>
          {table('By work', d.byItem)}
          {table('By person', d.byEmployee)}
        </>
      )}
    </div>
  );
}

function SetupTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const items = useQuery({ queryKey: qk.pieceItems, queryFn: pieceApi.items });
  const categories = useQuery({ queryKey: qk.pieceCategories, queryFn: pieceApi.categories });
  const [editing, setEditing] = useState<PieceItem | 'new' | null>(null);
  const [deleting, setDeleting] = useState<PieceItem | null>(null);
  const [rates, setRates] = useState<PieceItem | null>(null);
  const [cat, setCat] = useState('');
  const addCat = useMutation({
    mutationFn: () => pieceApi.addCategory(cat.trim()),
    onSuccess: () => {
      setCat('');
      qc.invalidateQueries({ queryKey: qk.pieceCategories });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <div className="space-y-5">
      <Card>
        <CardHeader
          title="Items"
          description="What is paid per unit, at what rate"
          actions={
            <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
              New item
            </Button>
          }
        />
        {!items.data?.length ? (
          <EmptyState icon={Factory} title="No items yet" description="E.g. collar stitching at PKR 12 a piece, packing at PKR 30 a carton." />
        ) : (
          <Table>
            <THead>
              <Tr>
                <Th>Item</Th>
                <Th className="text-right">Rate</Th>
                <Th className="text-right">Daily target</Th>
                <Th>Own rates</Th>
                <Th />
              </Tr>
            </THead>
            <TBody>
              {items.data.map((i) => (
                <Tr key={i.id}>
                  <Td>
                    <p className="font-medium">
                      {i.name} {!i.isActive && <Badge>Off</Badge>}
                    </p>
                    {i.category && <p className="text-xs text-zinc-500">{i.category}</p>}
                  </Td>
                  <Td className="text-right tabular-nums">
                    {formatCurrency(i.rate)} / {i.unit}
                  </Td>
                  <Td className="text-right tabular-nums">{i.dailyTarget ?? '—'}</Td>
                  <Td>
                    <Button size="sm" variant="ghost" onClick={() => setRates(i)}>
                      {i.specialRates ? `${i.specialRates} people` : 'Set'}
                    </Button>
                  </Td>
                  <Td>
                    <RowActions label={i.name} onEdit={() => setEditing(i)} onDelete={() => setDeleting(i)} />
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      <Card>
        <CardHeader title="Categories" />
        <div className="flex flex-wrap items-center gap-2 px-5 py-4">
          {categories.data?.map((c) => (
            <Badge key={c.id}>
              {c.name} · {c.items}
            </Badge>
          ))}
          <Input className="h-8 w-48" value={cat} maxLength={100} onChange={(e) => setCat(e.target.value)} placeholder="New category" />
          <Button size="sm" variant="secondary" disabled={!cat.trim()} loading={addCat.isPending} onClick={() => addCat.mutate()}>
            Add
          </Button>
        </div>
      </Card>
      {editing && <ItemModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {rates && <RatesModal item={rates} onClose={() => setRates(null)} />}
      <DeleteConfirm item={deleting} name={deleting?.name ?? ''} remove={pieceApi.deleteItem} invalidate={[qk.pieceItems]} onClose={() => setDeleting(null)} />
    </div>
  );
}

function ItemModal({ item, onClose }: { item: PieceItem | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const categories = useQuery({ queryKey: qk.pieceCategories, queryFn: pieceApi.categories });
  const [f, setF] = useState({
    name: item?.name ?? '',
    categoryId: item?.categoryId ?? '',
    unit: item?.unit ?? 'piece',
    rate: item?.rate?.toString() ?? '',
    target: item?.dailyTarget?.toString() ?? '',
    active: item?.isActive ?? true,
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () => {
      const b = {
        name: f.name.trim(),
        categoryId: f.categoryId || null,
        unit: f.unit.trim(),
        rate: Number(f.rate),
        dailyTarget: f.target ? Number(f.target) : null,
        isActive: f.active,
      };
      return item ? pieceApi.updateItem(item.id, b) : pieceApi.addItem(b);
    },
    onSuccess: () => {
      toast.success('Saved');
      qc.invalidateQueries({ queryKey: qk.piece });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? 'Edit item' : 'New item'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.name.trim() || !f.unit.trim() || !(Number(f.rate) >= 0) || f.rate === ''} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2">
          <Input value={f.name} maxLength={150} onChange={(e) => set('name', e.target.value)} placeholder="Collar stitching" />
        </Field>
        <Field label="Category">
          <Select value={f.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
            <option value="">—</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Unit" required>
          <Input value={f.unit} maxLength={30} onChange={(e) => set('unit', e.target.value)} placeholder="piece, dozen, kg" />
        </Field>
        <Field label="Rate (PKR per unit)" required>
          <Input type="number" min={0} step="any" value={f.rate} onChange={(e) => set('rate', e.target.value)} />
        </Field>
        <Field label="Daily target" hint="For efficiency">
          <Input type="number" min={0} step="any" value={f.target} onChange={(e) => set('target', e.target.value)} />
        </Field>
        {item && <Checkbox label="In use" checked={f.active} onChange={(e) => set('active', e.target.checked)} />}
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

function RatesModal({ item, onClose }: { item: PieceItem; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const rates = useQuery({ queryKey: qk.pieceRates(item.id), queryFn: () => pieceApi.rates(item.id) });
  const [who, setWho] = useState('');
  const [rate, setRate] = useState('');
  const add = useMutation({
    mutationFn: () => pieceApi.setRate(item.id, who, Number(rate)),
    onSuccess: () => {
      setWho('');
      setRate('');
      qc.invalidateQueries({ queryKey: qk.piece });
    },
    onError: (e) => toast.error(e),
  });
  const remove = useMutation({
    mutationFn: pieceApi.deleteRate,
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.piece }),
    onError: (e) => toast.error(e),
  });
  return (
    <Modal open onClose={onClose} title={`Own rates: ${item.name}`} description={`Standard rate ${formatCurrency(item.rate)} / ${item.unit}`}>
      <ul className="mb-3 divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
        {rates.data?.map((r) => (
          <li key={r.id} className="flex items-center justify-between px-3 py-2 text-sm">
            <span>{r.employeeName}</span>
            <span className="flex items-center gap-2 tabular-nums">
              {formatCurrency(r.rate)}
              <IconButton label="Remove" onClick={() => remove.mutate(r.id)}>
                <Trash2 className="size-4" />
              </IconButton>
            </span>
          </li>
        ))}
        {!rates.data?.length && <li className="px-3 py-3 text-sm text-zinc-500">Everyone is paid the standard rate.</li>}
      </ul>
      <div className="flex gap-2">
        <Select value={who} onChange={(e) => setWho(e.target.value)}>
          <option value="">Person…</option>
          {people.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </Select>
        <Input className="w-28" type="number" min={0} step="any" placeholder="Rate" value={rate} onChange={(e) => setRate(e.target.value)} />
        <Button variant="secondary" disabled={!who || rate === ''} loading={add.isPending} onClick={() => add.mutate()}>
          Set
        </Button>
      </div>
    </Modal>
  );
}
