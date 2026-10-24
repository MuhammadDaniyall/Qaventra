import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, MessageSquare, Star } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { helpdeskApi, PRIORITIES, TICKET_STATUS_LABELS, type TicketListItem, type TicketPriority, type TicketStatus } from '../../api/helpdesk';
import { qk } from '../../api/keys';
import { Badge, Button, Field, FormError, Input, Modal, Select, Table, TBody, Td, Textarea, Th, THead, Tr, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';

const STATUS_TONES: Record<TicketStatus, 'blue' | 'accent' | 'amber' | 'green' | 'gray' | 'red'> = {
  Open: 'blue',
  InProgress: 'accent',
  WaitingOnEmployee: 'amber',
  Resolved: 'green',
  Closed: 'gray',
  Cancelled: 'gray',
};

const PRIORITY_TONES: Record<TicketPriority, 'gray' | 'blue' | 'amber' | 'red'> = { Low: 'gray', Medium: 'blue', High: 'amber', Urgent: 'red' };

/** The company's names for statuses and priorities (defaults until loaded). */
export function useHelpdeskLabels() {
  const labels = useQuery({ queryKey: qk.helpdeskLabels, queryFn: helpdeskApi.labels, staleTime: 5 * 60 * 1000 });
  return {
    status: (s: TicketStatus) => labels.data?.statuses[s] ?? TICKET_STATUS_LABELS[s],
    priority: (p: TicketPriority) => labels.data?.priorities[p] ?? p,
  };
}

export function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const labels = useHelpdeskLabels();
  return (
    <Badge tone={STATUS_TONES[status]} dot>
      {labels.status(status)}
    </Badge>
  );
}

export function PriorityBadge({ priority }: { priority: TicketPriority }) {
  const labels = useHelpdeskLabels();
  return <Badge tone={PRIORITY_TONES[priority]}>{labels.priority(priority)}</Badge>;
}

/** "in 3 h", "2 d ago" from a UTC timestamp. */
export function relative(utc: string) {
  const diff = new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(utc) ? utc : `${utc}Z`).getTime() - Date.now();
  const abs = Math.abs(diff);
  const text = abs < 3_600_000 ? `${Math.max(1, Math.round(abs / 60_000))} min` : abs < 86_400_000 ? `${Math.round(abs / 3_600_000)} h` : `${Math.round(abs / 86_400_000)} d`;
  return diff >= 0 ? `in ${text}` : `${text} ago`;
}

export function TicketTable({ tickets, showRequester }: { tickets: TicketListItem[]; showRequester: boolean }) {
  const navigate = useNavigate();
  const open = (t: TicketListItem) => t.status === 'Open' || t.status === 'InProgress' || t.status === 'WaitingOnEmployee';
  return (
    <Table>
      <THead>
        <tr>
          <Th>Ticket</Th>
          {showRequester && <Th>Raised for</Th>}
          <Th>Status</Th>
          <Th>Assignee</Th>
          <Th>Due</Th>
          <Th className="text-end">Updated</Th>
        </tr>
      </THead>
      <TBody>
        {tickets.map((t) => (
          <Tr key={t.id} onClick={() => navigate(`/helpdesk/${t.id}`)} className="cursor-pointer">
            <Td>
              <p className="font-medium text-zinc-900 dark:text-zinc-100">{t.subject}</p>
              <p className="flex flex-wrap items-center gap-1.5 text-xs text-zinc-500">
                <span className="font-mono">{t.code}</span> · {t.category}
                {t.type ? ` · ${t.type}` : ''} · <PriorityBadge priority={t.priority} />
                {t.replies > 0 && (
                  <span className="flex items-center gap-0.5">
                    <MessageSquare className="size-3" /> {t.replies}
                  </span>
                )}
              </p>
            </Td>
            {showRequester && (
              <Td>
                <p className="text-zinc-900 dark:text-zinc-100">{t.employeeName}</p>
                <p className="text-xs text-zinc-500">{t.department ?? t.employeeCode}</p>
              </Td>
            )}
            <Td>
              <TicketStatusBadge status={t.status} />
              {t.rating && (
                <span className="mt-1 flex items-center gap-0.5 text-xs text-amber-600">
                  {Array.from({ length: t.rating }, (_, i) => (
                    <Star key={i} className="size-3 fill-current" />
                  ))}
                </span>
              )}
            </Td>
            <Td className="text-sm">{t.assignee ?? <span className="text-zinc-500">Unassigned</span>}</Td>
            <Td className="text-sm">
              {open(t) ? (
                <span className={cn('flex items-center gap-1', t.isOverdue ? 'font-medium text-red-600 dark:text-red-400' : 'text-zinc-600 dark:text-zinc-300')} title={formatDateTimeUtc(t.dueAtUtc)}>
                  {t.isOverdue && <AlertTriangle className="size-3.5" />}
                  {t.isOverdue ? `Overdue ${relative(t.dueAtUtc).replace(' ago', '')}` : relative(t.dueAtUtc)}
                </span>
              ) : (
                <span className="text-zinc-500 dark:text-zinc-400">—</span>
              )}
            </Td>
            <Td className="text-end text-xs text-zinc-500" title={formatDateTimeUtc(t.lastActivityAtUtc)}>
              {relative(t.lastActivityAtUtc)}
            </Td>
          </Tr>
        ))}
      </TBody>
    </Table>
  );
}

