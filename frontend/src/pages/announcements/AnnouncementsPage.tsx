import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { BarChart3, Check, ExternalLink, Megaphone, Pencil, Pin, Plus, Trash2, Users, Vote, X } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { announcementsApi, type Announcement, type AnnouncementKind, type Poll, type PollResultsVisibility } from '../../api/engagement';
import { useDepartments } from '../../api/hooks';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
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
  Switch,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate, todayIso } from '../../lib/format';

type Tab = 'news' | 'polls';

const KIND_TONES: Record<AnnouncementKind, 'blue' | 'violet' | 'green'> = { News: 'blue', Policy: 'violet', Event: 'green' };
const KIND_LABELS: Record<AnnouncementKind, string> = { News: 'News', Policy: 'HR policy', Event: 'Event' };

export function AnnouncementsPage() {
  const user = useUser();
  const hr = isHr(user);
  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get('tab') === 'polls' ? 'polls' : 'news';
  const [creating, setCreating] = useState<'announcement' | 'poll' | null>(null);
  return (
    <>
      <PageHeader
        title="Announcements"
        description={hr ? 'News, policies and polls for everyone or chosen departments.' : 'News and policies from HR, and polls to vote in.'}
        actions={
          hr && (
            <>
              <Button variant="secondary" icon={<Vote className="size-4" />} onClick={() => setCreating('poll')}>
                New poll
              </Button>
              <Button icon={<Plus className="size-4" />} onClick={() => setCreating('announcement')}>
                New announcement
              </Button>
            </>
          )
        }
      />
      <Tabs<Tab>
        className="mb-5"
        value={tab}
        onChange={(t) => setParams(t === 'news' ? {} : { tab: t }, { replace: true })}
        items={[
          { value: 'news', label: 'Announcements', icon: <Megaphone className="size-4" /> },
          { value: 'polls', label: 'Polls', icon: <BarChart3 className="size-4" /> },
        ]}
      />
      {tab === 'news' ? <AnnouncementList hr={hr} /> : <PollList hr={hr} />}
      {creating === 'announcement' && <AnnouncementModal item={null} onClose={() => setCreating(null)} />}
      {creating === 'poll' && <PollModal onClose={() => setCreating(null)} />}
    </>
  );
}

// ---------- Announcements ----------

function status(a: Announcement) {
  const today = todayIso();
  if (a.publishFrom > today) return { label: `Scheduled for ${formatDate(a.publishFrom)}`, tone: 'amber' as const };
  if (a.publishTo && a.publishTo < today) return { label: 'Ended', tone: 'gray' as const };
  return null;
}

