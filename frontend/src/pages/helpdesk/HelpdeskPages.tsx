import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { AlertTriangle, BarChart3, Download, Headset, Inbox, LifeBuoy, ListChecks, Plus, Search, Tags, Type, UserCheck, UserX } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { helpdeskApi, PRIORITIES, TICKET_STATUS_LABELS, type HelpdeskLabels, type QueueScope, type QueueStatus, type ReportRow, type TicketAssignment, type TicketCategory, type TicketPriority, type TicketStatus, type TicketType } from '../../api/helpdesk';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  Checkbox,
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
import { useDebounced } from '../../lib/hooks';
import { DeleteConfirm, RowActions } from '../settings/shared';
import { NewTicketModal, TicketTable, useHelpdeskLabels } from './shared';
import { toIsoDate } from '../../lib/format';

/** Everyone: tickets I raised, am the subject of, or was pulled into. */
export function MyTicketsPage() {
  const user = useUser();
  const navigate = useNavigate();
  const [raising, setRaising] = useState(false);
  const list = useQuery({ queryKey: qk.ticketsMy, queryFn: helpdeskApi.my });
  const agent = useQuery({ queryKey: qk.helpdeskMe, queryFn: helpdeskApi.me });
  return (
    <>
      <PageHeader
        title="Help desk"
        description="Ask IT, Admin, HR or any other team for help and follow the answer here."
        actions={
          <>
            {agent.data?.isAgent && (
              <Button variant="secondary" icon={<Headset className="size-4" />} onClick={() => navigate('/helpdesk/queue')}>
                Agent queue
              </Button>
            )}
            <Button icon={<Plus className="size-4" />} onClick={() => setRaising(true)}>
              Raise ticket
            </Button>
          </>
        }
      />
      <Card>
        {list.isLoading ? (
          <SkeletonRows rows={4} cols={5} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !list.data?.length ? (
          <EmptyState
            icon={LifeBuoy}
            title="No tickets yet"
            description="Laptop trouble, a broken chair, a payslip question: raise a ticket and the right team picks it up."
            action={<Button onClick={() => setRaising(true)}>Raise ticket</Button>}
          />
        ) : (
          <TicketTable tickets={list.data} showRequester={false} />
        )}
      </Card>
      {raising && <NewTicketModal hr={isHr(user)} onClose={() => setRaising(false)} />}
    </>
  );
}

type QueueTab = 'tickets' | 'reports' | 'categories' | 'labels';

/** Agents and HR: tickets to handle, with counts and filters. HR also manages categories here. */
export function HelpdeskQueuePage() {
  const user = useUser();
  const hr = isHr(user);
  const [params, setParams] = useSearchParams();
  const requested = (params.get('tab') as QueueTab | null) ?? 'tickets';
  const tab: QueueTab = !hr && (requested === 'categories' || requested === 'labels') ? 'tickets' : requested;
  const [raising, setRaising] = useState(false);
  return (
    <>
      <PageHeader
        title="Help desk queue"
        description="Tickets for the categories you handle. Take one, reply, and resolve it."
        back={{ to: '/helpdesk', label: 'Help desk' }}
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setRaising(true)}>
            Raise ticket
          </Button>
        }
      />
      <Tabs<QueueTab>
        className="mb-5"
        value={tab}
        onChange={(t) => setParams(t === 'tickets' ? {} : { tab: t }, { replace: true })}
        items={[
          { value: 'tickets', label: 'Tickets', icon: <ListChecks className="size-4" /> },
          { value: 'reports', label: 'Reports', icon: <BarChart3 className="size-4" /> },
          ...(hr
            ? ([
                { value: 'categories', label: 'Categories', icon: <Tags className="size-4" /> },
                { value: 'labels', label: 'Names', icon: <Type className="size-4" /> },
              ] as const)
            : []),
        ]}
      />
      {tab === 'tickets' && <Queue />}
      {tab === 'reports' && <Reports />}
      {tab === 'categories' && <Categories />}
      {tab === 'labels' && <Labels />}
      {raising && <NewTicketModal hr={hr} onClose={() => setRaising(false)} />}
    </>
  );
}

