import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { AlertTriangle, CheckCircle2, Clock, Lock, RotateCcw, Send, Star, UserPlus, XCircle } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { helpdeskApi, PRIORITIES, type TicketDetail, type TicketPriority, type TicketStatus } from '../../api/helpdesk';
import { qk } from '../../api/keys';
import { schedulingApi } from '../../api/scheduling';
import { useUser } from '../../auth/AuthContext';
import { AttachmentList, attachmentsKey } from '../../components/attachments/AttachmentList';
import {
  Avatar,
  Button,
  Card,
  CardBody,
  CardHeader,
  Checkbox,
  ConfirmDialog,
  ErrorState,
  Field,
  FormError,
  Modal,
  PageHeader,
  PageSpinner,
  Select,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { PriorityBadge, relative, TicketStatusBadge } from './shared';

export function TicketPage() {
  const { id = '' } = useParams();
  const ticket = useQuery({ queryKey: qk.ticket(id), queryFn: () => helpdeskApi.get(id) });
  if (ticket.isLoading) return <PageSpinner />;
  if (ticket.isError || !ticket.data)
    return (
      <>
        <PageHeader title="Ticket" back={{ to: '/helpdesk', label: 'Help desk' }} />
        <Card>
          <ErrorState error={ticket.error} onRetry={() => ticket.refetch()} />
        </Card>
      </>
    );
  return <TicketView detail={ticket.data} />;
}

function useTicketAction<T>(id: string, fn: (v: T) => Promise<unknown>, success: string, after?: () => void) {
  const toast = useToast();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      if (success) toast.success(success);
      qc.invalidateQueries({ queryKey: qk.helpdesk });
      qc.invalidateQueries({ queryKey: qk.ticket(id) });
      // Closing or reopening changes whether files can still be added.
      qc.invalidateQueries({ queryKey: attachmentsKey('Ticket', id) });
      after?.();
    },
    onError: (err) => toast.error(err),
  });
}

function TicketView({ detail }: { detail: TicketDetail }) {
  const t = detail.ticket;
  const active = t.status === 'Open' || t.status === 'InProgress' || t.status === 'WaitingOnEmployee';
  const finished = t.status === 'Closed' || t.status === 'Cancelled';
  const [closing, setClosing] = useState(false);
  const [reopening, setReopening] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const cancel = useTicketAction(t.id, () => helpdeskApi.cancel(t.id), 'Ticket cancelled', () => setCancelling(false));

  return (
    <>
      <PageHeader
        back={{ to: detail.isAgent ? '/helpdesk/queue' : '/helpdesk', label: detail.isAgent ? 'Help desk queue' : 'Help desk' }}
        title={t.subject}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-mono">{t.code}</span> · {t.category} · <PriorityBadge priority={t.priority} /> <TicketStatusBadge status={t.status} />
          </span>
        }
        actions={
          detail.isRequester && (
            <>
              {t.status === 'Resolved' && (
                <>
                  <Button variant="secondary" icon={<RotateCcw className="size-4" />} onClick={() => setReopening(true)}>
                    Still a problem
                  </Button>
                  <Button variant="success" icon={<CheckCircle2 className="size-4" />} onClick={() => setClosing(true)}>
                    Confirm & close
                  </Button>
                </>
              )}
              {active && (
                <Button variant="ghost" icon={<XCircle className="size-4" />} onClick={() => setCancelling(true)}>
                  Cancel ticket
                </Button>
              )}
            </>
          )
        }
      />
      {t.status === 'Resolved' && detail.isRequester && (
        <div className="mb-5 flex items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-100">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          The team marked this resolved. Confirm it is fixed and rate the help, or tell them it is still a problem. It closes on its own in 3 days.
        </div>
      )}
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="space-y-6">
          <Card>
            <CardBody>
              <div className="mb-3 flex items-center gap-3">
                <Avatar name={t.employeeName} size="sm" />
                <div className="text-sm">
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">
                    {t.employeeName}
                    {detail.raisedBy && detail.raisedBy !== t.employeeName && <span className="font-normal text-zinc-500"> · raised by {detail.raisedBy}</span>}
                  </p>
                  <p className="text-xs text-zinc-500">{formatDateTimeUtc(t.createdAtUtc)}</p>
                </div>
              </div>
              <p className="text-sm leading-relaxed whitespace-pre-line text-zinc-800 dark:text-zinc-200">{detail.description}</p>
              <AttachmentList owner="Ticket" ownerId={t.id} emptyText="No files." className="mt-4" />
            </CardBody>
          </Card>
          <Thread detail={detail} />
          {!finished && <ReplyBox detail={detail} />}
        </div>
        <SidePanel detail={detail} />
      </div>
      {closing && <CloseModal id={t.id} onClose={() => setClosing(false)} />}
      {reopening && <ReopenModal id={t.id} onClose={() => setReopening(false)} />}
      <ConfirmDialog
        open={cancelling}
        title="Cancel this ticket?"
        message="The team stops working on it. Raise a new one if you need help later."
        confirmLabel="Cancel ticket"
        tone="danger"
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate(undefined)}
        onClose={() => setCancelling(false)}
      />
    </>
  );
}

