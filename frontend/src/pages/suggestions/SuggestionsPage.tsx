import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Lightbulb, Plus } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { errorMessage } from '../../api/client';
import { suggestionsApi, type Suggestion, type SuggestionCategory, type SuggestionStatus } from '../../api/engine';
import { useEmployeeLookup } from '../../api/hooks';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
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
  StatusBadge,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatDateTimeUtc } from '../../lib/format';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from '../settings/shared';

const KEY = ['suggestions'] as const;
const STATUSES: { value: SuggestionStatus; label: string }[] = [
  { value: 'New', label: 'New' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Accepted', label: 'Accepted' },
  { value: 'Implemented', label: 'Implemented' },
  { value: 'Declined', label: 'Declined' },
];
const statusLabel = (s: SuggestionStatus) => STATUSES.find((x) => x.value === s)?.label ?? s;

type Tab = 'mine' | 'review' | 'categories';

/** Suggestion box: ideas and concerns, optionally anonymous, answered by the category's reviewers or HR. */
export function SuggestionsPage() {
  const user = useUser();
  const hr = isHr(user);
  const [params, setParams] = useSearchParams();
  const canReview = useQuery({ queryKey: [...KEY, 'can-review'], queryFn: suggestionsApi.canReview });
  const tab = (params.get('tab') as Tab | null) ?? 'mine';
  const [writing, setWriting] = useState(false);
  return (
    <>
      <PageHeader
        title="Suggestions"
        description="Share ideas or concerns with the company. You can stay anonymous."
        actions={
          <Button icon={<Plus className="size-4" />} onClick={() => setWriting(true)}>
            New suggestion
          </Button>
        }
      />
      {(canReview.data || hr) && (
        <Tabs<Tab>
          className="mb-6"
          value={tab}
          onChange={(t) => setParams(t === 'mine' ? {} : { tab: t }, { replace: true })}
          items={[
            { value: 'mine', label: 'Mine' },
            { value: 'review', label: 'To review' },
            ...(hr ? [{ value: 'categories' as const, label: 'Categories' }] : []),
          ]}
        />
      )}
      {tab === 'review' ? <Review /> : tab === 'categories' && hr ? <Categories /> : <Mine />}
      {writing && <WriteModal onClose={() => setWriting(false)} />}
    </>
  );
}

function SuggestionCard({ s, children }: { s: Suggestion; children?: React.ReactNode }) {
  return (
    <Card>
      <div className="space-y-2 p-5">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="font-medium text-zinc-900 dark:text-zinc-100">{s.title}</h3>
          {s.category && <Badge>{s.category}</Badge>}
          <StatusBadge
            status={s.status === 'Implemented' || s.status === 'Accepted' ? 'Approved' : s.status === 'Declined' ? 'Rejected' : 'Pending'}
            label={statusLabel(s.status)}
          />
          <span className="ms-auto text-xs text-zinc-500 dark:text-zinc-400">
            {s.isAnonymous ? (s.mine ? 'Anonymous (only you see it is yours)' : 'Anonymous') : s.from} · {formatDateTimeUtc(s.createdAtUtc)}
          </span>
        </div>
        <p className="text-sm whitespace-pre-line text-zinc-600 dark:text-zinc-300">{s.details}</p>
        {s.response && (
          <p className="rounded-lg bg-zinc-50 px-3 py-2 text-sm dark:bg-zinc-800/50">
            <span className="font-medium">{s.respondedByName}:</span> {s.response}
          </p>
        )}
        {children}
      </div>
    </Card>
  );
}

function Mine() {
  const mine = useQuery({ queryKey: [...KEY, 'mine'], queryFn: suggestionsApi.my });
  if (mine.isLoading) return <SkeletonRows rows={3} cols={2} />;
  if (!mine.data?.length) return <EmptyState icon={Lightbulb} title="No suggestions yet" description="Have an idea to make work better? Share it." />;
  return (
    <div className="space-y-4">
      {mine.data.map((s) => (
        <SuggestionCard key={s.id} s={s} />
      ))}
    </div>
  );
}

function Review() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: [...KEY, 'review'], queryFn: suggestionsApi.review });
  const [drafts, setDrafts] = useState<Record<string, { status: SuggestionStatus; response: string }>>({});
  const respond = useMutation({
    mutationFn: (s: Suggestion) => {
      const d = drafts[s.id] ?? { status: s.status, response: s.response ?? '' };
      return suggestionsApi.respond(s.id, d.status, d.response.trim() || null);
    },
    onSuccess: () => {
      toast.success('Saved', 'The sender is told.');
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });
  if (list.isLoading) return <SkeletonRows rows={3} cols={2} />;
  if (!list.data?.length) return <EmptyState icon={Lightbulb} title="Nothing to review" />;
  return (
    <div className="space-y-4">
      {list.data.map((s) => {
        const d = drafts[s.id] ?? { status: s.status === 'New' ? 'UnderReview' : s.status, response: s.response ?? '' };
        return (
          <SuggestionCard key={s.id} s={s}>
            <div className="grid gap-2 sm:grid-cols-[12rem_1fr_auto]">
              <Select
                aria-label="Status"
                value={d.status}
                onChange={(e) => setDrafts({ ...drafts, [s.id]: { ...d, status: e.target.value as SuggestionStatus } })}
              >
                {STATUSES.map((x) => (
                  <option key={x.value} value={x.value}>
                    {x.label}
                  </option>
                ))}
              </Select>
              <Input
                placeholder="Reply to the sender"
                value={d.response}
                maxLength={2000}
                onChange={(e) => setDrafts({ ...drafts, [s.id]: { ...d, response: e.target.value } })}
              />
              <Button loading={respond.isPending && respond.variables?.id === s.id} onClick={() => (setDrafts({ ...drafts, [s.id]: d }), respond.mutate(s))}>
                Save
              </Button>
            </div>
          </SuggestionCard>
        );
      })}
    </div>
  );
}

function WriteModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const categories = useQuery({ queryKey: [...KEY, 'categories'], queryFn: suggestionsApi.categories });
  const [f, setF] = useState({ title: '', details: '', categoryId: '', isAnonymous: false });
  const save = useMutation({
    mutationFn: () => suggestionsApi.submit({ title: f.title.trim(), details: f.details.trim(), categoryId: f.categoryId || null, isAnonymous: f.isAnonymous }),
    onSuccess: () => {
      toast.success('Thank you', 'Your suggestion was sent.');
      qc.invalidateQueries({ queryKey: KEY });
      onClose();
    },
  });
  const active = (categories.data ?? []).filter((c) => c.isActive);
  return (
    <Modal
      open
      onClose={onClose}
      title="New suggestion"
      footer={
        <Button loading={save.isPending} disabled={!f.title.trim() || !f.details.trim()} onClick={() => save.mutate()}>
          Send
        </Button>
      }
    >
      <div className="space-y-4">
        {active.length > 0 && (
          <Field label="Category">
            <Select value={f.categoryId} onChange={(e) => setF({ ...f, categoryId: e.target.value })}>
              <option value="">General (goes to HR)</option>
              {active.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <Field label="Title" required>
          <Input value={f.title} maxLength={150} onChange={(e) => setF({ ...f, title: e.target.value })} />
        </Field>
        <Field label="Details" required>
          <Textarea rows={5} value={f.details} maxLength={4000} onChange={(e) => setF({ ...f, details: e.target.value })} />
        </Field>
        <Checkbox
          label="Send anonymously"
          description="Reviewers will not see your name. You can still follow it here."
          checked={f.isAnonymous}
          onChange={(e) => setF({ ...f, isAnonymous: e.target.checked })}
        />
        <FormError message={save.isError ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}

function Categories() {
  const list = useQuery({ queryKey: [...KEY, 'categories'], queryFn: suggestionsApi.categories });
  const people = useEmployeeLookup();
  const [editing, setEditing] = useState<SuggestionCategory | 'new' | null>(null);
  const [deleting, setDeleting] = useState<SuggestionCategory | null>(null);
  const name = (id: string) => people.data?.find((p) => p.id === id)?.name ?? '';
  return (
    <Card>
      <CardHeader
        title="Categories"
        description="Each category can have its own reviewers; without reviewers, HR reviews it."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add category
          </Button>
        }
      />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {(list.data ?? []).map((c) => (
          <li key={c.id} className="flex items-center gap-3 px-5 py-3 text-sm">
            <span className="font-medium">{c.name}</span>
            {!c.isActive && <Badge>Off</Badge>}
            <span className="flex-1 text-xs text-zinc-500 dark:text-zinc-400">{c.reviewerIds.length ? c.reviewerIds.map(name).join(', ') : 'HR'}</span>
            <RowActions label={c.name} onEdit={() => setEditing(c)} onDelete={() => setDeleting(c)} />
          </li>
        ))}
        {!list.data?.length && <li className="px-5 py-3 text-sm text-zinc-500 dark:text-zinc-400">No categories: everything goes to HR.</li>}
      </ul>
      {editing && <CategoryModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Category" remove={suggestionsApi.deleteCategory} invalidate={[KEY]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function CategoryModal({ item, onClose }: { item: SuggestionCategory | null; onClose: () => void }) {
  const people = useEmployeeLookup();
  const [v, setV] = useState({ name: item?.name ?? '', reviewerIds: item?.reviewerIds ?? [], isActive: item?.isActive ?? true });
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: () => (item ? suggestionsApi.updateCategory(item.id, v) : suggestionsApi.addCategory(v)),
    invalidate: [KEY],
    successMessage: 'Category saved',
    onSuccess: onClose,
  });
  return (
    <EditModal
      open
      title={item ? 'Edit category' : 'Add category'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => (v.name.trim() ? mutation.mutate(undefined) : setFieldErrors({ name: 'Name is required.' }))}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={v.name} maxLength={100} placeholder="Workplace, Canteen, Process" onChange={(e) => setV({ ...v, name: e.target.value })} />
      </Field>
      <Field label="Reviewers" hint="Told about each new suggestion. None: HR.">
        <div className="space-y-2">
          <div className="flex flex-wrap gap-1.5">
            {v.reviewerIds.map((id) => (
              <Badge key={id}>
                {people.data?.find((p) => p.id === id)?.name}
                <button type="button" className="ms-1" aria-label="Remove" onClick={() => setV({ ...v, reviewerIds: v.reviewerIds.filter((x) => x !== id) })}>
                  ×
                </button>
              </Badge>
            ))}
          </div>
          <Select value="" aria-label="Add reviewer" onChange={(e) => e.target.value && setV({ ...v, reviewerIds: [...v.reviewerIds, e.target.value] })}>
            <option value="">Add reviewer…</option>
            {(people.data ?? [])
              .filter((p) => !v.reviewerIds.includes(p.id))
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
          </Select>
        </div>
      </Field>
      <Checkbox label="In use" checked={v.isActive} onChange={(e) => setV({ ...v, isActive: e.target.checked })} />
    </EditModal>
  );
}
