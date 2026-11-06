import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Award, ClipboardCheck, ImageUp, ListChecks, Plus, Sparkles, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { quizApi, quizKeys, type QuizListItem, type QuizQuestionInput, type SaveQuiz } from '../../api/quizzes';
import { trainingApi, type TrainingMe } from '../../api/training';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  Select,
  SkeletonRows,
  Table,
  TBody,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatDate, formatDateTimeUtc } from '../../lib/format';
import { DeleteConfirm, RowActions } from '../settings/shared';

/** Course quizzes: everyone takes them and keeps their certificates; HR builds them (with an AI first draft) and sees results. */
export function QuizzesTab({ me }: { me: TrainingMe }) {
  const quizzes = useQuery({ queryKey: quizKeys.all, queryFn: quizApi.list });
  const [editing, setEditing] = useState<string | 'new' | null>(null);
  const [results, setResults] = useState<QuizListItem | null>(null);
  const [deleting, setDeleting] = useState<QuizListItem | null>(null);
  const [logo, setLogo] = useState(false);
  const takeable = quizzes.data?.filter((q) => q.isActive) ?? [];

  return (
    <div className="space-y-5">
      <Card>
        <CardHeader title="Quizzes to take" description="Pass a course quiz to get a certificate you can print and share." />
        {quizzes.isLoading ? (
          <SkeletonRows rows={3} cols={3} />
        ) : !takeable.length ? (
          <p className="px-5 py-6 text-sm text-zinc-500 dark:text-zinc-400">No quizzes yet.</p>
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {takeable.map((q) => {
              const left = q.maxAttempts === 0 ? null : q.maxAttempts - q.attemptsUsed;
              return (
                <li key={q.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
                  <ListChecks className="size-4 text-zinc-500 dark:text-zinc-400" />
                  <div className="min-w-0 flex-1">
                    <p className="font-medium text-zinc-900 dark:text-white">{q.title}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {q.program} · {q.questions} questions · pass {q.passPercent}%{q.timeLimitMinutes ? ` · ${q.timeLimitMinutes} min` : ''}
                      {left !== null ? ` · ${left} attempts left` : ''}
                      {q.bestScore !== null ? ` · best ${q.bestScore}%` : ''}
                    </p>
                  </div>
                  {q.passed && <Badge tone="green">Passed</Badge>}
                  {q.certificateId && (
                    <Link to={`/training/certificates/${q.certificateId}`}>
                      <Button size="sm" variant="secondary" icon={<Award className="size-4" />}>
                        Certificate
                      </Button>
                    </Link>
                  )}
                  {!q.passed && (left === null || left > 0 || q.openAttemptId) && (
                    <Link to={`/training/quizzes/${q.id}/take`}>
                      <Button size="sm">{q.openAttemptId ? 'Continue' : q.attemptsUsed ? 'Try again' : 'Start quiz'}</Button>
                    </Link>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </Card>

      <MyCertificates />

      {me.isHr && (
        <Card>
          <CardHeader
            title="Manage quizzes"
            description="Multiple-choice questions, a pass mark, a time limit and a number of attempts. Answers are checked on the server."
            actions={
              <div className="flex gap-2">
                <Button variant="secondary" size="sm" icon={<ImageUp className="size-4" />} onClick={() => setLogo(true)}>
                  Certificate logo
                </Button>
                <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
                  New quiz
                </Button>
              </div>
            }
          />
          {!quizzes.data?.length ? (
            <EmptyState icon={ClipboardCheck} title="No quizzes yet" description="Add a quiz to a course. The AI can write a first draft from the course text." />
          ) : (
            <Table>
              <THead>
                <Tr>
                  <Th>Quiz</Th>
                  <Th>Course</Th>
                  <Th>Questions</Th>
                  <Th>Pass mark</Th>
                  <Th>Taken by</Th>
                  <Th>Pass rate</Th>
                  <Th />
                </Tr>
              </THead>
              <TBody>
                {quizzes.data.map((q) => (
                  <Tr key={q.id}>
                    <Td>
                      <button className="font-medium text-accent-600 hover:underline" onClick={() => setResults(q)}>
                        {q.title}
                      </button>
                      {!q.isActive && (
                        <Badge tone="gray" className="ms-2">
                          Off
                        </Badge>
                      )}
                    </Td>
                    <Td>{q.program}</Td>
                    <Td>{q.questions}</Td>
                    <Td>{q.passPercent}%</Td>
                    <Td>{q.takers}</Td>
                    <Td>{q.passRate === null ? '—' : `${q.passRate}%`}</Td>
                    <Td>
                      <RowActions label={q.title} onEdit={() => setEditing(q.id)} onDelete={() => setDeleting(q)} />
                    </Td>
                  </Tr>
                ))}
              </TBody>
            </Table>
          )}
        </Card>
      )}

      {editing && <QuizEditor id={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      {results && <ResultsModal quiz={results} onClose={() => setResults(null)} />}
      {logo && <LogoModal onClose={() => setLogo(false)} />}
      <DeleteConfirm item={deleting} name={deleting?.title ?? ''} remove={(id) => quizApi.remove(id)} invalidate={[quizKeys.all]} onClose={() => setDeleting(null)} />
    </div>
  );
}

function MyCertificates() {
  const certs = useQuery({ queryKey: quizKeys.certificates, queryFn: quizApi.myCertificates });
  if (!certs.data?.length) return null;
  return (
    <Card>
      <CardHeader title="My certificates" />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {certs.data.map((c) => (
          <li key={c.id} className="flex flex-wrap items-center gap-3 px-5 py-3">
            <Award className="size-4 text-amber-500" />
            <div className="min-w-0 flex-1">
              <p className="font-medium text-zinc-900 dark:text-white">{c.courseTitle}</p>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">
                {formatDate(c.issuedOn)} · {c.scorePercent}% · {c.code}
              </p>
            </div>
            <Link to={`/training/certificates/${c.id}`} className="text-sm font-medium text-accent-600 hover:underline">
              Open
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  );
}

const blankQuestion = (): QuizQuestionInput => ({ text: '', options: ['', '', '', ''], correctIndex: 0 });

function QuizEditor({ id, onClose }: { id: string | null; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const programs = useQuery({ queryKey: qk.trainingPrograms, queryFn: trainingApi.programs });
  const existing = useQuery({ queryKey: quizKeys.one(id ?? ''), queryFn: () => quizApi.get(id!), enabled: !!id });
  const [form, setForm] = useState<SaveQuiz | null>(null);
  const [aiText, setAiText] = useState('');
  const [aiCount, setAiCount] = useState(5);
  const [showAi, setShowAi] = useState(false);

  const value: SaveQuiz | null =
    form ??
    (id
      ? existing.data ?? null
      : {
          programId: programs.data?.[0]?.id ?? '',
          title: '',
          instructions: null,
          passPercent: 60,
          timeLimitMinutes: null,
          maxAttempts: 3,
          isActive: true,
          questions: [blankQuestion()],
        });
  const set = (patch: Partial<SaveQuiz>) => value && setForm({ ...value, ...patch });
  const setQ = (i: number, patch: Partial<QuizQuestionInput>) =>
    value && set({ questions: value.questions.map((q, j) => (j === i ? { ...q, ...patch } : q)) });

  const save = useMutation({
    mutationFn: (body: SaveQuiz) => {
      const clean = { ...body, questions: body.questions.map((q) => ({ ...q, options: q.options.filter((o) => o.trim() !== '') })) };
      return id ? quizApi.update(id, clean) : quizApi.create(clean);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: quizKeys.all });
      toast.success('Quiz saved');
      onClose();
    },
  });
  const draft = useMutation({
    mutationFn: () => quizApi.draft(value!.programId, aiCount, aiText.trim() || null),
    onSuccess: (r) => {
      const kept = value!.questions.filter((q) => q.text.trim());
      set({ questions: [...kept, ...r.questions.map((q) => ({ ...q, options: [...q.options] }))] });
      setShowAi(false);
      toast.success('Questions drafted', 'Check each question and its right answer before saving.');
    },
    onError: (e) => toast.error(e),
  });

  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={id ? 'Edit quiz' : 'New quiz'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!value?.programId || !value.title.trim()} onClick={() => value && save.mutate(value)}>
            Save quiz
          </Button>
        </>
      }
    >
      {!value ? (
        <SkeletonRows rows={4} cols={2} />
      ) : (
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Course" required>
              <Select value={value.programId} onChange={(e) => set({ programId: e.target.value })}>
                <option value="">Choose…</option>
                {programs.data?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.title}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Title" required>
              <Input value={value.title} maxLength={150} onChange={(e) => set({ title: e.target.value })} />
            </Field>
            <Field label="Pass mark (%)">
              <Input type="number" min={1} max={100} value={value.passPercent} onChange={(e) => set({ passPercent: Number(e.target.value) })} />
            </Field>
            <Field label="Time limit (minutes)" hint="Leave empty for no limit.">
              <Input
                type="number"
                min={1}
                max={600}
                value={value.timeLimitMinutes ?? ''}
                onChange={(e) => set({ timeLimitMinutes: e.target.value ? Number(e.target.value) : null })}
              />
            </Field>
            <Field label="Attempts allowed" hint="0 = unlimited.">
              <Input type="number" min={0} max={50} value={value.maxAttempts} onChange={(e) => set({ maxAttempts: Number(e.target.value) })} />
            </Field>
            <div className="flex items-end pb-2">
              <Checkbox label="Open for employees" checked={value.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
            </div>
          </div>
          <Field label="Instructions">
            <Textarea rows={2} value={value.instructions ?? ''} onChange={(e) => set({ instructions: e.target.value || null })} />
          </Field>

          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="text-sm font-semibold text-zinc-900 dark:text-white">Questions ({value.questions.length})</h3>
            <div className="flex gap-2">
              <Button size="sm" variant="secondary" icon={<Sparkles className="size-4" />} onClick={() => setShowAi((v) => !v)} disabled={!value.programId}>
                Draft with AI
              </Button>
              <Button size="sm" variant="secondary" icon={<Plus className="size-4" />} onClick={() => set({ questions: [...value.questions, blankQuestion()] })}>
                Add question
              </Button>
            </div>
          </div>
          {showAi && (
            <div className="space-y-3 rounded-lg border border-accent-200 bg-accent-50/50 p-3 dark:border-accent-500/30 dark:bg-accent-500/5">
              <p className="text-xs text-zinc-600 dark:text-zinc-300">
                The AI writes questions from the course description, or from text you paste here. Nothing is saved until you check it and press Save.
              </p>
              <Textarea rows={4} placeholder="Paste course notes (optional)" value={aiText} onChange={(e) => setAiText(e.target.value)} />
              <div className="flex items-center gap-2">
                <Input type="number" min={3} max={20} className="w-24" value={aiCount} onChange={(e) => setAiCount(Number(e.target.value))} />
                <span className="text-xs text-zinc-500">questions</span>
                <Button size="sm" loading={draft.isPending} icon={<Sparkles className="size-4" />} onClick={() => draft.mutate()}>
                  Write draft
                </Button>
              </div>
            </div>
          )}
          <ol className="space-y-4">
            {value.questions.map((q, i) => (
              <li key={i} className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
                <div className="flex items-start gap-2">
                  <span className="mt-2 text-sm font-semibold text-zinc-500">{i + 1}.</span>
                  <Textarea rows={2} className="flex-1" placeholder="Question" value={q.text} onChange={(e) => setQ(i, { text: e.target.value })} />
                  <IconButton label="Remove question" onClick={() => set({ questions: value.questions.filter((_, j) => j !== i) })}>
                    <Trash2 className="size-4" />
                  </IconButton>
                </div>
                <div className="mt-2 grid gap-2 sm:grid-cols-2">
                  {q.options.map((o, k) => (
                    <label key={k} className="flex items-center gap-2">
                      <input
                        type="radio"
                        name={`correct-${i}`}
                        checked={q.correctIndex === k}
                        onChange={() => setQ(i, { correctIndex: k })}
                        aria-label={`Choice ${k + 1} is right`}
                      />
                      <Input
                        value={o}
                        placeholder={`Choice ${k + 1}`}
                        onChange={(e) => setQ(i, { options: q.options.map((x, m) => (m === k ? e.target.value : x)) })}
                      />
                    </label>
                  ))}
                </div>
                {q.options.length < 6 && (
                  <button className="mt-2 text-xs font-medium text-accent-600 hover:underline" onClick={() => setQ(i, { options: [...q.options, ''] })}>
                    + Add choice
                  </button>
                )}
              </li>
            ))}
          </ol>
          <p className="text-xs text-zinc-500 dark:text-zinc-400">Mark the right choice with the round button. Empty choices are left out.</p>
          <FormError message={save.error ? errorMessage(save.error) : null} />
        </div>
      )}
    </Modal>
  );
}

function ResultsModal({ quiz, onClose }: { quiz: QuizListItem; onClose: () => void }) {
  const rows = useQuery({ queryKey: quizKeys.attempts(quiz.id), queryFn: () => quizApi.attempts(quiz.id) });
  return (
    <Modal open onClose={onClose} size="lg" title={quiz.title} description={`${quiz.program} · pass ${quiz.passPercent}%`}>
      {rows.isLoading ? (
        <SkeletonRows rows={3} cols={3} />
      ) : !rows.data?.length ? (
        <p className="text-sm text-zinc-500">Nobody has taken this quiz yet.</p>
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Employee</Th>
              <Th>Submitted</Th>
              <Th>Score</Th>
              <Th>Result</Th>
            </Tr>
          </THead>
          <TBody>
            {rows.data.map((r) => (
              <Tr key={r.id}>
                <Td>
                  {r.employeeName} <span className="text-xs text-zinc-500">{r.employeeCode}</span>
                </Td>
                <Td>{formatDateTimeUtc(r.submittedAtUtc)}</Td>
                <Td>{r.scorePercent}%</Td>
                <Td>
                  <Badge tone={r.passed ? 'green' : 'red'}>{r.passed ? 'Passed' : r.timedOut ? 'Time ran out' : 'Not passed'}</Badge>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
    </Modal>
  );
}

function LogoModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const logo = useQuery({ queryKey: quizKeys.logo, queryFn: quizApi.logo });
  const [value, setValue] = useState<string | null | undefined>(undefined);
  const current = value === undefined ? logo.data?.logoDataUrl ?? null : value;
  const save = useMutation({
    mutationFn: () => quizApi.saveLogo(current),
    onSuccess: () => {
      toast.success('Logo saved');
      onClose();
    },
    onError: (e) => toast.error(e),
  });
  const pick = (file: File | undefined) => {
    if (!file) return;
    if (file.size > 200_000) return toast.error('The logo must be 200 KB or smaller.');
    const reader = new FileReader();
    reader.onload = () => setValue(String(reader.result));
    reader.readAsDataURL(file);
  };
  return (
    <Modal
      open
      onClose={onClose}
      title="Certificate logo"
      description="Printed at the top of every certificate. PNG, JPEG or SVG, up to 200 KB."
      footer={
        <>
          {current && (
            <Button variant="ghost" onClick={() => setValue(null)}>
              Remove
            </Button>
          )}
          <Button loading={save.isPending} onClick={() => save.mutate()}>
            Save
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        {current ? <img src={current} alt="Company logo" className="h-16 object-contain" /> : <p className="text-sm text-zinc-500">No logo yet.</p>}
        <input type="file" accept="image/png,image/jpeg,image/svg+xml" onChange={(e) => pick(e.target.files?.[0])} />
      </div>
    </Modal>
  );
}