function Queue() {
  const [scope, setScope] = useState<QueueScope>('all');
  const [status, setStatus] = useState<QueueStatus>('open');
  const [categoryId, setCategoryId] = useState('');
  const [search, setSearch] = useState('');
  const q = useDebounced(search.trim(), 300);
  const categories = useQuery({ queryKey: qk.ticketCategories, queryFn: helpdeskApi.categories });
  const summary = useQuery({ queryKey: qk.helpdeskSummary, queryFn: helpdeskApi.summary });
  const query = { scope, status, categoryId: categoryId || undefined, search: q || undefined };
  const list = useQuery({ queryKey: [...qk.helpdesk, 'queue', query], queryFn: () => helpdeskApi.queue(query), placeholderData: keepPreviousData });
  const s = summary.data;
  const tile = (key: QueueScope, label: string, value: number | undefined, Icon: typeof Inbox, tone: string) => (
    <button
      type="button"
      onClick={() => {
        setScope(key);
        setStatus('open');
      }}
      aria-pressed={scope === key && status === 'open'}
      className={cn(
        'flex items-center gap-3 rounded-xl border bg-white p-4 text-start transition dark:bg-zinc-900',
        scope === key && status === 'open' ? 'border-accent-500 ring-2 ring-accent-500/20' : 'border-zinc-200 hover:border-zinc-300 dark:border-zinc-800',
      )}
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
      <div className="mb-5 grid grid-cols-2 gap-3 md:grid-cols-4">
        {tile('all', 'Open tickets', s ? s.open + s.inProgress + s.waiting : undefined, Inbox, 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300')}
        {tile('mine', 'Assigned to me', s?.mine, UserCheck, 'bg-accent-100 text-accent-700 dark:bg-accent-500/15 dark:text-accent-300')}
        {tile('unassigned', 'Unassigned', s?.unassigned, UserX, 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300')}
        {tile('overdue', 'Overdue', s?.overdue, AlertTriangle, 'bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300')}
      </div>
      <Card>
        <div className="flex flex-col gap-3 border-b border-zinc-100 p-4 sm:flex-row sm:items-center dark:border-zinc-800">
          <div className="relative flex-1 sm:max-w-xs">
            <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" />
            <Input className="ps-9" placeholder="HD-0042, subject or name" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Search tickets" />
          </div>
          <Select className="sm:w-48" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} aria-label="Category">
            <option value="">All categories</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
          <Select className="sm:w-44" value={status} onChange={(e) => setStatus(e.target.value as QueueStatus)} aria-label="Status">
            <option value="open">Open</option>
            <option value="resolved">Resolved</option>
            <option value="closed">Closed</option>
            <option value="any">Any status</option>
          </Select>
          <Select className="sm:w-40" value={scope} onChange={(e) => setScope(e.target.value as QueueScope)} aria-label="Show">
            <option value="all">Everyone’s</option>
            <option value="mine">Mine</option>
            <option value="unassigned">Unassigned</option>
            <option value="overdue">Overdue</option>
          </Select>
        </div>
        {list.isLoading ? (
          <SkeletonRows rows={5} cols={6} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !list.data?.length ? (
          <EmptyState icon={Inbox} title="Nothing here" description="No tickets match these filters." />
        ) : (
          <TicketTable tickets={list.data} showRequester />
        )}
      </Card>
    </>
  );
}

function Categories() {
  const list = useQuery({ queryKey: qk.ticketCategories, queryFn: helpdeskApi.categories });
  const [editing, setEditing] = useState<TicketCategory | 'new' | null>(null);
  const [deleting, setDeleting] = useState<TicketCategory | null>(null);
  const [typesOf, setTypesOf] = useState<string | null>(null);
  const typesCategory = list.data?.find((c) => c.id === typesOf);
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <div>
          <h2 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Categories</h2>
          <p className="text-xs text-zinc-500 dark:text-zinc-400">What people can ask about, who answers and how fast.</p>
        </div>
        <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          Add category
        </Button>
      </div>
      {list.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={Tags}
          title="No categories"
          description="For example IT support, Admin & facilities, Payroll queries. Add agents to each."
          action={<Button variant="secondary" onClick={() => setEditing('new')}>Add category</Button>}
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Category</Th>
              <Th>Agents</Th>
              <Th>Types</Th>
              <Th className="text-end">Target</Th>
              <Th className="text-end">Open</Th>
              <Th className="text-end">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((c) => (
              <Tr key={c.id}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">
                    {c.name} {!c.isActive && <Badge>Off</Badge>}
                  </p>
                  {c.description && <p className="text-xs text-zinc-500 dark:text-zinc-400">{c.description}</p>}
                </Td>
                <Td className="text-sm">
                  {c.agents.length ? c.agents.join(', ') : <span className="text-zinc-500 dark:text-zinc-400">HR only</span>}
                  <p className="text-xs text-zinc-500 dark:text-zinc-400">
                    {c.assignmentMode === 'RoundRobin' ? 'Assigned in turn' : c.assignmentMode === 'LeastBusy' ? 'Assigned to least busy' : 'Agents take tickets'}
                    {c.lead ? ` · Lead: ${c.lead}` : ''}
                  </p>
                </Td>
                <Td className="text-sm">
                  <Button variant="ghost" size="sm" onClick={() => setTypesOf(c.id)}>
                    {c.types.length ? `${c.types.length} type${c.types.length === 1 ? '' : 's'}` : 'Add types'}
                  </Button>
                </Td>
                <Td className="text-end tabular-nums">{c.targetHours} h</Td>
                <Td className="text-end tabular-nums">{c.openTickets}</Td>
                <Td>
                  <RowActions label={c.name} onEdit={() => setEditing(c)} onDelete={() => setDeleting(c)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <CategoryModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {typesCategory && <TypesModal category={typesCategory} onClose={() => setTypesOf(null)} />}
      <DeleteConfirm item={deleting} name="Category" remove={helpdeskApi.deleteCategory} invalidate={[qk.ticketCategories]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function CategoryModal({ item, onClose }: { item: TicketCategory | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const [name, setName] = useState(item?.name ?? '');
  const [description, setDescription] = useState(item?.description ?? '');
  const [hours, setHours] = useState(String(item?.targetHours ?? 24));
  const [agents, setAgents] = useState<string[]>(item?.agentIds ?? []);
  const [active, setActive] = useState(item?.isActive ?? true);
  const [lead, setLead] = useState(item?.leadEmployeeId ?? '');
  const [mode, setMode] = useState<TicketAssignment>(item?.assignmentMode ?? 'Manual');
  const [agentCanClose, setAgentCanClose] = useState(item?.agentCanClose ?? false);
  const [autoClose, setAutoClose] = useState(String(item?.autoCloseDays ?? 3));
  const [reopen, setReopen] = useState(String(item?.reopenDays ?? 7));
  const [filter, setFilter] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = {
        name: name.trim(),
        description: description.trim() || null,
        targetHours: Number(hours),
        agentIds: agents,
        isActive: active,
        leadEmployeeId: lead || null,
        assignmentMode: mode,
        agentCanClose,
        autoCloseDays: Number(autoClose) || 0,
        reopenDays: Number(reopen) || 0,
      };
      return item ? helpdeskApi.updateCategory(item.id, b) : helpdeskApi.createCategory(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Category updated' : 'Category added');
      qc.invalidateQueries({ queryKey: qk.helpdesk });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const shown = (people.data ?? []).filter((p) => !filter.trim() || p.name.toLowerCase().includes(filter.trim().toLowerCase()));
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={item ? 'Edit category' : 'Add category'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!name.trim()) return setError('Give the category a name.');
              if (!(Number(hours) >= 1)) return setError('Set a target of at least 1 hour.');
              if (mode !== 'Manual' && !agents.length) return setError('Automatic assignment needs at least one agent.');
              save.mutate();
            }}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="grid gap-5 md:grid-cols-2">
        <div className="space-y-4">
          <FormError message={error} />
          <Field label="Name" required>
            <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="IT support" />
          </Field>
          <Field label="Description" hint="Shown when raising a ticket">
            <Textarea rows={2} maxLength={500} value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
          <Field label="Resolve within (hours)" hint="Tickets past this show as overdue">
            <Input type="number" min={1} max={2000} value={hours} onChange={(e) => setHours(e.target.value)} className="w-32" />
          </Field>
          <Field label="Lead" hint="Hears when a ticket goes overdue. HR when blank.">
            <Select value={lead} onChange={(e) => setLead(e.target.value)}>
              <option value="">No lead</option>
              {people.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="New tickets go to">
            <Select value={mode} onChange={(e) => setMode(e.target.value as TicketAssignment)}>
              <option value="Manual">Nobody: agents take them</option>
              <option value="RoundRobin">Each agent in turn</option>
              <option value="LeastBusy">The agent with the fewest open tickets</option>
            </Select>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Auto-close after (days)" hint="Resolved tickets; 0 = never">
              <Input type="number" min={0} max={90} value={autoClose} onChange={(e) => setAutoClose(e.target.value)} />
            </Field>
            <Field label="Reopen within (days)" hint="After closing; 0 = never">
              <Input type="number" min={0} max={365} value={reopen} onChange={(e) => setReopen(e.target.value)} />
            </Field>
          </div>
          <Checkbox label="Agents may close tickets" description="Without waiting for the employee to confirm." checked={agentCanClose} onChange={(e) => setAgentCanClose(e.target.checked)} />
          {item && <Checkbox label="Open for new tickets" checked={active} onChange={(e) => setActive(e.target.checked)} />}
        </div>
        <Field label="Agents" hint={agents.length ? `${agents.length} chosen. HR can always help too.` : 'Nobody chosen: HR handles these tickets.'}>
          <div className="space-y-2">
            <Input placeholder="Search people" value={filter} onChange={(e) => setFilter(e.target.value)} aria-label="Search people" />
            <ul className="max-h-60 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
              {shown.map((p) => (
                <li key={p.id} className="px-3 py-1.5">
                  <Checkbox
                    label={p.name}
                    checked={agents.includes(p.id)}
                    onChange={(e) => setAgents((a) => (e.target.checked ? [...a, p.id] : a.filter((x) => x !== p.id)))}
                  />
                </li>
              ))}
            </ul>
          </div>
        </Field>
      </div>
    </Modal>
  );
}

function TypesModal({ category, onClose }: { category: TicketCategory; onClose: () => void }) {
  const [editing, setEditing] = useState<TicketType | 'new' | null>(null);
  const [deleting, setDeleting] = useState<TicketType | null>(null);
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={`${category.name}: types`}
      description="Narrow a category down, e.g. Laptop, Email and VPN under IT. Each type can have its own target and agents. Once a category has types, people must pick one."
      footer={<Button onClick={onClose}>Done</Button>}
    >
      <div className="space-y-3">
        {category.types.length === 0 ? (
          <p className="text-sm text-zinc-500 dark:text-zinc-400">No types yet. Tickets use the category's target and agents.</p>
        ) : (
          <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {category.types.map((t) => (
              <li key={t.id} className="flex items-center justify-between gap-3 px-3 py-2.5">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                    {t.name} {!t.isActive && <Badge>Off</Badge>}
                  </p>
                  <p className="text-xs text-zinc-500">
                    {t.targetHours ? `${t.targetHours} h target` : `Category target (${category.targetHours} h)`} ·{' '}
                    {t.agents.length ? t.agents.join(', ') : 'Category agents'}
                  </p>
                </div>
                <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
              </li>
            ))}
          </ul>
        )}
        <Button variant="secondary" size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
          Add type
        </Button>
      </div>
      {editing && <TypeModal categoryId={category.id} item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Type" remove={helpdeskApi.deleteType} invalidate={[qk.ticketCategories]} onClose={() => setDeleting(null)} />
    </Modal>
  );
}

function TypeModal({ categoryId, item, onClose }: { categoryId: string; item: TicketType | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const people = useEmployeeLookup();
  const [name, setName] = useState(item?.name ?? '');
  const [hours, setHours] = useState(item?.targetHours ? String(item.targetHours) : '');
  const [agents, setAgents] = useState<string[]>(item?.agentIds ?? []);
  const [active, setActive] = useState(item?.isActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = { name: name.trim(), targetHours: hours ? Number(hours) : null, agentIds: agents, isActive: active };
      return item ? helpdeskApi.updateType(item.id, b) : helpdeskApi.createType(categoryId, b);
    },
    onSuccess: () => {
      toast.success(item ? 'Type updated' : 'Type added');
      qc.invalidateQueries({ queryKey: qk.ticketCategories });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={item ? 'Edit type' : 'Add type'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} onClick={() => (name.trim() ? save.mutate() : setError('Give the type a name.'))}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Name" required>
            <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} placeholder="VPN" />
          </Field>
          <Field label="Target (hours)" hint="Blank uses the category's">
            <Input type="number" min={1} max={2000} value={hours} onChange={(e) => setHours(e.target.value)} />
          </Field>
        </div>
        <Field label="Agents" hint={agents.length ? `${agents.length} chosen` : "None: the category's agents handle it"}>
          <ul className="max-h-52 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {people.data?.map((p) => (
              <li key={p.id} className="px-3 py-1.5">
                <Checkbox label={p.name} checked={agents.includes(p.id)} onChange={(e) => setAgents((a) => (e.target.checked ? [...a, p.id] : a.filter((x) => x !== p.id)))} />
              </li>
            ))}
          </ul>
        </Field>
        {item && <Checkbox label="Open for new tickets" checked={active} onChange={(e) => setActive(e.target.checked)} />}
      </div>
    </Modal>
  );
}

function hours(h: number | null) {
  if (h === null) return '—';
  return h < 1 ? `${Math.round(h * 60)} min` : h < 48 ? `${h} h` : `${(h / 24).toFixed(1)} d`;
}

function ReportTable({ title, rows }: { title: string; rows: ReportRow[] }) {
  if (!rows.length) return null;
  return (
    <Card>
      <div className="border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">{title}</h3>
      </div>
      <Table>
        <THead>
          <tr>
            <Th>Name</Th>
            <Th className="text-end">Raised</Th>
            <Th className="text-end">Resolved</Th>
            <Th className="text-end">Open</Th>
            <Th className="text-end">First answer</Th>
            <Th className="text-end">Time to fix</Th>
            <Th className="text-end">Within target</Th>
            <Th className="text-end">Rating</Th>
          </tr>
        </THead>
        <TBody>
          {rows.map((r) => (
            <Tr key={r.id ?? r.name}>
              <Td className="font-medium text-zinc-900 dark:text-zinc-100">{r.name}</Td>
              <Td className="text-end tabular-nums">{r.raised}</Td>
              <Td className="text-end tabular-nums">{r.resolved}</Td>
              <Td className="text-end tabular-nums">{r.open}</Td>
              <Td className="text-end tabular-nums">{hours(r.avgFirstResponseHours)}</Td>
              <Td className="text-end tabular-nums">{hours(r.avgResolutionHours)}</Td>
              <Td className={cn('text-end tabular-nums', r.slaMetPercent !== null && r.slaMetPercent < 80 && 'font-medium text-red-600 dark:text-red-400')}>
                {r.slaMetPercent === null ? '—' : `${r.slaMetPercent}%`}
              </Td>
              <Td className="text-end tabular-nums">{r.avgRating === null ? '—' : `${r.avgRating}★ (${r.rated})`}</Td>
            </Tr>
          ))}
        </TBody>
      </Table>
    </Card>
  );
}

function Reports() {
  const labels = useHelpdeskLabels();
  const now = new Date();
  const [from, setFrom] = useState(toIsoDate(new Date(now.getFullYear(), now.getMonth(), 1)));
  const [to, setTo] = useState(toIsoDate(now));
  const [exporting, setExporting] = useState(false);
  const toast = useToast();
  const report = useQuery({ queryKey: qk.helpdeskReport(from, to), queryFn: () => helpdeskApi.report(from, to), enabled: !!from && !!to && from <= to });
  const r = report.data;
  const tile = (label: string, value: string, hint?: string) => (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="mt-1 text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value}</p>
      {hint && <p className="text-xs text-zinc-500">{hint}</p>}
    </div>
  );
  const bars = (title: string, data: Record<string, number>, name: (k: string) => string) => {
    const max = Math.max(1, ...Object.values(data));
    return (
      <Card className="p-5">
        <h3 className="mb-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">{title}</h3>
        <ul className="space-y-2">
          {Object.entries(data).map(([k, v]) => (
            <li key={k} className="grid grid-cols-[130px_1fr_32px] items-center gap-3 text-sm">
              <span className="truncate text-zinc-600 dark:text-zinc-300">{name(k)}</span>
              <span className="h-2 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                <span className="block h-full rounded-full bg-accent-500" style={{ width: `${(v / max) * 100}%` }} />
              </span>
              <span className="text-end text-zinc-900 tabular-nums dark:text-zinc-100">{v}</span>
            </li>
          ))}
        </ul>
      </Card>
    );
  };
  return (
    <div className="space-y-5">
      <Card className="flex flex-wrap items-end gap-3 p-4">
        <Field label="From">
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </Field>
        <Field label="To">
          <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
        </Field>
        <Button
          variant="secondary"
          className="ms-auto"
          icon={<Download className="size-4" />}
          loading={exporting}
          onClick={() => {
            setExporting(true);
            helpdeskApi
              .exportExcel(from, to)
              .catch((err) => toast.error(err))
              .finally(() => setExporting(false));
          }}
        >
          Export to Excel
        </Button>
      </Card>
      {report.isLoading ? (
        <Card>
          <SkeletonRows rows={4} cols={6} />
        </Card>
      ) : report.isError ? (
        <Card>
          <ErrorState error={report.error} onRetry={() => report.refetch()} />
        </Card>
      ) : r && r.total.raised === 0 ? (
        <Card>
          <EmptyState icon={BarChart3} title="No tickets in this period" />
        </Card>
      ) : (
        r && (
          <>
            <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
              {tile('Raised', String(r.total.raised), `${r.total.open} still open`)}
              {tile('Resolved', String(r.total.resolved))}
              {tile('First answer', hours(r.total.avgFirstResponseHours), 'on average')}
              {tile('Within target', r.total.slaMetPercent === null ? '—' : `${r.total.slaMetPercent}%`, `average fix ${hours(r.total.avgResolutionHours)}`)}
              {tile('Rating', r.total.avgRating === null ? '—' : `${r.total.avgRating}★`, `${r.total.rated} rated`)}
            </div>
            <div className="grid gap-5 lg:grid-cols-2">
              {bars('By priority', r.byPriority, (k) => labels.priority(k as TicketPriority))}
              {bars('By status', r.byStatus, (k) => labels.status(k as TicketStatus))}
            </div>
            <ReportTable title="By category" rows={r.categories} />
            <ReportTable title="By agent" rows={r.agents} />
          </>
        )
      )}
    </div>
  );
}

/** HR: the company's own names for statuses and priorities. */
function Labels() {
  const toast = useToast();
  const qc = useQueryClient();
  const current = useQuery({ queryKey: qk.helpdeskLabels, queryFn: helpdeskApi.labels });
  const [draft, setDraft] = useState<HelpdeskLabels | null>(null);
  const value = draft ?? current.data;
  const save = useMutation({
    mutationFn: () => helpdeskApi.saveLabels(value!),
    onSuccess: (saved) => {
      qc.setQueryData(qk.helpdeskLabels, saved);
      setDraft(null);
      toast.success('Names saved');
    },
    onError: (err) => toast.error(err),
  });
  if (!value) return <Card><SkeletonRows rows={4} cols={2} /></Card>;
  const statuses = Object.keys(TICKET_STATUS_LABELS) as TicketStatus[];
  return (
    <Card className="max-w-3xl p-5">
      <p className="mb-4 text-sm text-zinc-600 dark:text-zinc-300">Use the words your teams use. Only the names change; how tickets move stays the same.</p>
      <div className="grid gap-6 sm:grid-cols-2">
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Statuses</h3>
          {statuses.map((s) => (
            <Field key={s} label={TICKET_STATUS_LABELS[s]}>
              <Input value={value.statuses[s]} maxLength={40} onChange={(e) => setDraft({ ...value, statuses: { ...value.statuses, [s]: e.target.value } })} />
            </Field>
          ))}
        </div>
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Priorities</h3>
          {PRIORITIES.map((p) => (
            <Field key={p} label={p}>
              <Input value={value.priorities[p]} maxLength={40} onChange={(e) => setDraft({ ...value, priorities: { ...value.priorities, [p]: e.target.value } })} />
            </Field>
          ))}
        </div>
      </div>
      <div className="mt-5 flex justify-end">
        <Button loading={save.isPending} disabled={!draft} onClick={() => save.mutate()}>
          Save names
        </Button>
      </div>
    </Card>
  );
}
