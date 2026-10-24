import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardList, Pencil, Plus, Trash2 } from 'lucide-react';
import { interviewTemplatesApi, type InterviewQuestion, type InterviewTemplate, type QuestionMark } from '../../api/recruitment';
import {
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  IconButton,
  Input,
  Modal,
  Select,
  SkeletonRows,
  useToast,
} from '../../components/ui';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

const KEY = ['recruitment', 'interview-templates'] as const;

/** Question sets with marks; an interview picks one so every interviewer scores the same questions. */
export function InterviewTemplatesTab() {
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: KEY, queryFn: interviewTemplatesApi.list });
  const [editing, setEditing] = useState<InterviewTemplate | 'new' | null>(null);
  const [removing, setRemoving] = useState<InterviewTemplate | null>(null);
  const remove = useMutation({
    mutationFn: (t: InterviewTemplate) => interviewTemplatesApi.remove(t.id),
    onSuccess: () => {
      setRemoving(null);
      qc.invalidateQueries({ queryKey: KEY });
    },
    onError: (e) => toast.error(e),
  });

  return (
    <>
      <div className="mb-4 flex justify-end">
        <Button icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
          New template
        </Button>
      </div>
      {list.isLoading ? (
        <Card>
          <SkeletonRows rows={3} cols={2} />
        </Card>
      ) : list.isError ? (
        <Card>
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        </Card>
      ) : !list.data?.length ? (
        <Card>
          <EmptyState
            icon={ClipboardList}
            title="No interview templates yet"
            description="Make a set of questions with marks, then choose it on an interview so every interviewer scores the same way."
          />
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {list.data.map((t) => {
            const max = t.questions.reduce((s, q) => s + q.maxMarks, 0);
            return (
              <Card key={t.id} className="p-5">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="truncate font-medium text-zinc-900 dark:text-white">{t.name}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {t.questions.length} {tx('questions')} · {tx('out of')} {max}
                    </p>
                  </div>
                  <div className="flex gap-1">
                    <IconButton label="Edit" onClick={() => setEditing(t)}>
                      <Pencil className="size-4" />
                    </IconButton>
                    <IconButton label="Delete" onClick={() => setRemoving(t)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                </div>
                <ol className="mt-3 space-y-1 text-sm text-zinc-600 dark:text-zinc-300">
                  {t.questions.slice(0, 4).map((q, i) => (
                    <li key={q.id ?? i} className="flex gap-2">
                      <span className="text-zinc-400">{i + 1}.</span>
                      <span className="min-w-0 flex-1 truncate">{q.text}</span>
                      <span className="text-xs text-zinc-500 tabular-nums dark:text-zinc-400">{q.maxMarks}</span>
                    </li>
                  ))}
                  {t.questions.length > 4 && <li className="text-xs text-zinc-500 dark:text-zinc-400">+{t.questions.length - 4}</li>}
                </ol>
              </Card>
            );
          })}
        </div>
      )}
      {editing && <TemplateModal template={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!removing}
        title="Delete this template?"
        message="Templates already used on an interview can't be deleted."
        confirmLabel="Delete"
        loading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing)}
        onClose={() => setRemoving(null)}
      />
    </>
  );
}

const blank = (): InterviewQuestion => ({ id: null, text: '', category: null, maxMarks: 10 });

function TemplateModal({ template, onClose }: { template: InterviewTemplate | null; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [name, setName] = useState(template?.name ?? '');
  const [questions, setQuestions] = useState<InterviewQuestion[]>(template?.questions.length ? template.questions : [blank()]);
  const set = (i: number, patch: Partial<InterviewQuestion>) => setQuestions((qs) => qs.map((q, j) => (j === i ? { ...q, ...patch } : q)));
  const valid = name.trim() && questions.length > 0 && questions.every((q) => q.text.trim() && q.maxMarks >= 1 && q.maxMarks <= 100);
  const save = useMutation({
    mutationFn: () => {
      const body = { name: name.trim(), questions: questions.map((q) => ({ ...q, text: q.text.trim(), category: q.category?.trim() || null })) };
      return template ? interviewTemplatesApi.update(template.id, body) : interviewTemplatesApi.add(body);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: KEY });
      toast.success('Template saved');
      onClose();
    },
    onError: (e) => toast.error(e),
  });

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={template ? 'Edit template' : 'New interview template'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!valid} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <Field label="Name" required>
          <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} placeholder="Technical round" />
        </Field>
        <div className="space-y-3">
          {questions.map((q, i) => (
            <div key={i} className="rounded-xl border border-zinc-200 p-3 dark:border-zinc-700">
              <div className="flex items-start gap-2">
                <span className="mt-2 text-xs font-semibold text-zinc-500 tabular-nums dark:text-zinc-400">{i + 1}</span>
                <div className="grid min-w-0 flex-1 gap-2 sm:grid-cols-[1fr_9rem_6rem]">
                  <Input value={q.text} maxLength={500} onChange={(e) => set(i, { text: e.target.value })} placeholder="Question" aria-label="Question" />
                  <Input value={q.category ?? ''} maxLength={100} onChange={(e) => set(i, { category: e.target.value })} placeholder="Category" aria-label="Category" />
                  <Input type="number" min={1} max={100} value={q.maxMarks} onChange={(e) => set(i, { maxMarks: Number(e.target.value) })} aria-label="Out of" />
                </div>
                <IconButton label="Remove question" disabled={questions.length === 1} onClick={() => setQuestions((qs) => qs.filter((_, j) => j !== i))}>
                  <Trash2 className="size-4" />
                </IconButton>
              </div>
            </div>
          ))}
          <Button variant="secondary" size="sm" icon={<Plus className="size-3.5" />} onClick={() => setQuestions((qs) => [...qs, blank()])}>
            Add question
          </Button>
        </div>
      </div>
    </Modal>
  );
}

