import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { AlertTriangle, FilePlus2, FileText, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { errorMessage } from '../../api/client';
import { lettersApi } from '../../api/letters';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  Drawer,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  Skeleton,
  SkeletonRows,
  Spinner,
  Switch,
  Table,
  TBody,
  Tabs,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDate, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import type { LetterAutoUse, LetterCategory, LetterTemplate, SaveLetterTemplateRequest } from '../../types/workflows';
import { BodyEditor, categoryLabel, Highlighted, LETTER_CATEGORIES } from './shared';

type TabValue = 'issued' | 'templates';

export function LettersPage() {
  const [tab, setTab] = useState<TabValue>('issued');
  const [issuing, setIssuing] = useState<LetterTemplate | 'blank' | null>(null);

  return (
    <>
      <PageHeader
        title="HR letters"
        description="Offer, appointment, experience, salary certificates and more, filled from employee records."
        actions={
          <Button icon={<FilePlus2 className="size-4" />} onClick={() => setIssuing('blank')}>
            Issue letter
          </Button>
        }
      />
      <Tabs<TabValue>
        className="mb-4"
        value={tab}
        onChange={setTab}
        items={[
          { value: 'issued', label: 'Issued letters' },
          { value: 'templates', label: 'Templates' },
        ]}
      />
      {tab === 'issued' ? <IssuedList /> : <TemplatesGrid onUse={(t) => setIssuing(t)} />}
      {issuing && <IssueLetterDrawer initial={issuing === 'blank' ? null : issuing} onClose={() => setIssuing(null)} />}
    </>
  );
}

function IssuedList() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<LetterCategory | ''>('');
  const q = useDebounced(search.trim(), 300);
  const list = useQuery({ queryKey: qk.letterList(q, category), queryFn: () => lettersApi.list(q, category) });

  return (
    <Card>
      <div className="flex flex-wrap gap-3 border-b border-zinc-100 p-4 dark:border-zinc-800">
        <div className="relative min-w-56 flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" aria-hidden="true" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by employee, reference or subject"
            className="pl-9"
            aria-label="Search letters"
          />
        </div>
        <Select value={category} onChange={(e) => setCategory(e.target.value as LetterCategory | '')} className="w-48" aria-label="Letter type">
          <option value="">All types</option>
          {LETTER_CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {categoryLabel(c)}
            </option>
          ))}
        </Select>
      </div>
      {list.isLoading ? (
        <SkeletonRows rows={5} cols={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={FileText}
          title={q || category ? 'No letters match' : 'No letters issued yet'}
          description="Issued letters appear here with their reference numbers."
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Reference</Th>
              <Th>Employee</Th>
              <Th>Subject</Th>
              <Th>Issued</Th>
              <Th>Visible to employee</Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((l) => (
              <Tr key={l.id} className="cursor-pointer" onClick={() => navigate(`/letters/${l.id}`)}>
                <Td className="font-mono text-xs">{l.referenceNo}</Td>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{l.employeeName}</p>
                  <p className="font-mono text-xs text-zinc-500">{l.employeeCode}</p>
                </Td>
                <Td>
                  <p className="max-w-xs truncate text-zinc-900 dark:text-zinc-100">{l.subject}</p>
                  <p className="text-xs text-zinc-500">{categoryLabel(l.category)}</p>
                </Td>
                <Td>
                  {formatDate(l.issuedOn)}
                  {l.issuedByName && <p className="text-xs text-zinc-500">by {l.issuedByName}</p>}
                </Td>
                <Td>{l.sharedWithEmployee ? <Badge tone="green">Shared</Badge> : <Badge>HR only</Badge>}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Card>
  );
}

function TemplatesGrid({ onUse }: { onUse: (t: LetterTemplate) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [editing, setEditing] = useState<LetterTemplate | 'new' | null>(null);
  const [deleting, setDeleting] = useState<LetterTemplate | null>(null);
  const templates = useQuery({ queryKey: qk.letterTemplates, queryFn: lettersApi.templates });
  const remove = useMutation({
    mutationFn: (id: string) => lettersApi.deleteTemplate(id),
    onSuccess: () => {
      toast.success('Template deleted');
      qc.invalidateQueries({ queryKey: qk.letterTemplates });
      setDeleting(null);
    },
    onError: (err) => toast.error(err),
  });

  if (templates.isError) {
    return (
      <Card>
        <ErrorState error={templates.error} onRetry={() => templates.refetch()} />
      </Card>
    );
  }

  return (
    <>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <button
          type="button"
          onClick={() => setEditing('new')}
          className="flex min-h-44 flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-zinc-200 text-sm font-medium text-zinc-500 transition-colors hover:border-accent-400 hover:text-accent-700 dark:border-zinc-800 dark:text-zinc-400"
        >
          <Plus className="size-5" aria-hidden="true" />
          New template
        </button>
        {templates.isLoading
          ? Array.from({ length: 5 }).map((_, i) => (
              <Card key={i} className="p-5">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="mt-3 h-5 w-40" />
                <Skeleton className="mt-4 h-16 w-full" />
              </Card>
            ))
          : templates.data?.map((t) => (
              <Card key={t.id} className="group flex flex-col p-5">
                <div className="flex items-start justify-between gap-2">
                  <Badge tone={t.isActive ? 'accent' : 'gray'}>{categoryLabel(t.category)}</Badge>
                  <div className="flex gap-1 opacity-100 transition-opacity sm:opacity-0 sm:group-focus-within:opacity-100 sm:group-hover:opacity-100">
                    <IconButton label={`Edit ${t.name}`} onClick={() => setEditing(t)}>
                      <Pencil className="size-4" />
                    </IconButton>
                    <IconButton label={`Delete ${t.name}`} onClick={() => setDeleting(t)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                </div>
                <p className="mt-3 font-semibold text-zinc-900 dark:text-white">{t.name}</p>
                <p className="mt-2 line-clamp-3 flex-1 font-serif text-sm leading-relaxed text-zinc-500 dark:text-zinc-400">
                  <Highlighted text={t.body} />
                </p>
                <Button variant="secondary" size="sm" className="mt-4 self-start" disabled={!t.isActive} onClick={() => onUse(t)}>
                  Use template
                </Button>
              </Card>
            ))}
      </div>
      {editing && <TemplateModal template={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!deleting}
        title="Delete template?"
        message={<>“{deleting?.name}” will be removed. Letters already issued from it stay as they are.</>}
        confirmLabel="Delete"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}

function TemplateModal({ template, onClose }: { template: LetterTemplate | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [form, setForm] = useState<SaveLetterTemplateRequest>(
    template ?? { name: '', category: 'Other', subject: '', body: 'Dear {{EmployeeName}},\n\n', isActive: true },
  );
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: async () => {
      if (template) await lettersApi.updateTemplate(template.id, form);
      else await lettersApi.createTemplate(form);
    },
    onSuccess: () => {
      toast.success(template ? 'Template saved' : 'Template created');
      qc.invalidateQueries({ queryKey: qk.letterTemplates });
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!form.name.trim() || !form.subject.trim() || !form.body.trim()) return setError('Name, subject and body are required.');
    save.mutate();
  };

  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={template ? 'Edit template' : 'New template'}
      description="Click a field to insert it where your cursor is. It is filled from the employee's record when you issue the letter."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="template-form" loading={save.isPending}>
            Save template
          </Button>
        </>
      }
    >
      <form id="template-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Template name" required>
            <Input value={form.name} maxLength={150} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </Field>
          <Field label="Type">
            <Select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value as LetterCategory })}>
              {LETTER_CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {categoryLabel(c)}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Field label="Subject" required>
          <Input value={form.subject} maxLength={300} onChange={(e) => setForm({ ...form, subject: e.target.value })} />
        </Field>
        <BodyEditor value={form.body} onChange={(body) => setForm({ ...form, body })} />
        <Switch
          checked={form.isActive}
          onChange={(isActive) => setForm({ ...form, isActive })}
          label="Active"
          description="Inactive templates cannot be used to issue letters."
        />
        <div className="grid gap-4 sm:grid-cols-2">
          <Switch
            checked={!!form.requiresApproval}
            onChange={(requiresApproval) => setForm({ ...form, requiresApproval })}
            label="Needs approval"
            description="Goes through the letter approval chain before the employee gets it."
          />
          <Switch
            checked={!!form.selfService}
            onChange={(selfService) => setForm({ ...form, selfService })}
            label="Employees can ask for it"
            description="Shown under My documents → Request a letter (always approved first)."
          />
          <Field label="Issue automatically when" hint="One template per event.">
            <Select value={form.autoUse ?? ''} onChange={(e) => setForm({ ...form, autoUse: (e.target.value || null) as LetterAutoUse | null })}>
              <option value="">Never</option>
              <option value="Offer">A candidate gets an offer (candidate letter)</option>
              <option value="Hire">A candidate is hired</option>
              <option value="Confirmation">Someone is confirmed after probation</option>
              <option value="JobChange">A transfer or promotion is recorded</option>
              <option value="SalaryRevision">A salary is revised</option>
              <option value="Exit">A separation is completed</option>
            </Select>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Top margin (mm)" hint="Room for a printed letterhead">
              <Input
                type="number"
                min={5}
                max={80}
                value={form.marginTopMm ?? 25}
                onChange={(e) => setForm({ ...form, marginTopMm: Number(e.target.value) || 25 })}
              />
            </Field>
            <Field label="Side margins (mm)">
              <Input
                type="number"
                min={5}
                max={60}
                value={form.marginSideMm ?? 20}
                onChange={(e) => setForm({ ...form, marginSideMm: Number(e.target.value) || 20 })}
              />
            </Field>
          </div>
        </div>
        <Field label="Footer" hint="Printed at the bottom, e.g. registered office or a confidentiality line.">
          <Input value={form.footer ?? ''} maxLength={500} onChange={(e) => setForm({ ...form, footer: e.target.value || null })} />
        </Field>
      </form>
    </Modal>
  );
}

function IssueLetterDrawer({ initial, onClose }: { initial: LetterTemplate | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const employees = useQuery({ queryKey: [...qk.employeeLookup, 'all'], queryFn: employeesApi.lookupAll });
  const templates = useQuery({ queryKey: qk.letterTemplates, queryFn: lettersApi.templates });
  const [employeeId, setEmployeeId] = useState('');
  const [templateId, setTemplateId] = useState(initial?.id ?? '');
  const [category, setCategory] = useState<LetterCategory>(initial?.category ?? 'Other');
  const [subject, setSubject] = useState(initial?.subject ?? '');
  const [body, setBody] = useState(initial?.body ?? '');
  const [issuedOn, setIssuedOn] = useState(todayIso());
  const [share, setShare] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const applyTemplate = (id: string) => {
    setTemplateId(id);
    const t = templates.data?.find((x) => x.id === id);
    if (t) {
      setCategory(t.category);
      setSubject(t.subject);
      setBody(t.body);
    }
  };

  const draft = useDebounced({ employeeId, subject, body }, 400);
  const preview = useQuery({
    queryKey: ['letters', 'preview', draft],
    queryFn: () => lettersApi.preview(draft.employeeId, draft.subject, draft.body),
    enabled: !!draft.employeeId && !!draft.body.trim(),
    placeholderData: (prev) => prev,
  });

  // Keep the first employee preselected once the list loads.
  useEffect(() => {
    if (!employeeId && employees.data?.length) setEmployeeId(employees.data[0].id);
  }, [employees.data, employeeId]);

  const issue = useMutation({
    mutationFn: () => lettersApi.issue({ employeeId, category, subject, body, issuedOn, shareWithEmployee: share }),
    onSuccess: (l) => {
      toast.success('Letter issued', l.referenceNo);
      qc.invalidateQueries({ queryKey: qk.letters });
      onClose();
      navigate(`/letters/${l.id}`);
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!employeeId) return setError('Choose the employee.');
    if (!subject.trim() || !body.trim()) return setError('The letter needs a subject and a body.');
    issue.mutate();
  };

  const unknown = preview.data?.unknownPlaceholders ?? [];

  return (
    <Drawer
      open
      onClose={onClose}
      width="xl"
      title="Issue letter"
      description="Edit freely; the preview shows exactly what the employee gets."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="issue-letter-form" loading={issue.isPending} disabled={unknown.length > 0}>
            Issue letter
          </Button>
        </>
      }
    >
      <form id="issue-letter-form" onSubmit={submit} className="space-y-4">
        <FormError message={error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Employee" required>
            <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} disabled={employees.isLoading}>
              {employees.data?.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Start from template">
            <Select value={templateId} onChange={(e) => applyTemplate(e.target.value)}>
              <option value="">Blank letter</option>
              {templates.data
                ?.filter((t) => t.isActive)
                .map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
            </Select>
          </Field>
          <Field label="Type">
            <Select value={category} onChange={(e) => setCategory(e.target.value as LetterCategory)}>
              {LETTER_CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {categoryLabel(c)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Letter date">
            <Input type="date" value={issuedOn} onChange={(e) => setIssuedOn(e.target.value)} />
          </Field>
        </div>
        <Field label="Subject" required>
          <Input value={subject} maxLength={300} onChange={(e) => setSubject(e.target.value)} />
        </Field>

        <div className="grid gap-4 2xl:grid-cols-2">
          <BodyEditor value={body} onChange={setBody} rows={14} />
          <section aria-label="Preview" className="rounded-xl border border-zinc-200 bg-zinc-50 p-5 dark:border-zinc-800 dark:bg-zinc-950/40">
            <div className="mb-3 flex items-center justify-between">
              <p className="text-xs font-semibold tracking-wide text-zinc-500 uppercase">Preview</p>
              {preview.isFetching && <Spinner className="size-3.5" />}
            </div>
            {unknown.length > 0 && (
              <p className="mb-3 flex items-start gap-2 rounded-lg bg-amber-50 p-2.5 text-xs text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
                Unknown field{unknown.length === 1 ? '' : 's'}: {unknown.map((k) => `{{${k}}}`).join(', ')}. Fix or remove before issuing.
              </p>
            )}
            {preview.data ? (
              <article className="font-serif text-[15px] leading-relaxed whitespace-pre-wrap text-zinc-800 dark:text-zinc-200">
                <p className="mb-4 font-sans font-semibold">{preview.data.subject}</p>
                {preview.data.body}
              </article>
            ) : (
              <p className="text-sm text-zinc-500">Choose an employee and write the letter to see it filled in.</p>
            )}
          </section>
        </div>

        <Switch
          checked={share}
          onChange={setShare}
          label="Show in the employee's documents"
          description="Turn off for letters only HR should keep, such as warnings under review."
        />
      </form>
    </Drawer>
  );
}
