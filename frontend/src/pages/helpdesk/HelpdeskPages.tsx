import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { AlertTriangle, Headset, Inbox, LifeBuoy, ListChecks, Plus, Search, Tags, UserCheck, UserX } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { helpdeskApi, type QueueScope, type QueueStatus, type TicketCategory } from '../../api/helpdesk';
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
import { NewTicketModal, TicketTable } from './shared';

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

type QueueTab = 'tickets' | 'categories';

/** Agents and HR: tickets to handle, with counts and filters. HR also manages categories here. */
export function HelpdeskQueuePage() {
  const user = useUser();
  const hr = isHr(user);
  const [params, setParams] = useSearchParams();
  const tab: QueueTab = hr && params.get('tab') === 'categories' ? 'categories' : 'tickets';
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
      {hr && (
        <Tabs<QueueTab>
          className="mb-5"
          value={tab}
          onChange={(t) => setParams(t === 'tickets' ? {} : { tab: t }, { replace: true })}
          items={[
            { value: 'tickets', label: 'Tickets', icon: <ListChecks className="size-4" /> },
            { value: 'categories', label: 'Categories', icon: <Tags className="size-4" /> },
          ]}
        />
      )}
      {tab === 'tickets' ? <Queue /> : <Categories />}
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
        'flex items-center gap-3 rounded-xl border bg-white p-4 text-left transition dark:bg-zinc-900',
        scope === key && status === 'open' ? 'border-accent-500 ring-2 ring-accent-500/20' : 'border-zinc-200 hover:border-zinc-300 dark:border-zinc-800',
      )}
    >
      <span className={cn('flex size-9 items-center justify-center rounded-lg', tone)}>
        <Icon className="size-4" />
      </span>
      <span>
        <span className="block text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value ?? '–'}</span>
        <span className="block text-xs text-zinc-500">{label}</span>
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
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
            <Input className="pl-9" placeholder="HD-0042, subject or name" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Search tickets" />
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
  return (
    <Card>
      <div className="flex items-center justify-between border-b border-zinc-100 px-5 py-4 dark:border-zinc-800">
        <div>
          <h2 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Categories</h2>
          <p className="text-xs text-zinc-500">What people can ask about, who answers and how fast.</p>
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
              <Th className="text-right">Target</Th>
              <Th className="text-right">Open</Th>
              <Th className="text-right">
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
                  {c.description && <p className="text-xs text-zinc-500">{c.description}</p>}
                </Td>
                <Td className="text-sm">{c.agents.length ? c.agents.join(', ') : <span className="text-zinc-400">HR only</span>}</Td>
                <Td className="text-right tabular-nums">{c.targetHours} h</Td>
                <Td className="text-right tabular-nums">{c.openTickets}</Td>
                <Td>
                  <RowActions label={c.name} onEdit={() => setEditing(c)} onDelete={() => setDeleting(c)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <CategoryModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
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
  const [filter, setFilter] = useState('');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = { name: name.trim(), description: description.trim() || null, targetHours: Number(hours), agentIds: agents, isActive: active };
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
