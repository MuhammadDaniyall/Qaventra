import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { Award, CheckCircle2, Clock, Printer, XCircle } from 'lucide-react';
import { quizApi, quizKeys, type QuizPaper, type QuizResult } from '../../api/quizzes';
import { publicApi } from '../../api/publicTools';
import { Badge, Button, Card, CardHeader, ErrorState, PageHeader, PageSpinner, useToast } from '../../components/ui';
import { formatDate } from '../../lib/format';
import { cn } from '../../lib/cn';

const utc = (u: string) => new Date(/[zZ]$/.test(u) ? u : `${u}Z`).getTime();

/** Taking a quiz: questions without answers, a countdown when there is a time limit, then the marked result. */
export function TakeQuizPage() {
  const { id = '' } = useParams();
  const qc = useQueryClient();
  const toast = useToast();
  const [paper, setPaper] = useState<QuizPaper | null>(null);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [result, setResult] = useState<QuizResult | null>(null);
  const [now, setNow] = useState(() => Date.now());

  const start = useMutation({ mutationFn: () => quizApi.start(id), onSuccess: setPaper, onError: (e) => toast.error(e) });
  const submit = useMutation({
    mutationFn: () => quizApi.submit(paper!.attemptId, answers),
    onSuccess: (r) => {
      setResult(r);
      qc.invalidateQueries({ queryKey: quizKeys.all });
      qc.invalidateQueries({ queryKey: quizKeys.certificates });
    },
    onError: (e) => toast.error(e),
  });

  useEffect(() => {
    if (!paper?.deadlineUtc || result) return;
    const t = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(t);
  }, [paper, result]);

  const secondsLeft = paper?.deadlineUtc ? Math.max(0, Math.floor((utc(paper.deadlineUtc) - now) / 1000)) : null;
  const autoSent = useRef(false);
  useEffect(() => {
    // Hand the answers in automatically (once) when the time is up.
    if (secondsLeft === 0 && paper && !result && !autoSent.current) {
      autoSent.current = true;
      submit.mutate();
    }
  }, [secondsLeft, paper, result, submit]);

  if (result) return <ResultView result={result} />;

  if (!paper)
    return (
      <>
        <PageHeader title="Quiz" back={{ to: '/training?tab=quizzes', label: 'Training' }} />
        <Card className="p-6">
          <p className="text-sm text-zinc-600 dark:text-zinc-300">
            When you start, the clock starts (if the quiz has a time limit) and the attempt counts. Answer every question, then press Submit.
          </p>
          <Button className="mt-4" loading={start.isPending} onClick={() => start.mutate()}>
            Start quiz
          </Button>
        </Card>
      </>
    );

  const answered = Object.keys(answers).length;
  return (
    <>
      <PageHeader
        title={paper.title}
        description={`${paper.program} · pass mark ${paper.passPercent}%`}
        actions={
          secondsLeft !== null && (
            <Badge tone={secondsLeft < 60 ? 'red' : 'blue'}>
              <Clock className="me-1 inline size-3.5" />
              {Math.floor(secondsLeft / 60)}:{String(secondsLeft % 60).padStart(2, '0')}
            </Badge>
          )
        }
      />
      {paper.instructions && <p className="mb-4 text-sm text-zinc-600 dark:text-zinc-300">{paper.instructions}</p>}
      <ol className="space-y-4">
        {paper.questions.map((q, i) => (
          <li key={q.id}>
            <Card className="p-4">
              <p className="font-medium text-zinc-900 dark:text-white">
                {i + 1}. {q.text}
              </p>
              <div className="mt-3 grid gap-2">
                {q.options.map((o, k) => (
                  <label
                    key={k}
                    className={cn(
                      'flex cursor-pointer items-center gap-3 rounded-lg border px-3 py-2 text-sm',
                      answers[q.id] === k
                        ? 'border-accent-500 bg-accent-50 dark:bg-accent-500/10'
                        : 'border-zinc-200 hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800',
                    )}
                  >
                    <input type="radio" name={q.id} checked={answers[q.id] === k} onChange={() => setAnswers((a) => ({ ...a, [q.id]: k }))} />
                    {o}
                  </label>
                ))}
              </div>
            </Card>
          </li>
        ))}
      </ol>
      <div className="mt-5 flex items-center gap-3">
        <Button loading={submit.isPending} onClick={() => submit.mutate()}>
          Submit answers
        </Button>
        <span className="text-sm text-zinc-500">
          {answered} of {paper.questions.length} answered
        </span>
      </div>
    </>
  );
}

function ResultView({ result }: { result: QuizResult }) {
  return (
    <>
      <PageHeader title={result.passed ? 'You passed!' : 'Not passed this time'} back={{ to: '/training?tab=quizzes', label: 'Training' }} />
      <Card className="mb-5 flex flex-wrap items-center gap-4 p-5">
        {result.passed ? <CheckCircle2 className="size-10 text-emerald-500" /> : <XCircle className="size-10 text-red-500" />}
        <div className="flex-1">
          <p className="text-2xl font-semibold text-zinc-900 dark:text-white">{result.scorePercent}%</p>
          <p className="text-sm text-zinc-500">
            {result.correct} of {result.total} right · pass mark {result.passPercent}%{result.timedOut ? ' · submitted after the time limit' : ''}
          </p>
        </div>
        {result.certificateId && (
          <Link to={`/training/certificates/${result.certificateId}`}>
            <Button icon={<Award className="size-4" />}>View certificate</Button>
          </Link>
        )}
      </Card>
      <Card>
        <CardHeader title="Answers" />
        <ol className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {result.review.map((r, i) => (
            <li key={r.questionId} className="px-5 py-3 text-sm">
              <p className="font-medium text-zinc-900 dark:text-white">
                {i + 1}. {r.text}
              </p>
              <ul className="mt-1 space-y-0.5">
                {r.options.map((o, k) => (
                  <li
                    key={k}
                    className={cn(
                      k === r.correctIndex && 'font-medium text-emerald-700 dark:text-emerald-300',
                      k === r.chosen && k !== r.correctIndex && 'text-red-600 line-through',
                    )}
                  >
                    {k === r.correctIndex ? '✓ ' : k === r.chosen ? '✗ ' : '· '}
                    {o}
                  </li>
                ))}
              </ul>
            </li>
          ))}
        </ol>
      </Card>
    </>
  );
}

