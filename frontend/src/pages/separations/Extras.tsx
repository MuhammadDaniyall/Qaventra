import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardCheck, Plus, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useEmployeeLookup } from '../../api/hooks';
import { separationSetupApi, type ClearanceTemplate, type ExitAnswer, type ExitTemplate } from '../../api/separationSetup';
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  Checkbox,
  EmptyState,
  Field,
  FormError,
  IconButton,
  Input,
  PageHeader,
  Select,
  SkeletonRows,
  Tabs,
  Textarea,
  useToast,
} from '../../components/ui';
import { formatDate, formatDateTimeUtc } from '../../lib/format';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from '../settings/shared';

const KEY = ['separation-setup'] as const;
const useExtras = (id: string) => useQuery({ queryKey: [...KEY, 'extras', id], queryFn: () => separationSetupApi.extras(id) });

/** HR: clearance level and owner of each checklist item. */
export function ClearanceOwnersCard({ separationId, titles }: { separationId: string; titles: Record<string, string> }) {
  const toast = useToast();
  const qc = useQueryClient();
  const extras = useExtras(separationId);
  const people = useEmployeeLookup();
  const save = useMutation({
    mutationFn: (x: { itemId: string; level: number; owner: string | null }) => separationSetupApi.setOwner(separationId, x.itemId, x.level, x.owner),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
    onError: (e) => toast.error(e),
  });
  if (!extras.data?.clearance.length) return null;
  return (
    <Card>
      <CardHeader title="Who clears what" description="Later levels open once earlier ones are cleared. Owners clear their items from Exit clearances." />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {extras.data.clearance.map((c) => (
          <li key={c.itemId} className="grid grid-cols-[1fr_5rem_11rem] items-center gap-2 px-5 py-2 text-sm">
            <span className="truncate">
              {titles[c.itemId]} {!c.open && <Badge className="ms-1">Waiting</Badge>}
            </span>
            <Select
              aria-label="Level"
              value={c.level}
              onChange={(e) => save.mutate({ itemId: c.itemId, level: Number(e.target.value), owner: c.responsibleEmployeeId })}
            >
              {Array.from({ length: 10 }, (_, i) => (
                <option key={i + 1} value={i + 1}>
                  Level {i + 1}
                </option>
              ))}
            </Select>
            <Select
              aria-label="Owner"
              value={c.responsibleEmployeeId ?? ''}
              onChange={(e) => save.mutate({ itemId: c.itemId, level: c.level, owner: e.target.value || null })}
            >
              <option value="">HR</option>
              {(people.data ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </li>
        ))}
      </ul>
    </Card>
  );
}

/** Exit interview questions: the leaver (or panel/HR) answers; others read. */
export function ExitQuestionsCard({ separationId, canAnswer }: { separationId: string; canAnswer: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const extras = useExtras(separationId);
  const [draft, setDraft] = useState<Record<string, string> | null>(null);
  const save = useMutation({
    mutationFn: () =>
      separationSetupApi.answer(
        separationId,
        Object.entries(draft!).map(([questionId, answer]) => ({ questionId, answer: answer || null })),
      ),
    onSuccess: () => {
      toast.success('Answers saved');
      setDraft(null);
      qc.invalidateQueries({ queryKey: KEY });
    },
  });
  const answers = extras.data?.answers ?? [];
  if (!answers.length) return null;
  const v = draft ?? Object.fromEntries(answers.map((a) => [a.questionId, a.answer ?? '']));
  const field = (a: ExitAnswer) => {
    const set = (x: string) => setDraft({ ...v, [a.questionId]: x });
    if (!draft) return <p className="text-sm text-zinc-600 dark:text-zinc-300">{a.answer || <span className="text-zinc-500 dark:text-zinc-400">Not answered</span>}</p>;
    if (a.kind === 'Rating')
      return (
        <div className="flex gap-1">
          {[1, 2, 3, 4, 5].map((n) => (
            <Button key={n} size="sm" variant={v[a.questionId] === String(n) ? 'primary' : 'secondary'} onClick={() => set(String(n))}>
              {n}
            </Button>
          ))}
        </div>
      );
    if (a.kind === 'YesNo')
      return (
        <Select value={v[a.questionId]} onChange={(e) => set(e.target.value)}>
          <option value="">—</option>
          <option value="Yes">Yes</option>
          <option value="No">No</option>
        </Select>
      );
    return <Textarea rows={2} value={v[a.questionId]} maxLength={2000} onChange={(e) => set(e.target.value)} />;
  };
  return (
    <Card>
      <CardHeader
        title="Exit interview questions"
        actions={
          canAnswer &&
          (draft ? (
            <Button size="sm" loading={save.isPending} onClick={() => save.mutate()}>
              Save answers
            </Button>
          ) : (
            <Button size="sm" variant="secondary" onClick={() => setDraft(v)}>
              Answer
            </Button>
          ))
        }
      />
      <CardBody className="space-y-4">
        <FormError message={save.isError ? errorMessage(save.error) : null} />
        {answers.map((a) => (
          <div key={a.questionId}>
            <p className="mb-1 text-sm font-medium">
              {a.question} {a.category && <span className="text-xs font-normal text-zinc-500 dark:text-zinc-400">· {a.category}</span>}
            </p>
            {field(a)}
          </div>
        ))}
      </CardBody>
    </Card>
  );
}

/** HR: which questions, who sits on the panel, when; panel notes. */
export function ExitPanelCard({ separationId }: { separationId: string }) {
  const toast = useToast();
  const qc = useQueryClient();
  const extras = useExtras(separationId);
  const templates = useQuery({ queryKey: [...KEY, 'exit-templates'], queryFn: separationSetupApi.exitTemplates });
  const people = useEmployeeLookup();
  const [f, setF] = useState<{ templateId: string; ids: string[]; at: string; notes: string } | null>(null);
  const e = extras.data;
  const v = f ?? {
    templateId: e?.exitTemplateId ?? '',
    ids: e?.interviewerIds ?? [],
    at: e?.interviewScheduledUtc ? new Date(e.interviewScheduledUtc).toISOString().slice(0, 16) : '',
    notes: e?.interviewNotes ?? '',
  };
  const save = useMutation({
    mutationFn: () =>
      separationSetupApi.panel(separationId, {
        templateId: v.templateId || null,
        interviewerIds: v.ids,
        scheduledUtc: v.at ? new Date(v.at).toISOString() : null,
        notes: v.notes || null,
      }),
    onSuccess: () => {
      toast.success('Saved');
      setF(null);
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (err) => toast.error(err),
  });
  if (!e) return null;
  return (
    <Card>
      <CardHeader
        title="Exit interview panel"
        actions={
          <Button size="sm" variant={f ? 'primary' : 'secondary'} loading={save.isPending} onClick={() => (f ? save.mutate() : setF(v))}>
            {f ? 'Save' : 'Edit'}
          </Button>
        }
      />
      <CardBody className="space-y-3">
        {f ? (
          <>
            <Field label="Questions">
              <Select value={v.templateId} onChange={(x) => setF({ ...v, templateId: x.target.value })}>
                <option value="">Default template</option>
                {(templates.data ?? []).map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="When">
              <Input type="datetime-local" value={v.at} onChange={(x) => setF({ ...v, at: x.target.value })} />
            </Field>
            <Field label="Panel">
              <div className="space-y-2">
                <div className="flex flex-wrap gap-1.5">
                  {v.ids.map((id) => (
                    <Badge key={id}>
                      {people.data?.find((p) => p.id === id)?.name}
                      <button type="button" className="ms-1" aria-label="Remove" onClick={() => setF({ ...v, ids: v.ids.filter((x) => x !== id) })}>
                        ×
                      </button>
                    </Badge>
                  ))}
                </div>
                <Select value="" aria-label="Add to panel" onChange={(x) => x.target.value && setF({ ...v, ids: [...v.ids, x.target.value] })}>
                  <option value="">Add someone…</option>
                  {(people.data ?? [])
                    .filter((p) => !v.ids.includes(p.id))
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                </Select>
              </div>
            </Field>
            <Field label="Panel notes (HR only)">
              <Textarea rows={3} value={v.notes} maxLength={4000} onChange={(x) => setF({ ...v, notes: x.target.value })} />
            </Field>
          </>
        ) : (
          <div className="space-y-1 text-sm">
            <p>
              <span className="text-zinc-500 dark:text-zinc-400">When:</span> {e.interviewScheduledUtc ? formatDateTimeUtc(e.interviewScheduledUtc) : 'Not scheduled'}
            </p>
            <p>
              <span className="text-zinc-500 dark:text-zinc-400">Panel:</span>{' '}
              {e.interviewerIds.length ? e.interviewerIds.map((id) => people.data?.find((p) => p.id === id)?.name).join(', ') : 'None'}
            </p>
            {e.interviewNotes && <p className="whitespace-pre-line text-zinc-600 dark:text-zinc-300">{e.interviewNotes}</p>}
          </div>
        )}
      </CardBody>
    </Card>
  );
}

/** HR: send the final settlement for approval and record payment. */
export function SettlementApprovalCard({ separationId, completed }: { separationId: string; completed: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const extras = useExtras(separationId);
  const refresh = () => qc.invalidateQueries({ queryKey: KEY });
  const submit = useMutation({ mutationFn: () => separationSetupApi.submitSettlement(separationId), onSuccess: refresh, onError: (e) => toast.error(e) });
  const paid = useMutation({
    mutationFn: (via: string) => separationSetupApi.settlementPaid(separationId, via),
    onSuccess: refresh,
    onError: (e) => toast.error(e),
  });
  const e = extras.data;
  if (!e) return null;
  return (
    <Card className="mt-6">
      <CardHeader title="Settlement approval and payment" />
      <CardBody className="space-y-3 text-sm">
        {e.settlementApprovedAtUtc ? (
          <p className="flex items-center gap-2 text-emerald-700 dark:text-emerald-300">
            <CheckCircle2 className="size-4" /> Approved {formatDateTimeUtc(e.settlementApprovedAtUtc)}
            {e.settlementApprovalNote && <span className="text-zinc-500 dark:text-zinc-400">· {e.settlementApprovalNote}</span>}
          </p>
        ) : e.settlementSubmittedAtUtc ? (
          <p className="text-amber-700 dark:text-amber-300">Waiting for approval since {formatDateTimeUtc(e.settlementSubmittedAtUtc)}.</p>
        ) : (
          <div className="flex items-center justify-between gap-3">
            <p className="text-zinc-500 dark:text-zinc-400">
              Optional: send the saved settlement through the final settlement approval chain. Completion then waits for it.
              {e.settlementApprovalNote && <span className="block text-red-600">Sent back: {e.settlementApprovalNote}</span>}
            </p>
            {!completed && (
              <Button size="sm" variant="secondary" loading={submit.isPending} onClick={() => submit.mutate()}>
                Send for approval
              </Button>
            )}
          </div>
        )}
        {completed &&
          (e.settlementPaidAtUtc ? (
            <p>
              Paid {formatDateTimeUtc(e.settlementPaidAtUtc)} by {e.settlementPaidVia}
            </p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {['Bank transfer', 'Cheque', 'Cash'].map((via) => (
                <Button key={via} size="sm" variant="secondary" loading={paid.isPending && paid.variables === via} onClick={() => paid.mutate(via)}>
                  Paid by {via.toLowerCase()}
                </Button>
              ))}
            </div>
          ))}
      </CardBody>
    </Card>
  );
}

/** Clearance items the signed-in person must clear for people leaving. */
export function MyClearancesPage() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: [...KEY, 'mine'], queryFn: separationSetupApi.myClearances });
  const toggle = useMutation({
    mutationFn: (c: { separationId: string; itemId: string; isCleared: boolean }) => separationSetupApi.clearMine(c.separationId, c.itemId, !c.isCleared, null),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
    onError: (e) => toast.error(e),
  });
  return (
    <>
      <PageHeader title="Exit clearances" description="Things you need to clear for people who are leaving." />
      <Card>
        {list.isLoading ? (
          <SkeletonRows rows={3} cols={4} />
        ) : !list.data?.length ? (
          <EmptyState icon={ClipboardCheck} title="Nothing to clear" />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {list.data.map((c) => (
              <li key={c.itemId} className="flex items-center gap-3 px-5 py-3 text-sm">
                <Checkbox label="" checked={c.isCleared} onChange={() => toggle.mutate(c)} aria-label={`Cleared: ${c.title}`} />
                <span className="flex-1">
                  <span className="font-medium">{c.title}</span> <span className="text-zinc-500 dark:text-zinc-400">({c.department})</span>
                  <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                    {c.employeeName} · {c.employeeCode}
                    {c.lastWorkingDay && ` · last day ${formatDate(c.lastWorkingDay)}`}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </>
  );
}

// ---------- Setup: templates ----------

type Tab = 'clearance' | 'exit';

export function SeparationSetupPage() {
  const [tab, setTab] = useState<Tab>('clearance');
  return (
    <>
      <PageHeader
        title="Separation setup"
        description="Clearance checklists and exit interview questions."
        back={{ to: '/separations', label: 'Separations' }}
      />
      <Tabs<Tab>
        className="mb-6"
        value={tab}
        onChange={setTab}
        items={[
          { value: 'clearance', label: 'Clearance templates' },
          { value: 'exit', label: 'Exit interview templates' },
        ]}
      />
      {tab === 'clearance' ? <ClearanceTemplates /> : <ExitTemplates />}
    </>
  );
}

function ClearanceTemplates() {
  const list = useQuery({ queryKey: [...KEY, 'clearance-templates'], queryFn: separationSetupApi.clearanceTemplates });
  const [editing, setEditing] = useState<ClearanceTemplate | 'new' | null>(null);
  const [deleting, setDeleting] = useState<ClearanceTemplate | null>(null);
  return (
    <Card>
      <CardHeader
        title="Clearance templates"
        description="The default template is used whenever a separation starts."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add template
          </Button>
        }
      />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {(list.data ?? []).map((t) => (
          <li key={t.id} className="flex items-center gap-3 px-5 py-3 text-sm">
            <span className="font-medium">{t.name}</span>
            {t.isDefault && <Badge tone="accent">Default</Badge>}
            <span className="flex-1 text-xs text-zinc-500 dark:text-zinc-400">
              {t.items.length} items · {Math.max(...t.items.map((i) => i.level))} level(s)
            </span>
            <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
          </li>
        ))}
        {!list.data?.length && <li className="px-5 py-3 text-sm text-zinc-500 dark:text-zinc-400">No templates: the built-in checklist is used.</li>}
      </ul>
      {editing && <ClearanceTemplateModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Template" remove={separationSetupApi.deleteClearanceTemplate} invalidate={[KEY]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function ClearanceTemplateModal({ item, onClose }: { item: ClearanceTemplate | null; onClose: () => void }) {
  const people = useEmployeeLookup();
  const [v, setV] = useState({
    name: item?.name ?? '',
    isDefault: item?.isDefault ?? true,
    items: item?.items ?? [{ department: 'IT', title: '', level: 1, responsibleEmployeeId: null as string | null }],
  });
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: () => {
      const b = { ...v, items: v.items.filter((i) => i.title.trim() && i.department.trim()) };
      return item ? separationSetupApi.updateClearanceTemplate(item.id, b) : separationSetupApi.addClearanceTemplate(b);
    },
    invalidate: [KEY],
    successMessage: 'Template saved',
    onSuccess: onClose,
  });
  const patch = (i: number, p: Partial<(typeof v.items)[number]>) => setV({ ...v, items: v.items.map((x, j) => (j === i ? { ...x, ...p } : x)) });
  return (
    <EditModal
      open
      title={item ? 'Edit clearance template' : 'Add clearance template'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => (v.name.trim() ? mutation.mutate(undefined) : setFieldErrors({ name: 'Name is required.' }))}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={v.name} maxLength={150} onChange={(e) => setV({ ...v, name: e.target.value })} />
      </Field>
      <Checkbox label="Default for new separations" checked={v.isDefault} onChange={(e) => setV({ ...v, isDefault: e.target.checked })} />
      <div className="space-y-2">
        {v.items.map((x, i) => (
          <div key={i} className="grid grid-cols-[7rem_1fr_5.5rem_9rem_auto] items-center gap-2">
            <Input aria-label="Department" value={x.department} maxLength={100} onChange={(e) => patch(i, { department: e.target.value })} />
            <Input aria-label="Item" placeholder="What must be cleared" value={x.title} maxLength={200} onChange={(e) => patch(i, { title: e.target.value })} />
            <Select aria-label="Level" value={x.level} onChange={(e) => patch(i, { level: Number(e.target.value) })}>
              {Array.from({ length: 10 }, (_, k) => (
                <option key={k + 1} value={k + 1}>
                  L{k + 1}
                </option>
              ))}
            </Select>
            <Select aria-label="Owner" value={x.responsibleEmployeeId ?? ''} onChange={(e) => patch(i, { responsibleEmployeeId: e.target.value || null })}>
              <option value="">HR</option>
              {(people.data ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
            <IconButton label="Remove" onClick={() => setV({ ...v, items: v.items.filter((_, j) => j !== i) })}>
              <Trash2 className="size-4" />
            </IconButton>
          </div>
        ))}
        <Button
          size="sm"
          variant="ghost"
          icon={<Plus className="size-4" />}
          onClick={() => setV({ ...v, items: [...v.items, { department: '', title: '', level: 1, responsibleEmployeeId: null }] })}
        >
          Add item
        </Button>
      </div>
    </EditModal>
  );
}

function ExitTemplates() {
  const list = useQuery({ queryKey: [...KEY, 'exit-templates'], queryFn: separationSetupApi.exitTemplates });
  const [editing, setEditing] = useState<ExitTemplate | 'new' | null>(null);
  const [deleting, setDeleting] = useState<ExitTemplate | null>(null);
  return (
    <Card>
      <CardHeader
        title="Exit interview templates"
        description="Questions the leaver answers (ratings, yes/no, text). The default is used unless HR picks another."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add template
          </Button>
        }
      />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {(list.data ?? []).map((t) => (
          <li key={t.id} className="flex items-center gap-3 px-5 py-3 text-sm">
            <span className="font-medium">{t.name}</span>
            {t.isDefault && <Badge tone="accent">Default</Badge>}
            <span className="flex-1 text-xs text-zinc-500 dark:text-zinc-400">{t.questions.length} questions</span>
            <RowActions label={t.name} onEdit={() => setEditing(t)} onDelete={() => setDeleting(t)} />
          </li>
        ))}
        {!list.data?.length && <li className="px-5 py-3 text-sm text-zinc-500 dark:text-zinc-400">No templates yet.</li>}
      </ul>
      {editing && <ExitTemplateModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm item={deleting} name="Template" remove={separationSetupApi.deleteExitTemplate} invalidate={[KEY]} onClose={() => setDeleting(null)} />
    </Card>
  );
}

function ExitTemplateModal({ item, onClose }: { item: ExitTemplate | null; onClose: () => void }) {
  const [v, setV] = useState({
    name: item?.name ?? '',
    isDefault: item?.isDefault ?? true,
    questions: item?.questions ?? [{ id: null, text: '', category: null, kind: 'Rating' as const }],
  });
  const { mutation, error, fieldErrors, setFieldErrors } = useSaveMutation({
    save: () => {
      const b = { ...v, questions: v.questions.filter((q) => q.text.trim()) };
      return item ? separationSetupApi.updateExitTemplate(item.id, b) : separationSetupApi.addExitTemplate(b);
    },
    invalidate: [KEY],
    successMessage: 'Template saved',
    onSuccess: onClose,
  });
  const patch = (i: number, p: Partial<(typeof v.questions)[number]>) => setV({ ...v, questions: v.questions.map((x, j) => (j === i ? { ...x, ...p } : x)) });
  return (
    <EditModal
      open
      title={item ? 'Edit exit interview template' : 'Add exit interview template'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => (v.name.trim() ? mutation.mutate(undefined) : setFieldErrors({ name: 'Name is required.' }))}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={v.name} maxLength={150} onChange={(e) => setV({ ...v, name: e.target.value })} />
      </Field>
      <Checkbox label="Default" checked={v.isDefault} onChange={(e) => setV({ ...v, isDefault: e.target.checked })} />
      <div className="space-y-2">
        {v.questions.map((q, i) => (
          <div key={i} className="grid grid-cols-[1fr_8rem_7rem_auto] items-center gap-2">
            <Input aria-label="Question" placeholder="Question" value={q.text} maxLength={500} onChange={(e) => patch(i, { text: e.target.value })} />
            <Input
              aria-label="Category"
              placeholder="Category"
              value={q.category ?? ''}
              maxLength={100}
              onChange={(e) => patch(i, { category: e.target.value || null })}
            />
            <Select aria-label="Kind" value={q.kind} onChange={(e) => patch(i, { kind: e.target.value as 'Rating' | 'YesNo' | 'Text' })}>
              <option value="Rating">1–5</option>
              <option value="YesNo">Yes / no</option>
              <option value="Text">Text</option>
            </Select>
            <IconButton label="Remove" onClick={() => setV({ ...v, questions: v.questions.filter((_, j) => j !== i) })}>
              <Trash2 className="size-4" />
            </IconButton>
          </div>
        ))}
        <Button
          size="sm"
          variant="ghost"
          icon={<Plus className="size-4" />}
          onClick={() => setV({ ...v, questions: [...v.questions, { id: null, text: '', category: null, kind: 'Rating' }] })}
        >
          Add question
        </Button>
      </div>
    </EditModal>
  );
}