function AnnouncementList({ hr }: { hr: boolean }) {
  const qc = useQueryClient();
  const list = useQuery({ queryKey: hr ? qk.announcementsManage : qk.announcements, queryFn: hr ? announcementsApi.manage : announcementsApi.feed });
  const [editing, setEditing] = useState<Announcement | null>(null);
  const [deleting, setDeleting] = useState<Announcement | null>(null);
  const toast = useToast();

  // Opening the page counts as reading what is on it.
  const unread = (list.data ?? []).filter((a) => !a.isRead && !status(a)).map((a) => a.id);
  const unreadKey = unread.join(',');
  useEffect(() => {
    if (!unreadKey) return;
    const t = window.setTimeout(() => {
      Promise.all(unreadKey.split(',').map((id) => announcementsApi.read(id).catch(() => undefined))).then(() =>
        qc.invalidateQueries({ queryKey: qk.announcements }),
      );
    }, 1500);
    return () => window.clearTimeout(t);
  }, [unreadKey, qc]);

  const remove = useMutation({
    mutationFn: (id: string) => announcementsApi.remove(id),
    onSuccess: () => {
      toast.success('Announcement deleted');
      setDeleting(null);
      qc.invalidateQueries({ queryKey: qk.announcements });
    },
    onError: (err) => toast.error(err),
  });

  if (list.isLoading) return <Card><SkeletonRows rows={4} cols={2} /></Card>;
  if (list.isError) return <Card><ErrorState error={list.error} onRetry={() => list.refetch()} /></Card>;
  if (!list.data?.length)
    return (
      <Card>
        <EmptyState icon={Megaphone} title="No announcements" description={hr ? 'Share news, a policy or an event with everyone or some departments.' : 'Nothing from HR right now.'} />
      </Card>
    );
  return (
    <div className="space-y-4">
      {list.data.map((a) => {
        const s = status(a);
        return (
          <Card key={a.id} className={cn('p-5', a.isPinned && 'border-accent-200 dark:border-accent-500/30', s && 'opacity-80')}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <div className="mb-1.5 flex flex-wrap items-center gap-2">
                  {a.isPinned && (
                    <Badge tone="accent">
                      <Pin className="size-3" /> Pinned
                    </Badge>
                  )}
                  <Badge tone={KIND_TONES[a.kind]}>{KIND_LABELS[a.kind]}</Badge>
                  {s && <Badge tone={s.tone}>{s.label}</Badge>}
                  {!a.isRead && !s && !hr && <Badge tone="amber">New</Badge>}
                  <span className="text-xs text-zinc-500">
                    {formatDate(a.publishFrom)}
                    {a.createdByName ? ` · ${a.createdByName}` : ''}
                    {a.departments.length ? ` · ${a.departments.join(', ')}` : hr ? ' · Everyone' : ''}
                  </span>
                </div>
                <h2 className="text-base font-semibold text-zinc-900 dark:text-white">{a.title}</h2>
              </div>
              {hr && (
                <div className="flex items-center gap-1">
                  {a.audienceCount !== null && (
                    <span className="mr-2 flex items-center gap-1 text-xs text-zinc-500" title="Read by / sent to">
                      <Users className="size-3.5" /> {a.readCount} / {a.audienceCount} read
                    </span>
                  )}
                  <IconButton label="Edit" onClick={() => setEditing(a)}>
                    <Pencil className="size-4" />
                  </IconButton>
                  <IconButton label="Delete" onClick={() => setDeleting(a)}>
                    <Trash2 className="size-4" />
                  </IconButton>
                </div>
              )}
            </div>
            <p className="mt-2 text-sm leading-relaxed whitespace-pre-line text-zinc-700 dark:text-zinc-300">{a.body}</p>
            {a.linkUrl && (
              <a href={a.linkUrl} target="_blank" rel="noopener noreferrer" className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
                Open link <ExternalLink className="size-3.5" />
              </a>
            )}
            {(hr || a.attachments > 0) && (
              <AttachmentList owner="Announcement" ownerId={a.id} emptyText={hr ? 'No files. Attach a policy PDF or poster here.' : 'No files.'} className="mt-4" />
            )}
          </Card>
        );
      })}
      {editing && <AnnouncementModal item={editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!deleting}
        title="Delete this announcement?"
        message="It disappears for everyone, with its files."
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}

function DepartmentPicker({ value, onChange }: { value: string[]; onChange: (ids: string[]) => void }) {
  const departments = useDepartments();
  return (
    <Field label="Who sees it" hint={value.length ? `${value.length} department${value.length === 1 ? '' : 's'}` : 'Everyone in the company'}>
      <div className="flex flex-wrap gap-1.5">
        <button
          type="button"
          onClick={() => onChange([])}
          className={cn('rounded-md border px-2.5 py-1 text-xs font-medium', !value.length ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300' : 'border-zinc-200 text-zinc-600 dark:border-zinc-700 dark:text-zinc-300')}
        >
          Everyone
        </button>
        {departments.data?.map((d) => {
          const on = value.includes(d.id);
          return (
            <button
              key={d.id}
              type="button"
              aria-pressed={on}
              onClick={() => onChange(on ? value.filter((x) => x !== d.id) : [...value, d.id])}
              className={cn('rounded-md border px-2.5 py-1 text-xs font-medium', on ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300' : 'border-zinc-200 text-zinc-600 dark:border-zinc-700 dark:text-zinc-300')}
            >
              {d.name}
            </button>
          );
        })}
      </div>
    </Field>
  );
}

function AnnouncementModal({ item, onClose }: { item: Announcement | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [title, setTitle] = useState(item?.title ?? '');
  const [body, setBody] = useState(item?.body ?? '');
  const [kind, setKind] = useState<AnnouncementKind>(item?.kind ?? 'News');
  const [from, setFrom] = useState(item?.publishFrom ?? todayIso());
  const [to, setTo] = useState(item?.publishTo ?? '');
  const [departments, setDepartments] = useState<string[]>(item?.departmentIds ?? []);
  const [pinned, setPinned] = useState(item?.isPinned ?? false);
  const [link, setLink] = useState(item?.linkUrl ?? '');
  const [notify, setNotify] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () => {
      const b = { title: title.trim(), body: body.trim(), kind, publishFrom: from, publishTo: to || null, departmentIds: departments, isPinned: pinned, linkUrl: link.trim() || null, notify };
      return item ? announcementsApi.update(item.id, b) : announcementsApi.create(b);
    },
    onSuccess: () => {
      toast.success(item ? 'Announcement updated' : from <= todayIso() ? 'Announcement published' : 'Announcement scheduled', item ? undefined : 'You can attach files to it now.');
      qc.invalidateQueries({ queryKey: qk.announcements });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={item ? 'Edit announcement' : 'New announcement'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!title.trim() || !body.trim()) return setError('Add a title and a message.');
              save.mutate();
            }}
          >
            {item ? 'Save' : from <= todayIso() ? 'Publish' : 'Schedule'}
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Title" required className="sm:col-span-2">
          <Input value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Office closed on Friday" />
        </Field>
        <Field label="Message" required className="sm:col-span-2">
          <Textarea rows={5} maxLength={8000} value={body} onChange={(e) => setBody(e.target.value)} />
        </Field>
        <Field label="Type">
          <Select value={kind} onChange={(e) => setKind(e.target.value as AnnouncementKind)}>
            <option value="News">News</option>
            <option value="Policy">HR policy</option>
            <option value="Event">Event</option>
          </Select>
        </Field>
        <Field label="Link" hint="Optional, https://…">
          <Input type="url" value={link} maxLength={500} onChange={(e) => setLink(e.target.value)} />
        </Field>
        <Field label="Show from" required>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </Field>
        <Field label="Show until" hint="Blank keeps it up">
          <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
        </Field>
        <div className="sm:col-span-2">
          <DepartmentPicker value={departments} onChange={setDepartments} />
        </div>
        <Checkbox label="Pin to the top" checked={pinned} onChange={(e) => setPinned(e.target.checked)} />
        {!item && <Checkbox label="Notify people" description="In the app, and by email for those who have it on." checked={notify} onChange={(e) => setNotify(e.target.checked)} />}
      </div>
    </Modal>
  );
}

// ---------- Polls ----------

function PollList({ hr }: { hr: boolean }) {
  const list = useQuery({ queryKey: hr ? qk.pollsManage : qk.polls, queryFn: hr ? announcementsApi.managePolls : announcementsApi.polls });
  if (list.isLoading) return <Card><SkeletonRows rows={4} cols={2} /></Card>;
  if (list.isError) return <Card><ErrorState error={list.error} onRetry={() => list.refetch()} /></Card>;
  if (!list.data?.length)
    return (
      <Card>
        <EmptyState icon={BarChart3} title="No polls" description={hr ? 'Ask a quick question and see what people think.' : 'Nothing to vote on right now.'} />
      </Card>
    );
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {list.data.map((p) => (
        <PollCard key={p.id} poll={p} hr={hr} />
      ))}
    </div>
  );
}

export function PollCard({ poll, hr }: { poll: Poll; hr: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [deleting, setDeleting] = useState(false);
  const [showVoters, setShowVoters] = useState(false);
  const today = todayIso();
  const upcoming = poll.opensOn > today;
  const vote = useMutation({
    mutationFn: (optionId: string) => announcementsApi.vote(poll.id, optionId),
    onSuccess: () => {
      toast.success(poll.myOptionId ? 'Vote changed' : 'Vote counted');
      qc.invalidateQueries({ queryKey: qk.polls });
    },
    onError: (err) => toast.error(err),
  });
  const remove = useMutation({
    mutationFn: () => announcementsApi.removePoll(poll.id),
    onSuccess: () => {
      toast.success('Poll deleted');
      qc.invalidateQueries({ queryKey: qk.polls });
    },
    onError: (err) => toast.error(err),
  });
  const total = poll.totalVotes ?? 0;
  const lead = Math.max(0, ...poll.options.map((o) => o.votes ?? 0));
  const canVote = poll.isOpen && !hr;

  return (
    <Card className="flex flex-col p-5">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="mb-1.5 flex flex-wrap items-center gap-2">
            {upcoming ? <Badge tone="amber">Opens {formatDate(poll.opensOn)}</Badge> : poll.isOpen ? <Badge tone="green" >Open until {formatDate(poll.closesOn)}</Badge> : <Badge tone="gray">Closed {formatDate(poll.closesOn)}</Badge>}
            {poll.isAnonymous && <Badge tone="violet">Anonymous</Badge>}
            {hr && <span className="text-xs text-zinc-500">{poll.departments.length ? poll.departments.join(', ') : 'Everyone'}</span>}
          </div>
          <h2 className="font-semibold text-zinc-900 dark:text-white">{poll.question}</h2>
          {poll.description && <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">{poll.description}</p>}
        </div>
        {hr && (
          <IconButton label="Delete poll" onClick={() => setDeleting(true)}>
            <Trash2 className="size-4" />
          </IconButton>
        )}
      </div>
      <ul className="space-y-2">
        {poll.options.map((o) => {
          const mine = poll.myOptionId === o.id;
          const pct = poll.resultsShown && total ? Math.round(((o.votes ?? 0) / total) * 100) : 0;
          return (
            <li key={o.id}>
              <button
                type="button"
                disabled={!canVote || vote.isPending}
                onClick={() => vote.mutate(o.id)}
                aria-pressed={mine}
                className={cn(
                  'relative flex w-full items-center justify-between gap-3 overflow-hidden rounded-lg border px-3 py-2.5 text-left text-sm transition',
                  mine ? 'border-accent-500 ring-2 ring-accent-500/25' : 'border-zinc-200 dark:border-zinc-700',
                  canVote && 'hover:border-accent-400',
                )}
              >
                {poll.resultsShown && (
                  <span
                    className={cn('absolute inset-y-0 left-0 transition-[width] duration-500', (o.votes ?? 0) === lead && lead > 0 ? 'bg-accent-100 dark:bg-accent-500/20' : 'bg-zinc-100 dark:bg-zinc-800')}
                    style={{ width: `${pct}%` }}
                    aria-hidden="true"
                  />
                )}
                <span className="relative flex items-center gap-2 font-medium text-zinc-900 dark:text-zinc-100">
                  {mine && <Check className="size-4 text-accent-600 dark:text-accent-400" />}
                  {o.text}
                </span>
                {poll.resultsShown && (
                  <span className="relative text-xs text-zinc-600 tabular-nums dark:text-zinc-300">
                    {pct}% · {o.votes}
                  </span>
                )}
              </button>
              {showVoters && o.voters && o.voters.length > 0 && <p className="mt-1 px-1 text-xs text-zinc-500">{o.voters.join(', ')}</p>}
            </li>
          );
        })}
      </ul>
      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-zinc-500">
        <span>
          {poll.resultsShown
            ? `${total} vote${total === 1 ? '' : 's'}${poll.audienceCount !== null ? ` of ${poll.audienceCount} people` : ''}.`
            : poll.myOptionId
              ? poll.resultsVisibility === 'AfterClose' ? 'Results show when the poll closes.' : 'Only HR sees the results.'
              : canVote ? 'Pick an answer to vote.' : ''}
          {canVote && poll.myOptionId ? ' You can change your vote until it closes.' : ''}
        </span>
        {hr && !poll.isAnonymous && total > 0 && (
          <Button variant="ghost" size="sm" onClick={() => setShowVoters((s) => !s)}>
            {showVoters ? 'Hide names' : 'Who voted'}
          </Button>
        )}
      </div>
      <ConfirmDialog
        open={deleting}
        title="Delete this poll?"
        message="Its votes are deleted too."
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={() => setDeleting(false)}
      />
    </Card>
  );
}

function PollModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [question, setQuestion] = useState('');
  const [description, setDescription] = useState('');
  const [options, setOptions] = useState(['', '']);
  const [opens, setOpens] = useState(todayIso());
  const [closes, setCloses] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  });
  const [departments, setDepartments] = useState<string[]>([]);
  const [anonymous, setAnonymous] = useState(false);
  const [visibility, setVisibility] = useState<PollResultsVisibility>('AfterVoting');
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: () =>
      announcementsApi.createPoll({
        question: question.trim(),
        description: description.trim() || null,
        opensOn: opens,
        closesOn: closes,
        departmentIds: departments,
        isAnonymous: anonymous,
        resultsVisibility: visibility,
        options: options.map((o) => o.trim()).filter(Boolean),
      }),
    onSuccess: () => {
      toast.success('Poll created', 'People in the audience have been notified.');
      qc.invalidateQueries({ queryKey: qk.polls });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="New poll"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            loading={save.isPending}
            onClick={() => {
              setError(null);
              if (!question.trim()) return setError('Write the question.');
              if (options.filter((o) => o.trim()).length < 2) return setError('Give at least two answers.');
              save.mutate();
            }}
          >
            Create poll
          </Button>
        </>
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Question" required className="sm:col-span-2">
          <Input value={question} maxLength={300} onChange={(e) => setQuestion(e.target.value)} placeholder="e.g. Where should the annual dinner be?" />
        </Field>
        <Field label="Details" className="sm:col-span-2">
          <Textarea rows={2} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        <Field label="Answers" hint="2 to 6" className="sm:col-span-2">
          <div className="space-y-2">
            {options.map((o, i) => (
              <div key={i} className="flex gap-2">
                <Input value={o} maxLength={200} placeholder={`Answer ${i + 1}`} onChange={(e) => setOptions((list) => list.map((x, j) => (j === i ? e.target.value : x)))} />
                {options.length > 2 && (
                  <IconButton label="Remove answer" onClick={() => setOptions((list) => list.filter((_, j) => j !== i))}>
                    <X className="size-4" />
                  </IconButton>
                )}
              </div>
            ))}
            {options.length < 6 && (
              <Button variant="ghost" size="sm" icon={<Plus className="size-4" />} onClick={() => setOptions((list) => [...list, ''])}>
                Add answer
              </Button>
            )}
          </div>
        </Field>
        <Field label="Opens" required>
          <Input type="date" value={opens} onChange={(e) => setOpens(e.target.value)} />
        </Field>
        <Field label="Closes" required>
          <Input type="date" value={closes} min={opens} onChange={(e) => setCloses(e.target.value)} />
        </Field>
        <div className="sm:col-span-2">
          <DepartmentPicker value={departments} onChange={setDepartments} />
        </div>
        <Field label="Who sees results">
          <Select value={visibility} onChange={(e) => setVisibility(e.target.value as PollResultsVisibility)}>
            <option value="AfterVoting">Everyone, after they vote</option>
            <option value="AfterClose">Everyone, when it closes</option>
            <option value="HrOnly">Only HR</option>
          </Select>
        </Field>
        <div className="flex items-end pb-2">
          <Switch checked={anonymous} onChange={setAnonymous} label="Anonymous" description="Nobody sees who voted for what." />
        </div>
      </div>
    </Modal>
  );
}
