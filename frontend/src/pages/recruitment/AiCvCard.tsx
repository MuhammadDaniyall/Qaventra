import { useMutation, useQuery } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';
import { aiApi } from '../../api/ai';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Button, Card, CardBody, CardHeader, ErrorState } from '../../components/ui';
import { cn } from '../../lib/cn';
import { useT } from '../../lib/i18n';

/** HR asks the AI to read the candidate's CV against the job: fit score, strengths, gaps and interview questions. */
export function AiCvCard({ applicationId }: { applicationId: string }) {
  const { t } = useT();
  const user = useUser();
  const status = useQuery({ queryKey: ['ai-status'], queryFn: aiApi.status, staleTime: 5 * 60_000, retry: false });
  const screen = useMutation({ mutationFn: (refresh: boolean) => aiApi.screenCv(applicationId, refresh) });
  if (!status.data?.enabled || !isHr(user)) return null;
  const r = screen.data;

  return (
    <Card>
      <CardHeader
        title={
          <span className="flex items-center gap-2">
            <Sparkles className="size-4 text-accent-500" /> {t('AI CV check')}
          </span>
        }
        description={t('Reads the CV against this job. A second opinion, not a decision.')}
        actions={
          <Button size="sm" variant="secondary" loading={screen.isPending} onClick={() => screen.mutate(!!r)}>
            {r ? t('Check again') : t('Check CV')}
          </Button>
        }
      />
      {(r || screen.isError) && (
        <CardBody className="space-y-3 text-sm">
          {screen.isError && <ErrorState error={screen.error} />}
          {r && (
            <>
              <div className="flex items-center gap-3">
                <span
                  className={cn(
                    'rounded-lg px-2.5 py-1 text-lg font-semibold',
                    r.score >= 70 ? 'bg-emerald-50 text-emerald-700' : r.score >= 45 ? 'bg-amber-50 text-amber-700' : 'bg-red-50 text-red-700',
                  )}
                >
                  {r.score}/100
                </span>
                <p className="text-zinc-700 dark:text-zinc-300">{r.summary}</p>
              </div>
              {!r.fromCv && <p className="text-amber-700">{t('No PDF CV, so this is from the application form only.')}</p>}
              <List title={t('Strengths')} items={r.strengths} />
              <List title={t('Gaps')} items={r.gaps} />
              <List title={t('Questions to ask')} items={r.questions} />
            </>
          )}
        </CardBody>
      )}
    </Card>
  );
}

function List({ title, items }: { title: string; items: string[] }) {
  if (!items.length) return null;
  return (
    <div>
      <p className="font-medium">{title}</p>
      <ul className="ms-5 list-disc space-y-0.5 text-zinc-600 dark:text-zinc-400">
        {items.map((x, i) => (
          <li key={i}>{x}</li>
        ))}
      </ul>
    </div>
  );
}