/** Raise a ticket. HR can raise one for someone else. */
export function NewTicketModal({ hr, onClose }: { hr: boolean; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const categories = useQuery({ queryKey: qk.ticketCategories, queryFn: helpdeskApi.categories });
  const people = useEmployeeLookup(hr);
  const labels = useHelpdeskLabels();
  const active = (categories.data ?? []).filter((c) => c.isActive);
  const [categoryId, setCategoryId] = useState('');
  const [typeId, setTypeId] = useState('');
  const [forId, setForId] = useState('');
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<TicketPriority>('Medium');
  const [error, setError] = useState<string | null>(null);
  const chosen = active.find((c) => c.id === categoryId);
  const types = (chosen?.types ?? []).filter((t) => t.isActive);
  const type = types.find((t) => t.id === typeId);
  const create = useMutation({
    mutationFn: () => helpdeskApi.create({ forEmployeeId: forId || null, categoryId, typeId: typeId || null, subject: subject.trim(), description: description.trim(), priority }),
    onSuccess: (t) => {
      toast.success(`Ticket ${t.code} raised`, 'You can attach screenshots or files on the ticket.');
      qc.invalidateQueries({ queryKey: qk.helpdesk });
      onClose();
      navigate(`/helpdesk/${t.id}`);
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Raise a ticket"
      description="Tell the right team what you need. You will be notified of every reply."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={create.isPending}
            onClick={() => {
              setError(null);
              if (!categoryId) return setError('Choose what it is about.');
              if (types.length && !typeId) return setError('Choose the kind of problem.');
              if (!subject.trim() || !description.trim()) return setError('Add a subject and describe the problem.');
              create.mutate();
            }}
          >
            Raise ticket
          </Button>
        </>
      }
    >
      {categories.data && !active.length ? (
        <p className="text-sm text-zinc-600 dark:text-zinc-300">HR has not set up any help desk categories yet.</p>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <FormError message={error} />
          </div>
          <Field label="About" required hint={chosen && !types.length ? `Usually resolved within ${chosen.targetHours} h` : chosen?.description ?? undefined}>
            <Select
              value={categoryId}
              onChange={(e) => {
                setCategoryId(e.target.value);
                setTypeId('');
              }}
            >
              <option value="">Choose…</option>
              {active.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          </Field>
          {types.length > 0 && (
            <Field label="Kind" required hint={type ? `Usually resolved within ${type.targetHours ?? chosen!.targetHours} h` : undefined}>
              <Select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
                <option value="">Choose…</option>
                {types.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          <Field label="Priority">
            <Select value={priority} onChange={(e) => setPriority(e.target.value as TicketPriority)}>
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>
                  {labels.priority(p)}
                </option>
              ))}
            </Select>
          </Field>
          {hr && (
            <Field label="Raise for" hint="Blank raises it for yourself" className="sm:col-span-2">
              <Select value={forId} onChange={(e) => setForId(e.target.value)}>
                <option value="">Myself</option>
                {people.data?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          <Field label="Subject" required className="sm:col-span-2">
            <Input value={subject} maxLength={200} onChange={(e) => setSubject(e.target.value)} placeholder="e.g. Laptop will not charge" />
          </Field>
          <Field label="Details" required className="sm:col-span-2">
            <Textarea rows={5} maxLength={8000} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What happened, since when, what you have tried" />
          </Field>
        </div>
      )}
    </Modal>
  );
}
