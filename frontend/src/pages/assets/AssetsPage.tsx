import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { AlertTriangle, Boxes, ClipboardList, Download, Layers, Package, PackageCheck, Plus, Search, Tags, Undo2, Wrench } from 'lucide-react';
import { errorMessage } from '../../api/client';
import {
  ASSET_STATUS_LABELS,
  assetsApi,
  CONDITIONS,
  type Asset,
  type AssetCondition,
  type AssetKit,
  type AssetRequest,
  type AssetStatus,
  type AssetType,
} from '../../api/assets';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  Drawer,
  EmptyState,
  ErrorState,
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
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import { DeleteConfirm, RowActions } from '../settings/shared';
import { AssetStatusBadge, HandOverModal, TakeBackModal } from './shared';

type Tab = 'inventory' | 'requests' | 'types' | 'kits';

/** HR and handlers: the inventory, hand-overs waiting, and (HR) types and starter kits. */
export function AssetsPage() {
  const hr = isHr(useUser());
  const [params, setParams] = useSearchParams();
  const raw = (params.get('tab') as Tab | null) ?? 'inventory';
  const tab: Tab = !hr && (raw === 'types' || raw === 'kits') ? 'inventory' : raw;
  const summary = useQuery({ queryKey: qk.assetsSummary, queryFn: assetsApi.summary });
  const [adding, setAdding] = useState(false);
  const [exporting, setExporting] = useState(false);
  const toast = useToast();
  const s = summary.data;
  const tile = (label: string, value: string | number | undefined, Icon: typeof Boxes, tone: string, onClick?: () => void) => (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className="flex items-center gap-3 rounded-xl border border-zinc-200 bg-white p-4 text-start transition enabled:hover:border-zinc-300 dark:border-zinc-800 dark:bg-zinc-900"
    >
      <span className={cn('flex size-9 items-center justify-center rounded-lg', tone)}>
        <Icon className="size-4" />
      </span>
      <span>
        <span className="block text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value ?? '–'}</span>
        <span className="block text-xs text-zinc-500 dark:text-zinc-400">{label}</span>
      </span>
    </button>
  );
  return (
    <>
      <PageHeader
        title="Assets"
        description="Company property: what you have, who has it, and what needs handing over."
        actions={
          <>
            <Button
              variant="secondary"
              icon={<Download className="size-4" />}
              loading={exporting}
              onClick={() => {
                setExporting(true);
                assetsApi
                  .exportExcel(`assets-${todayIso()}.xlsx`)
                  .catch((err) => toast.error(err))
                  .finally(() => setExporting(false));
              }}
            >
              Export
            </Button>
            <Button icon={<Plus className="size-4" />} onClick={() => setAdding(true)}>
              Add asset
            </Button>
          </>
        }
      />
      <div className="mb-5 grid grid-cols-2 gap-3 md:grid-cols-5">
        {tile('Available', s?.available, PackageCheck, 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300')}
        {tile('With people', s?.assigned, Package, 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300')}
        {tile('In repair', s?.inRepair, Wrench, 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300')}
        {tile('Late returns', s?.overdueReturns, AlertTriangle, 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300')}
        {tile('To hand over / take back', s ? `${s.toHandOver} / ${s.toTakeBack}` : undefined, ClipboardList, 'bg-accent-100 text-accent-700 dark:bg-accent-500/15 dark:text-accent-300', () =>
          setParams({ tab: 'requests' }, { replace: true }),
        )}
      </div>
      <Tabs<Tab>
        className="mb-5"
        value={tab}
        onChange={(t) => setParams(t === 'inventory' ? {} : { tab: t }, { replace: true })}
        items={[
          { value: 'inventory', label: 'Inventory', icon: <Boxes className="size-4" /> },
          { value: 'requests', label: s && s.toHandOver + s.toTakeBack ? `Requests (${s.toHandOver + s.toTakeBack})` : 'Requests', icon: <ClipboardList className="size-4" /> },
          ...(hr
            ? ([
                { value: 'types', label: 'Types', icon: <Tags className="size-4" /> },
                { value: 'kits', label: 'Starter kits', icon: <Layers className="size-4" /> },
              ] as const)
            : []),
        ]}
      />
      {tab === 'inventory' && <Inventory />}
      {tab === 'requests' && <Requests />}
      {tab === 'types' && <Types />}
      {tab === 'kits' && <Kits />}
      {adding && <AssetModal item={null} onClose={() => setAdding(false)} />}
      {s && s.value > 0 && <p className="mt-4 text-end text-xs text-zinc-500 dark:text-zinc-400">Value of assets in use or in stock: {formatCurrency(s.value)}</p>}
    </>
  );
}

function Inventory() {
  const types = useQuery({ queryKey: qk.assetTypes, queryFn: assetsApi.types });
  const [status, setStatus] = useState('');
  const [typeId, setTypeId] = useState('');
  const [search, setSearch] = useState('');
  const q = useDebounced(search.trim(), 300);
  const query = { status: status === 'overdue' ? undefined : status || undefined, overdue: status === 'overdue' || undefined, typeId: typeId || undefined, search: q || undefined };
  const list = useQuery({ queryKey: [...qk.assets, 'list', query], queryFn: () => assetsApi.list(query), placeholderData: keepPreviousData });
  const [open, setOpen] = useState<string | null>(null);
  return (
    <Card>
      <div className="flex flex-col gap-3 border-b border-zinc-100 p-4 sm:flex-row sm:items-center dark:border-zinc-800">
        <div className="relative flex-1 sm:max-w-xs">
          <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" />
          <Input className="ps-9" placeholder="Tag, name, serial or holder" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Search assets" />
        </div>
        <Select className="sm:w-44" value={typeId} onChange={(e) => setTypeId(e.target.value)} aria-label="Type">
          <option value="">All types</option>
          {types.data?.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </Select>
        <Select className="sm:w-44" value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Status">
          <option value="">Any status</option>
          {(Object.keys(ASSET_STATUS_LABELS) as AssetStatus[]).map((s) => (
            <option key={s} value={s}>
              {ASSET_STATUS_LABELS[s]}
            </option>
          ))}
          <option value="overdue">Late returns</option>
        </Select>
      </div>
      {list.isLoading ? (
        <SkeletonRows rows={5} cols={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState icon={Boxes} title="No assets" description={types.data?.length ? 'Add laptops, phones and other property with Add asset.' : 'Set up asset types first, then add your property.'} />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Asset</Th>
              <Th>Status</Th>
              <Th>With</Th>
              <Th>Condition</Th>
              <Th className="text-end">Cost</Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((a) => (
              <Tr key={a.id} onClick={() => setOpen(a.id)}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{a.name}</p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    <span className="font-mono">{a.tag}</span> · {a.type}
                    {a.serialNumber ? ` · SN ${a.serialNumber}` : ''}
                  </p>
                </Td>
                <Td>
                  <AssetStatusBadge status={a.status} />
                </Td>
                <Td className="text-sm">
                  {a.holder ? (
                    <>
                      <p className="text-zinc-900 dark:text-zinc-100">{a.holder}</p>
                      <p className={cn('text-xs', a.isOverdue ? 'font-medium text-red-600 dark:text-red-400' : 'text-zinc-500 dark:text-zinc-400')}>
                        since {formatDate(a.assignedOn)}
                        {a.dueBack ? ` · back by ${formatDate(a.dueBack)}` : ''}
                      </p>
                    </>
                  ) : (
                    <span className="text-zinc-500 dark:text-zinc-400">—</span>
                  )}
                </Td>
                <Td className="text-sm">{a.condition}</Td>
                <Td className="text-end text-sm tabular-nums">{a.cost ? formatCurrency(a.cost) : '—'}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {open && <AssetDrawer id={open} onClose={() => setOpen(null)} />}
    </Card>
  );
}

function AssetDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const detail = useQuery({ queryKey: qk.asset(id), queryFn: () => assetsApi.get(id) });
  const [dialog, setDialog] = useState<'edit' | 'assign' | 'return' | null>(null);
  const status = useMutation({
    mutationFn: (s: AssetStatus) => assetsApi.setStatus(id, s, null),
    onSuccess: () => {
      toast.success('Status updated');
      qc.invalidateQueries({ queryKey: qk.assets });
    },
    onError: (err) => toast.error(err),
  });
  const a = detail.data?.asset;
  return (
    <Drawer open onClose={onClose} title={a ? `${a.name}` : 'Asset'} description={a ? `${a.tag} · ${a.type}` : undefined}>
      {!a ? (
        <SkeletonRows rows={4} cols={2} />
      ) : (
        <div className="space-y-5">
          <div className="flex flex-wrap items-center gap-2">
            <AssetStatusBadge status={a.status} />
            <Badge>{a.condition}</Badge>
            {a.isOverdue && <Badge tone="red">Late return</Badge>}
          </div>
          {a.canManage && (
            <div className="flex flex-wrap gap-2">
              {a.status === 'Available' && (
                <Button size="sm" icon={<Package className="size-3.5" />} onClick={() => setDialog('assign')}>
                  Hand over
                </Button>
              )}
              {a.status === 'Assigned' && (
                <Button size="sm" icon={<Undo2 className="size-3.5" />} onClick={() => setDialog('return')}>
                  Take back
                </Button>
              )}
              <Button size="sm" variant="secondary" onClick={() => setDialog('edit')}>
                Edit
              </Button>
              {a.status !== 'Assigned' && a.status !== 'Available' && (
                <Button size="sm" variant="secondary" loading={status.isPending} onClick={() => status.mutate('Available')}>
                  Back in stock
                </Button>
              )}
              {a.status === 'Available' && (
                <>
                  <Button size="sm" variant="ghost" loading={status.isPending} onClick={() => status.mutate('InRepair')}>
                    Send to repair
                  </Button>
                  <Button size="sm" variant="ghost" loading={status.isPending} onClick={() => status.mutate('Retired')}>
                    Retire
                  </Button>
                </>
              )}
              {(a.status === 'Assigned' || a.status === 'Available') && (
                <Button size="sm" variant="ghost" loading={status.isPending} onClick={() => status.mutate('Lost')}>
                  Mark lost
                </Button>
              )}
            </div>
          )}
          <dl className="grid grid-cols-2 gap-3 text-sm">
            {[
              ['With', a.holder ?? '—'],
              ['Serial number', a.serialNumber ?? '—'],
              ['Purchased', a.purchasedOn ? formatDate(a.purchasedOn) : '—'],
              ['Cost', a.cost ? formatCurrency(a.cost) : '—'],
              ['Warranty until', a.warrantyUntil ? formatDate(a.warrantyUntil) : '—'],
              ['Due back', a.dueBack ? formatDate(a.dueBack) : '—'],
            ].map(([k, v]) => (
              <div key={k}>
                <dt className="text-xs text-zinc-500 dark:text-zinc-400">{k}</dt>
                <dd className="text-zinc-900 dark:text-zinc-100">{v}</dd>
              </div>
            ))}
          </dl>
          {a.notes && <p className="text-sm whitespace-pre-line text-zinc-600 dark:text-zinc-400">{a.notes}</p>}
          <div>
            <h3 className="mb-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">History</h3>
            {!detail.data!.history.length ? (
              <p className="text-sm text-zinc-500 dark:text-zinc-400">Never handed out.</p>
            ) : (
              <ol className="space-y-3">
                {detail.data!.history.map((h) => (
                  <li key={h.id} className="rounded-lg border border-zinc-200 p-3 text-sm dark:border-zinc-800">
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{h.employeeName}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {formatDate(h.assignedOn)} ({h.conditionOut.toLowerCase()}) → {h.returnedOn ? `${formatDate(h.returnedOn)} (${h.conditionIn?.toLowerCase() ?? '—'})` : 'still with them'}
                    </p>
                    {h.notes && <p className="mt-1 text-xs whitespace-pre-line text-zinc-600 dark:text-zinc-400">{h.notes}</p>}
                  </li>
                ))}
              </ol>
            )}
          </div>
        </div>
      )}
      {dialog === 'edit' && a && <AssetModal item={a} onClose={() => setDialog(null)} />}
      {dialog === 'assign' && a && <HandOverModal assetId={a.id} onClose={() => setDialog(null)} />}
      {dialog === 'return' && a?.assignmentId && <TakeBackModal assignmentId={a.assignmentId} label={`${a.name} from ${a.holder}`} onClose={() => setDialog(null)} />}
    </Drawer>
  );
}

function AssetModal({ item, onClose }: { item: Asset | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const types = useQuery({ queryKey: qk.assetTypes, queryFn: assetsApi.types });
  const [typeId, setTypeId] = useState(item?.typeId ?? '');
  const [tag, setTag] = useState(item?.tag ?? '');
  const [name, setName] = useState(item?.name ?? '');
  const [serial, setSerial] = useState(item?.serialNumber ?? '');
  const [purchased, setPurchased] = useState(item?.purchasedOn ?? '');
  const [cost, setCost] = useState(item?.cost ? String(item.cost) : '');
  const [warranty, setWarranty] = useState(item?.warrantyUntil ?? '');
  const [condition, setCondition] = useState<AssetCondition>(item?.condition ?? 'New');
  const [notes, setNotes] = useState(item?.notes ?? '');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = {
        typeId,
        tag: tag.trim() || null,
        name: name.trim(),
        serialNumber: serial.trim() || null,
        purchasedOn: purchased || null,
        cost: cost ? Number(cost) : null,
        warrantyUntil: warranty || null,
        condition,
        notes: notes.trim() || null,
      };
      return item ? assetsApi.update(item.id, b) : assetsApi.create(b);
    },
    onSuccess: (a) => {
      toast.success(item ? 'Asset updated' : `Added as ${a.tag}`);
      qc.invalidateQueries({ queryKey: qk.assets });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={item ? 'Edit asset' : 'Add asset'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!typeId) return setError('Choose the type.');
              if (!name.trim()) return setError('Give it a name, e.g. the model.');
              save.mutate();
            }}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Type" required>
          <Select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
            <option value="">Choose…</option>
            {types.data?.filter((t) => t.isActive || t.id === typeId).map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Tag" hint={item ? undefined : 'Blank gives the next AST number'}>
          <Input value={tag} maxLength={50} onChange={(e) => setTag(e.target.value)} placeholder="AST-0001" />
        </Field>
        <Field label="Name / model" required>
          <Input value={name} maxLength={200} onChange={(e) => setName(e.target.value)} placeholder="Dell Latitude 5420" />
        </Field>
        <Field label="Serial number">
          <Input value={serial} maxLength={100} onChange={(e) => setSerial(e.target.value)} />
        </Field>
        <Field label="Purchased on">
          <Input type="date" value={purchased} onChange={(e) => setPurchased(e.target.value)} />
        </Field>
        <Field label="Cost (Rs)">
          <Input type="number" min={0} value={cost} onChange={(e) => setCost(e.target.value)} />
        </Field>
        <Field label="Warranty until">
          <Input type="date" value={warranty} onChange={(e) => setWarranty(e.target.value)} />
        </Field>
        <Field label="Condition">
          <Select value={condition} onChange={(e) => setCondition(e.target.value as AssetCondition)}>
            {CONDITIONS.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Notes" className="sm:col-span-2">
          <Textarea rows={2} maxLength={1000} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

function Requests() {
  const [status, setStatus] = useState<'Approved' | 'Fulfilled'>('Approved');
  const list = useQuery({ queryKey: [...qk.assets, 'requests', status], queryFn: () => assetsApi.requests(status) });
  const [handing, setHanding] = useState<AssetRequest | null>(null);
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">{status === 'Approved' ? 'Approved requests waiting for you to hand over or take back.' : 'Done recently.'}</p>
        <Select className="w-40" value={status} onChange={(e) => setStatus(e.target.value as 'Approved' | 'Fulfilled')} aria-label="Show">
          <option value="Approved">To do</option>
          <option value="Fulfilled">Done</option>
        </Select>
      </div>
      {list.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : !list.data?.length ? (
        <EmptyState icon={ClipboardList} title={status === 'Approved' ? 'Nothing to do' : 'Nothing yet'} description="Approved asset requests and returns appear here." />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>For</Th>
              <Th>What</Th>
              <Th>Asked</Th>
              <Th className="text-end">
                <span className="sr-only">Action</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((r) => (
              <Tr key={r.id}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.employeeName}</p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">{r.employeeCode}</p>
                </Td>
                <Td>
                  <p className="text-sm text-zinc-900 dark:text-zinc-100">
                    {r.kind === 'Return' ? <Badge tone="amber">Return</Badge> : <Badge tone="blue">New</Badge>}{' '}
                    {r.kind === 'Return' ? r.assetLabel : r.type}
                  </p>
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {r.reason}
                    {r.neededBy ? ` · needed by ${formatDate(r.neededBy)}` : ''}
                    {r.returnBy ? ` · until ${formatDate(r.returnBy)}` : ''}
                  </p>
                </Td>
                <Td className="text-xs text-zinc-500 dark:text-zinc-400">{formatDateTimeUtc(r.createdAtUtc)}</Td>
                <Td className="text-end">
                  {r.canFulfill && (
                    <Button size="sm" onClick={() => setHanding(r)}>
                      {r.kind === 'Return' ? 'Take back' : 'Hand over'}
                    </Button>
                  )}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {handing &&
        (handing.kind === 'Return' ? (
          <TakeBackModal request={handing} label={`${handing.assetLabel} from ${handing.employeeName}`} onClose={() => setHanding(null)} />
        ) : (
          <HandOverModal request={handing} onClose={() => setHanding(null)} />
        ))}
    </Card>
  );
}

function Types() {
  const list = useQuery({ queryKey: qk.assetTypes, queryFn: assetsApi.types });
  const [editing, setEditing] = useState<AssetType | 'new' | null>(null);
  const [deleting, setDeleting] = useState<AssetType | null>(null);
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">Kinds of property and who hands them over.</p>
        <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          Add type
        </Button>
      </div>
      {!list.data?.length ? (
        <EmptyState icon={Tags} title="No asset types" description="For example Laptop, Mobile phone, SIM, Access card, Vehicle." action={<Button variant="secondary" onClick={() => setEditing('new')}>Add type</Button>} />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Type</Th>
              <Th>Handled by</Th>
              <Th>Rules</Th>
              <Th className="text-end">Stock</Th>
              <Th className="text-end">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((t) => (
              <Tr key={t.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                  {t.name} {!t.isActive && <Badge>Off</Badge>}
                </Td>
                <Td className="text-sm">{t.handlers.length ? t.handlers.join(', ') : <span className="text-zinc-500 dark:text-zinc-400">HR</span>}</Td>
                <Td>
                  <div className="flex flex-wrap gap-1">
                    {t.allowMultiple && <Badge tone="blue">More than one each</Badge>}
                    {t.returnOnExit && <Badge tone="amber">Return on exit</Badge>}
                  </div>
                </Td>
                <Td className="text-end text-sm tabular-nums">
                  {t.available} free / {t.total}
                </Td>
                <Td>
                  <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <TypeModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Type" remove={assetsApi.deleteType} invalidate={[qk.assets]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function TypeModal({ item, onClose }: { item: AssetType | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const [name, setName] = useState(item?.name ?? '');
  const [multiple, setMultiple] = useState(item?.allowMultiple ?? false);
  const [onExit, setOnExit] = useState(item?.returnOnExit ?? true);
  const [handlers, setHandlers] = useState<string[]>(item?.handlerIds ?? []);
  const [active, setActive] = useState(item?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = { name: name.trim(), allowMultiple: multiple, returnOnExit: onExit, handlerIds: handlers, isActive: active };
      return item ? assetsApi.updateType(item.id, b) : assetsApi.createType(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Type updated' : 'Type added');
      qc.invalidateQueries({ queryKey: qk.assets });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={item ? 'Edit type' : 'Add type'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => (name.trim() ? save.mutate() : setError('Give it a name.'))}>
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-5 md:grid-cols-2">
        <div className="space-y-4">
          <FormError message={error} />
          <Field label="Name" required>
            <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Laptop" />
          </Field>
          <Checkbox label="More than one per person" description="E.g. SIMs or monitors." checked={multiple} onChange={(e) => setMultiple(e.target.checked)} />
          <Checkbox label="Must be returned on exit" description="Listed on the person's separation." checked={onExit} onChange={(e) => setOnExit(e.target.checked)} />
          {item && <Checkbox label="In use" checked={active} onChange={(e) => setActive(e.target.checked)} />}
        </div>
        <Field label="Handled by" hint={handlers.length ? `${handlers.length} chosen` : 'Nobody chosen: HR hands these over'}>
          <ul className="max-h-60 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {people.data?.map((p) => (
              <li key={p.id} className="px-3 py-1.5">
                <Checkbox label={p.name} checked={handlers.includes(p.id)} onChange={(e) => setHandlers((h) => (e.target.checked ? [...h, p.id] : h.filter((x) => x !== p.id)))} />
              </li>
            ))}
          </ul>
        </Field>
      </div>
    </Modal>
  );
}

function Kits() {
  const list = useQuery({ queryKey: qk.assetKits, queryFn: assetsApi.kits });
  const [editing, setEditing] = useState<AssetKit | 'new' | null>(null);
  const [deleting, setDeleting] = useState<AssetKit | null>(null);
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <p className="text-sm text-zinc-600 dark:text-zinc-300">Sets you give together, e.g. to new joiners. Give one from the person's Assets tab.</p>
        <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          Add kit
        </Button>
      </div>
      {!list.data?.length ? (
        <EmptyState icon={Layers} title="No starter kits" description="E.g. Developer: laptop, monitor, access card." />
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {list.data.map((k) => (
            <li key={k.id} className="flex items-center justify-between gap-3 px-5 py-3">
              <div>
                <p className="font-medium text-zinc-900 dark:text-zinc-100">{k.name}</p>
                <p className="text-xs text-zinc-500">{k.types.join(', ')}</p>
              </div>
              <RowActions label={k.name} onEdit={() => setEditing(k)} onDelete={() => setDeleting(k)} />
            </li>
          ))}
        </ul>
      )}
      {editing && <KitModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Kit" remove={assetsApi.deleteKit} invalidate={[qk.assets]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function KitModal({ item, onClose }: { item: AssetKit | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const types = useQuery({ queryKey: qk.assetTypes, queryFn: assetsApi.types });
  const [name, setName] = useState(item?.name ?? '');
  const [picked, setPicked] = useState<string[]>(item?.typeIds ?? []);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => (item ? assetsApi.updateKit(item.id, { name: name.trim(), typeIds: picked }) : assetsApi.createKit({ name: name.trim(), typeIds: picked })),
    onSuccess: () => {
      toast.success(item ? 'Kit updated' : 'Kit added');
      qc.invalidateQueries({ queryKey: qk.assets });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? 'Edit kit' : 'Add kit'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => (name.trim() && picked.length ? save.mutate() : setError('Name the kit and pick what is in it.'))}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <Field label="Name" required>
          <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="Developer" />
        </Field>
        <Field label="Contains">
          <div className="space-y-1.5">
            {types.data?.filter((t) => t.isActive).map((t) => (
              <Checkbox key={t.id} label={t.name} checked={picked.includes(t.id)} onChange={(e) => setPicked((p) => (e.target.checked ? [...p, t.id] : p.filter((x) => x !== t.id)))} />
            ))}
          </div>
        </Field>
      </div>
    </Modal>
  );
}
