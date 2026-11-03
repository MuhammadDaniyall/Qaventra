import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw, Sparkles } from 'lucide-react';
import { aiApi } from '../../api/ai';
import { Badge, Button, Card, CardBody, CardHeader, ErrorState, Skeleton } from '../../components/ui';
import { formatDateTimeUtc } from '../../lib/format';
import { useT } from '../../lib/i18n';

const LEVEL = { good: 'green', watch: 'amber', act: 'red' } as const;

/** The AI's written read of the company's numbers; shown only when the site has an AI key. */
export function AiInsightsCard() {
  const { t } = useT();
  const qc = useQueryClient();
  const status = useQuery({ queryKey: ['ai-status'], queryFn: aiApi.status, staleTime: 5 * 60_000, retry: false });
  const [refreshing, setRefreshing] = useState(false);
  const insights = useQuery({
    queryKey: ['ai-insights'],
    queryFn: () => aiApi.insights(),
    enabled: !!status.data?.enabled,
    staleTime: 60 * 60_000,
    retry: false,
  });
  if (!status.data?.enabled) return null;

  const refresh = async () => {
    setRefreshing(true);
    try {
      qc.setQueryData(['ai-insights'], await aiApi.insights(true));
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <Card>
      <CardHeader
        title={
          <span className="flex items-center gap-2">
            <Sparkles className="size-4 text-accent-500" /> {t('AI summary')}
          </span>
        }
        description={
          insights.data ? `${t('Made')} ${formatDateTimeUtc(insights.data.madeAtUtc)}` : t('What the numbers say this week and what to do about it.')
        }
        actions={
          <Button size="sm" variant="secondary" icon={<RefreshCw className="size-4" />} loading={refreshing} onClick={refresh}>
            {t('Refresh')}
          </Button>
        }
      />
      <CardBody className="space-y-3">
        {insights.isLoading && <Skeleton className="h-24" />}
        {insights.isError && <ErrorState error={insights.error} />}
        {insights.data && (
          <>
            <p className="font-medium text-zinc-800 dark:text-zinc-100">{insights.data.headline}</p>
            <ul className="space-y-3">
              {insights.data.findings.map((f, i) => (
                <li key={i} className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge tone={LEVEL[f.level] ?? 'gray'}>{t(f.level === 'good' ? 'Good' : f.level === 'watch' ? 'Watch' : 'Act')}</Badge>
                    <span className="font-medium">{f.title}</span>
                  </div>
                  <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">{f.detail}</p>
                  <p className="mt-1 text-sm">
                    <span className="font-medium">{t('Next step')}:</span> {f.action}
                  </p>
                </li>
              ))}
            </ul>
          </>
        )}
      </CardBody>
    </Card>
  );
}
