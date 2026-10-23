import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CheckCircle2, Circle, PartyPopper, Sparkles } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { onboardingApi, type Welcome } from '../../api/onboarding';
import { useUser } from '../../auth/AuthContext';
import { Button, Card, CardBody, CardHeader, EmptyState, Field, FormError, PageSpinner, Textarea, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { FirstDay } from './OnboardingDetailPage';
import { TaskList } from './shared';

/** The new joiner's page: first-day details, their checklist and introducing themselves. */
export function WelcomePage() {
  const user = useUser();
  const welcome = useQuery({ queryKey: qk.welcome, queryFn: onboardingApi.welcome });
  if (welcome.isLoading) return <PageSpinner />;
  const w = welcome.data;
  const o = w?.onboarding;
  const first = user.fullName.split(' ')[0];
  if (!w || !o)
    return (
      <Card className="mx-auto mt-10 max-w-xl">
        <EmptyState icon={PartyPopper} title={`Hi ${first}`} description="There is no onboarding for you right now." />
      </Card>
    );
  const mine = o.tasks.filter((t) => t.canDo);
  const others = o.tasks.filter((t) => !t.canDo);
  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 overflow-hidden rounded-2xl bg-gradient-to-br from-accent-600 to-violet-600 p-6 text-white shadow-lg sm:p-8">
        <p className="flex items-center gap-2 text-sm font-medium text-white/80">
          <Sparkles className="size-4" /> Welcome to {user.companyName}
        </p>
        <h1 className="mt-2 text-2xl font-semibold sm:text-3xl">So glad you are here, {first}!</h1>
        <p className="mt-2 max-w-2xl text-white/85">
          Here is everything for your first days: when and where to come, who to ask for, and a short checklist to get you set up.
        </p>
      </div>
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="space-y-6">
          <Card>
            <CardHeader title="Your checklist" />
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              <Step done={w.profileComplete} to="/me/profile" title="Check your details" hint="Phone, CNIC, address and bank account. Ask for corrections if anything is wrong." />
              <Step done={w.emergencyContacts > 0} to="/me/profile" title="Add an emergency contact" hint="Someone we can call if needed." />
              <Step done={w.hasPhoto} to="/me/documents" title="Upload a photo and your documents" hint="Your CNIC and certificates, so HR has them on file." />
              <Step done={o.summary.introduced} title="Introduce yourself" hint="A few words so your team knows you. See below." />
            </ul>
            {mine.length > 0 && <TaskList tasks={mine} />}
          </Card>
          {!o.summary.introduced ? <Introduce w={w} /> : (
            <Card>
              <CardBody className="flex items-center gap-2 text-sm text-emerald-700 dark:text-emerald-300">
                <CheckCircle2 className="size-4" /> Your team has your introduction. Welcome aboard!
              </CardBody>
            </Card>
          )}
          {others.length > 0 && (
            <Card>
              <CardHeader title="What others are preparing for you" description="So you know what to expect." />
              <TaskList tasks={others} />
            </Card>
          )}
        </div>
        <FirstDay d={o} />
      </div>
    </div>
  );
}

function Step({ done, title, hint, to }: { done: boolean; title: string; hint: string; to?: string }) {
  return (
    <li className="flex items-start gap-3 px-5 py-3">
      {done ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-500" /> : <Circle className="mt-0.5 size-5 shrink-0 text-zinc-300 dark:text-zinc-600" />}
      <div className="min-w-0 flex-1">
        <p className={cn('text-sm font-medium', done ? 'text-zinc-400 line-through' : 'text-zinc-900 dark:text-zinc-100')}>{title}</p>
        <p className="text-xs text-zinc-500">{hint}</p>
      </div>
      {to && !done && (
        <Link to={to} className="text-xs font-medium text-accent-600 hover:underline dark:text-accent-400">
          Open
        </Link>
      )}
    </li>
  );
}

function Introduce({ w }: { w: Welcome }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const send = useMutation({
    mutationFn: () => onboardingApi.introduce(Object.fromEntries(Object.entries(answers).map(([k, v]) => [k, v.trim() || null]))),
    onSuccess: () => {
      toast.success('Introduction shared', 'Your team will see it in Announcements.');
      qc.invalidateQueries({ queryKey: qk.welcome });
      qc.invalidateQueries({ queryKey: qk.announcements });
    },
    onError: (err) => setError(errorMessage(err)),
  });
  return (
    <Card>
      <CardHeader title="Introduce yourself" description="Shared with your department as a welcome post. Answer as many as you like." />
      <CardBody className="space-y-4">
        <FormError message={error} />
        {w.questions.map((q) => (
          <Field key={q.key} label={q.question}>
            <Textarea rows={2} maxLength={1000} value={answers[q.key] ?? ''} onChange={(e) => setAnswers((a) => ({ ...a, [q.key]: e.target.value }))} />
          </Field>
        ))}
        <div className="flex justify-end">
          <Button loading={send.isPending} disabled={!Object.values(answers).some((v) => v.trim())} onClick={() => send.mutate()}>
            Share with my team
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}

/** Dashboard nudge for someone being onboarded. Hidden otherwise. */
export function WelcomeBanner({ className }: { className?: string }) {
  const welcome = useQuery({ queryKey: qk.welcome, queryFn: onboardingApi.welcome });
  const o = welcome.data?.onboarding;
  if (!o || o.summary.status !== 'InProgress') return null;
  const left = o.tasks.filter((t) => t.canDo && !t.completedAtUtc).length + (o.summary.introduced ? 0 : 1);
  return (
    <Link to="/welcome" className={cn('flex items-center gap-4 rounded-xl border border-accent-200 bg-accent-50 p-4 transition hover:bg-accent-100 dark:border-accent-500/30 dark:bg-accent-500/10 dark:hover:bg-accent-500/15', className)}>
      <PartyPopper className="size-6 shrink-0 text-accent-600 dark:text-accent-300" />
      <div className="min-w-0 flex-1">
        <p className="font-medium text-zinc-900 dark:text-white">Welcome aboard! Your first days are planned.</p>
        <p className="text-sm text-zinc-600 dark:text-zinc-300">{left ? `${left} thing${left === 1 ? '' : 's'} left on your checklist.` : 'Your checklist is done.'}</p>
      </div>
      <span className="text-sm font-medium text-accent-700 dark:text-accent-300">Open</span>
    </Link>
  );
}