function Thread({ detail }: { detail: TicketDetail }) {
  if (!detail.comments.length) return null;
  return (
    <ol className="space-y-3">
      {detail.comments.map((c) =>
        c.isSystem ? (
          <li key={c.id} className="flex items-center gap-2 px-2 text-xs text-zinc-500">
            <span className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />
            <span>
              {c.body} · {c.authorName} · {relative(c.createdAtUtc)}
            </span>
            <span className="h-px flex-1 bg-zinc-200 dark:bg-zinc-800" />
          </li>
        ) : (
          <li
            key={c.id}
            className={cn(
              'rounded-xl border p-4',
              c.isInternal
                ? 'border-amber-200 bg-amber-50/70 dark:border-amber-500/30 dark:bg-amber-500/10'
                : c.authorEmployeeId === detail.ticket.employeeId
                  ? 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'
                  : 'border-accent-200 bg-accent-50/50 dark:border-accent-500/25 dark:bg-accent-500/5',
            )}
          >
            <div className="mb-1.5 flex flex-wrap items-center gap-2 text-xs">
              <Avatar name={c.authorName} size="sm" />
              <span className="font-medium text-zinc-900 dark:text-zinc-100">{c.authorName}</span>
              {c.isInternal && (
                <span className="flex items-center gap-1 font-medium text-amber-700 dark:text-amber-300">
                  <Lock className="size-3" /> Internal note
                </span>
              )}
              <span className="text-zinc-500" title={formatDateTimeUtc(c.createdAtUtc)}>
                {relative(c.createdAtUtc)}
              </span>
            </div>
            <p className="text-sm whitespace-pre-line text-zinc-800 dark:text-zinc-200">{c.body}</p>
          </li>
        ),
      )}
    </ol>
  );
}