/** Marks per question for one interview: the signed-in interviewer gives theirs; the recruiter sees everyone's. */
export function ScorecardModal({ interviewId, canRun, iAmInterviewer, onClose }: { interviewId: string; canRun: boolean; iAmInterviewer: boolean; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = ['recruitment', 'scorecard', interviewId];
  const card = useQuery({ queryKey: key, queryFn: () => interviewTemplatesApi.scorecard(interviewId) });
  const templates = useQuery({ queryKey: KEY, queryFn: interviewTemplatesApi.list, enabled: canRun });
  const [marks, setMarks] = useState<Record<string, number>>({});
  useEffect(() => {
    if (card.data) setMarks(Object.fromEntries(card.data.mine.map((m) => [m.questionId, m.marks])));
  }, [card.data]);

  const setTemplate = useMutation({
    mutationFn: (templateId: string | null) => interviewTemplatesApi.setForInterview(interviewId, templateId),
    onSuccess: () => qc.invalidateQueries({ queryKey: key }),
    onError: (e) => toast.error(e),
  });
  const save = useMutation({
    mutationFn: () =>
      interviewTemplatesApi.saveMarks(
        interviewId,
        Object.entries(marks).map(([questionId, m]): QuestionMark => ({ questionId, marks: m })),
      ),
    onSuccess: () => {
      toast.success('Marks saved');
      qc.invalidateQueries({ queryKey: key });
    },
    onError: (e) => toast.error(e),
  });

  const c = card.data;
  const questions = c?.questions ?? [];
  const max = questions.reduce((s, q) => s + q.maxMarks, 0);
  const myTotal = questions.reduce((s, q) => s + (q.id ? (marks[q.id] ?? 0) : 0), 0);
  const complete = questions.every((q) => q.id && marks[q.id] !== undefined);

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Scorecard"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
          {iAmInterviewer && questions.length > 0 && (
            <Button loading={save.isPending} disabled={!complete} onClick={() => save.mutate()}>
              Save my marks
            </Button>
          )}
        </>
      }
    >
      {card.isLoading ? (
        <SkeletonRows rows={4} cols={2} />
      ) : card.isError ? (
        <ErrorState error={card.error} onRetry={() => card.refetch()} />
      ) : (
        <div className="space-y-5">
          {canRun && (
            <Field label="Template" hint="Can't be changed once marks are given">
              <Select value={c?.templateId ?? ''} disabled={setTemplate.isPending} onChange={(e) => setTemplate.mutate(e.target.value || null)}>
                <option value="">{tx('None')}</option>
                {(templates.data ?? []).map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          {!questions.length ? (
            <EmptyState icon={ClipboardList} title="No template on this interview" description={canRun ? 'Choose one above.' : 'Ask the recruiter to choose one.'} className="py-6" />
          ) : (
            <>
              {iAmInterviewer && (
                <div>
                  <div className="mb-2 flex items-baseline justify-between">
                    <p className="text-sm font-semibold text-zinc-900 dark:text-white">{tx('Your marks')}</p>
                    <p className="text-sm text-zinc-500 tabular-nums dark:text-zinc-400">
                      {myTotal} / {max}
                    </p>
                  </div>
                  <ul className="divide-y divide-zinc-100 rounded-xl border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-700">
                    {questions.map((q, i) => (
                      <li key={q.id ?? i} className="flex items-center gap-3 px-3 py-2.5">
                        <span className="min-w-0 flex-1 text-sm">
                          <span className="text-zinc-900 dark:text-zinc-100">{q.text}</span>
                          {q.category && <span className="block text-xs text-zinc-500 dark:text-zinc-400">{q.category}</span>}
                        </span>
                        <Input
                          type="number"
                          min={0}
                          max={q.maxMarks}
                          className="w-20"
                          aria-label={`${q.text}, out of ${q.maxMarks}`}
                          value={q.id && marks[q.id] !== undefined ? marks[q.id] : ''}
                          onChange={(e) =>
                            q.id && setMarks((m) => ({ ...m, [q.id!]: Math.max(0, Math.min(q.maxMarks, Number(e.target.value))) }))
                          }
                        />
                        <span className="w-10 text-xs text-zinc-500 tabular-nums dark:text-zinc-400">/ {q.maxMarks}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              {c?.canSeeAll && c.all.length > 0 && (
                <div>
                  <p className="mb-2 text-sm font-semibold text-zinc-900 dark:text-white">{tx('All interviewers')}</p>
                  <ul className="space-y-2">
                    {c.all.map((r) => {
                      const pct = r.max ? Math.round((r.total / r.max) * 100) : 0;
                      return (
                        <li key={r.interviewerEmployeeId} className="rounded-lg bg-zinc-50 px-3 py-2 dark:bg-zinc-800/50">
                          <div className="flex items-center justify-between text-sm">
                            <span className="font-medium text-zinc-900 dark:text-zinc-100">{r.interviewer}</span>
                            <span className="text-zinc-600 tabular-nums dark:text-zinc-300">
                              {r.total} / {r.max} ({pct}%)
                            </span>
                          </div>
                          <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-700">
                            <div className={cn('h-full rounded-full', pct >= 60 ? 'bg-emerald-500' : 'bg-amber-500')} style={{ width: `${pct}%` }} />
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </Modal>
  );
}