/** A4 certificate, laid out for printing (or saving as PDF from the print dialog). */
export function CertificatePage() {
  const { id = '' } = useParams();
  const cert = useQuery({ queryKey: quizKeys.certificate(id), queryFn: () => quizApi.certificate(id) });
  if (cert.isLoading) return <PageSpinner />;
  if (cert.isError) return <ErrorState error={cert.error} onRetry={() => cert.refetch()} />;
  const c = cert.data!;
  const verifyUrl = `${window.location.origin}${c.verifyPath}`;
  return (
    <div className="min-h-screen bg-zinc-100 py-6 print:bg-white print:py-0">
      <style>{'@page { size: A4 landscape; margin: 0 } @media print { .no-print { display: none !important } }'}</style>
      <div className="no-print mx-auto mb-4 flex max-w-[297mm] justify-between px-4">
        <Link to="/training?tab=quizzes" className="text-sm text-accent-600 hover:underline">
          ← Training
        </Link>
        <Button icon={<Printer className="size-4" />} onClick={() => window.print()}>
          Print / save as PDF
        </Button>
      </div>
      <div className="mx-auto flex aspect-[297/210] w-full max-w-[297mm] flex-col items-center justify-between border-[10px] border-double border-amber-600 bg-white p-[12mm] text-center text-zinc-900 shadow-lg print:shadow-none">
        <div className="flex flex-col items-center gap-2">
          {c.logoDataUrl && <img src={c.logoDataUrl} alt="" className="h-16 object-contain" />}
          <p className="text-lg font-semibold tracking-wide">{c.companyName}</p>
        </div>
        <div>
          <p className="font-serif text-4xl tracking-wide text-amber-700">Certificate of Completion</p>
          <p className="mt-6 text-sm uppercase tracking-widest text-zinc-500">This certifies that</p>
          <p className="mt-2 font-serif text-4xl font-semibold">{c.employeeName}</p>
          <p className="mt-4 text-sm uppercase tracking-widest text-zinc-500">has successfully completed</p>
          <p className="mt-2 text-2xl font-semibold">{c.courseTitle}</p>
          <p className="mt-2 text-sm text-zinc-500">with a score of {c.scorePercent}%</p>
        </div>
        <div className="flex w-full items-end justify-between text-xs text-zinc-500">
          <div className="text-start">
            <p className="text-sm font-medium text-zinc-800">{formatDate(c.issuedOn)}</p>
            <p>Date</p>
          </div>
          <div className="text-end">
            <p className="font-mono text-sm text-zinc-800">{c.code}</p>
            <p>Verify at {verifyUrl}</p>
          </div>
        </div>
      </div>
    </div>
  );
}

/** Public: anyone with the code sees the name, course and date, nothing else. */
export function VerifyCertificatePage() {
  const { code = '' } = useParams();
  const res = useQuery({ queryKey: ['public', 'verify', code], queryFn: () => publicApi.verify(code), retry: false });
  useEffect(() => {
    document.title = 'Verify a certificate · Qaventra';
  }, []);
  return (
    <div className="flex min-h-screen items-center justify-center bg-zinc-50 p-4 dark:bg-zinc-950">
      <Card className="w-full max-w-md p-6 text-center">
        {res.isLoading ? (
          <PageSpinner />
        ) : res.data ? (
          <>
            <CheckCircle2 className="mx-auto size-12 text-emerald-500" />
            <h1 className="mt-3 text-lg font-semibold text-zinc-900 dark:text-white">Certificate is genuine</h1>
            <dl className="mt-4 space-y-2 text-sm">
              <div>
                <dt className="text-zinc-500">Name</dt>
                <dd className="font-medium text-zinc-900 dark:text-white">{res.data.employeeName}</dd>
              </div>
              <div>
                <dt className="text-zinc-500">Course</dt>
                <dd className="font-medium text-zinc-900 dark:text-white">{res.data.courseTitle}</dd>
              </div>
              <div>
                <dt className="text-zinc-500">Date</dt>
                <dd className="font-medium text-zinc-900 dark:text-white">{formatDate(res.data.issuedOn)}</dd>
              </div>
            </dl>
          </>
        ) : (
          <>
            <XCircle className="mx-auto size-12 text-red-500" />
            <h1 className="mt-3 text-lg font-semibold text-zinc-900 dark:text-white">No certificate with this code</h1>
            <p className="mt-1 text-sm text-zinc-500">Check the code and try again.</p>
          </>
        )}
        <p className="mt-6 text-xs text-zinc-400">
          Code {code.toUpperCase()} · <Link to="/tools">Free HR tools</Link>
        </p>
      </Card>
    </div>
  );
}