function ReplyBox({ detail }: { detail: TicketDetail }) {
  const [body, setBody] = useState('');
  const [internal, setInternal] = useState(false);
  const send = useTicketAction(detail.ticket.id, () => helpdeskApi.comment(detail.ticket.id, body.trim(), internal), '', () => setBody(''));
  return (
    <Card className={cn(internal && 'border-amber-300 dark:border-amber-500/40')}>
      <CardBody className="space-y-3">
        <Textarea
          rows={3}
          maxLength={8000}
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder={internal ? 'Note for other agents. The employee will not see it.' : detail.isAgent && !detail.isRequester ? 'Reply to the employee…' : 'Add a reply…'}
          aria-label="Reply"
        />
        <div className="flex flex-wrap items-center justify-between gap-2">
          {detail.isAgent ? <Checkbox label="Internal note" checked={internal} onChange={(e) => setInternal(e.target.checked)} /> : <span />}
          <Button icon={<Send className="size-4" />} loading={send.isPending} disabled={!body.trim()} onClick={() => send.mutate(undefined)}>
            {internal ? 'Add note' : 'Send'}
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}

function SidePanel({ detail }: { detail: TicketDetail }) {
  const t = detail.ticket;
  const active = t.status === 'Open' || t.status === 'InProgress' || t.status === 'WaitingOnEmployee';
  const [involving, setInvolving] = useState(false);
  const [resolving, setResolving] = useState(false);
  const categories = useQuery({ queryKey: qk.ticketCategories, queryFn: helpdeskApi.categories, enabled: detail.isAgent });
  const update = useTicketAction(t.id, (b: { assigneeEmployeeId: string | null; priority: TicketPriority; categoryId: string }) => helpdeskApi.update(t.id, b), 'Ticket updated');
  const setStatus = useTicketAction(t.id, (s: TicketStatus) => helpdeskApi.status(t.id, s, null), 'Status updated');
  const current = { assigneeEmployeeId: t.assigneeEmployeeId, priority: t.priority, categoryId: t.categoryId };
  const canTake = detail.isAgent && active && !t.assigneeEmployeeId;

  return (
    <div className="space-y-4">
      {detail.isAgent && active && (
        <Card>
          <CardBody className="space-y-2">
            {canTake && <TakeButton detail={detail} />}
            {t.status !== 'WaitingOnEmployee' && (
              <Button variant="secondary" className="w-full" icon={<Clock className="size-4" />} loading={setStatus.isPending} onClick={() => setStatus.mutate('WaitingOnEmployee')}>
                Waiting on employee
              </Button>
            )}
            <Button variant="success" className="w-full" icon={<CheckCircle2 className="size-4" />} onClick={() => setResolving(true)}>
              Mark resolved
            </Button>
          </CardBody>
        </Card>
      )}
      <Card>
        <CardHeader title="Details" />
        <CardBody className="space-y-4 text-sm">
          {detail.isAgent && active ? (
            <>
              <Field label="Assignee">
                <Select value={t.assigneeEmployeeId ?? ''} disabled={update.isPending} onChange={(e) => update.mutate({ ...current, assigneeEmployeeId: e.target.value || null })}>
                  <option value="">Unassigned</option>
                  {detail.assignees.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Priority">
                <Select value={t.priority} disabled={update.isPending} onChange={(e) => update.mutate({ ...current, priority: e.target.value as TicketPriority })}>
                  {PRIORITIES.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Category">
                <Select value={t.categoryId} disabled={update.isPending} onChange={(e) => update.mutate({ ...current, categoryId: e.target.value })}>
                  {categories.data?.filter((c) => c.isActive || c.id === t.categoryId).map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </Select>
              </Field>
            </>
          ) : (
            <Row label="Assignee" value={t.assignee ?? 'Not picked up yet'} />
          )}
          <Row
            label="Due"
            value={
              active ? (
                <span className={cn(t.isOverdue && 'flex items-center gap-1 font-medium text-red-600 dark:text-red-400')}>
                  {t.isOverdue && <AlertTriangle className="size-3.5" />}
                  {formatDateTimeUtc(t.dueAtUtc)}
                </span>
              ) : (
                '—'
              )
            }
          />
          <Row label="Raised" value={formatDateTimeUtc(t.createdAtUtc)} />
          {detail.firstResponseAtUtc && <Row label="First response" value={formatDateTimeUtc(detail.firstResponseAtUtc)} />}
          {detail.resolvedAtUtc && <Row label="Resolved" value={formatDateTimeUtc(detail.resolvedAtUtc)} />}
          {t.rating && (
            <Row
              label="Rating"
              value={
                <span>
                  <span className="flex text-amber-500">
                    {Array.from({ length: 5 }, (_, i) => (
                      <Star key={i} className={cn('size-4', i < t.rating! && 'fill-current')} />
                    ))}
                  </span>
                  {detail.feedback && <span className="mt-1 block text-xs text-zinc-600 dark:text-zinc-400">“{detail.feedback}”</span>}
                </span>
              }
            />
          )}
          <div>
            <div className="mb-1 flex items-center justify-between">
              <span className="text-xs text-zinc-500">Involved</span>
              {detail.isAgent && active && (
                <Button variant="ghost" size="sm" icon={<UserPlus className="size-3.5" />} onClick={() => setInvolving(true)}>
                  Add
                </Button>
              )}
            </div>
            <p className="text-zinc-800 dark:text-zinc-200">{detail.involved.length ? detail.involved.join(', ') : <span className="text-zinc-400">Nobody</span>}</p>
          </div>
        </CardBody>
      </Card>
      {involving && <InvolveModal detail={detail} onClose={() => setInvolving(false)} />}
      {resolving && <ResolveModal id={t.id} onClose={() => setResolving(false)} />}
    </div>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs text-zinc-500">{label}</p>
      <div className="text-zinc-900 dark:text-zinc-100">{value}</div>
    </div>
  );
}

/** "Take it": assign the ticket to the signed-in agent. */
function TakeButton({ detail }: { detail: TicketDetail }) {
  const t = detail.ticket;
  const mine = useUser().employeeId;
  const take = useTicketAction(t.id, () => helpdeskApi.update(t.id, { assigneeEmployeeId: mine, priority: t.priority, categoryId: t.categoryId }), 'Ticket is yours');
  if (!mine || !detail.assignees.some((a) => a.id === mine)) return null;
  return (
    <Button className="w-full" loading={take.isPending} onClick={() => take.mutate(undefined)}>
      Take this ticket
    </Button>
  );
}

function ResolveModal({ id, onClose }: { id: string; onClose: () => void }) {
  const [note, setNote] = useState('');
  const resolve = useTicketAction(id, () => helpdeskApi.status(id, 'Resolved', note.trim() || null), 'Marked resolved', onClose);
  return (
    <Modal
      open
      onClose={onClose}
      title="Mark resolved"
      description="The employee is asked to confirm and rate the help."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="success" loading={resolve.isPending} onClick={() => resolve.mutate(undefined)}>
            Mark resolved
          </Button>
        </>
      }
    >
      <Field label="What was done" hint="Optional, shown to the employee">
        <Textarea rows={3} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} placeholder="e.g. Replaced the charger" />
      </Field>
    </Modal>
  );
}

function CloseModal({ id, onClose }: { id: string; onClose: () => void }) {
  const [rating, setRating] = useState(0);
  const [feedback, setFeedback] = useState('');
  const close = useTicketAction(id, () => helpdeskApi.close(id, rating || null, feedback.trim() || null), 'Thanks! Ticket closed', onClose);
  return (
    <Modal
      open
      onClose={onClose}
      title="How was the help?"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="success" loading={close.isPending} onClick={() => close.mutate(undefined)}>
            Close ticket
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="flex justify-center gap-1" role="radiogroup" aria-label="Rating">
          {[1, 2, 3, 4, 5].map((n) => (
            <button key={n} type="button" role="radio" aria-checked={rating === n} aria-label={`${n} star${n === 1 ? '' : 's'}`} onClick={() => setRating(n)} className="p-1">
              <Star className={cn('size-8 transition', n <= rating ? 'fill-amber-400 text-amber-400' : 'text-zinc-300 hover:text-amber-300 dark:text-zinc-600')} />
            </button>
          ))}
        </div>
        <Field label="Comment" hint="Optional">
          <Textarea rows={2} maxLength={1000} value={feedback} onChange={(e) => setFeedback(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}

function ReopenModal({ id, onClose }: { id: string; onClose: () => void }) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const reopen = useMutation({
    mutationFn: () => helpdeskApi.reopen(id, reason.trim()),
    onError: (err) => setError(errorMessage(err)),
  });
  const qc = useQueryClient();
  const toast = useToast();
  return (
    <Modal
      open
      onClose={onClose}
      title="Still a problem?"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={reopen.isPending}
            onClick={() => {
              if (!reason.trim()) return setError('Say what is still wrong.');
              reopen.mutate(undefined, {
                onSuccess: () => {
                  toast.success('Ticket reopened');
                  qc.invalidateQueries({ queryKey: qk.helpdesk });
                  qc.invalidateQueries({ queryKey: qk.ticket(id) });
                  onClose();
                },
              });
            }}
          >
            Reopen
          </Button>
        </>
      }
    >
      <FormError message={error} />
      <Field label="What is still wrong" required>
        <Textarea rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
    </Modal>
  );
}

function InvolveModal({ detail, onClose }: { detail: TicketDetail; onClose: () => void }) {
  const people = useQuery({ queryKey: [...qk.attendance, 'colleagues'], queryFn: schedulingApi.colleagues });
  const [picked, setPicked] = useState<string[]>([]);
  const involve = useTicketAction(detail.ticket.id, () => helpdeskApi.involve(detail.ticket.id, picked), 'Colleagues involved', onClose);
  const choices = (people.data ?? []).filter((p) => !detail.involvedIds.includes(p.id) && p.id !== detail.ticket.employeeId);
  return (
    <Modal
      open
      onClose={onClose}
      title="Involve colleagues"
      description="They are notified and can read and reply on this ticket."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={involve.isPending} disabled={!picked.length} onClick={() => involve.mutate(undefined)}>
            Involve {picked.length || ''}
          </Button>
        </>
      }
    >
      <ul className="max-h-72 divide-y divide-zinc-100 overflow-y-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
        {choices.map((p) => (
          <li key={p.id} className="px-3 py-1.5">
            <Checkbox label={p.name} checked={picked.includes(p.id)} onChange={(e) => setPicked((a) => (e.target.checked ? [...a, p.id] : a.filter((x) => x !== p.id)))} />
          </li>
        ))}
      </ul>
    </Modal>
  );
}
