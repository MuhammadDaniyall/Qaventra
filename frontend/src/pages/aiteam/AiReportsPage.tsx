import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Download, Search, Sparkles, TableProperties } from 'lucide-react';
import { aiApi, type AiReportResult } from '../../api/ai';
import { errorMessage } from '../../api/client';
import { Badge, Button, Card, EmptyState, FormError, Input, PageHeader, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatDate } from '../../lib/format';
import { useT } from '../../lib/i18n';

const EXAMPLES = [
  'How many people came late in Lahore this month?',
  'Who was absent last month?',
  'Headcount by department',
  'Leave taken this year',
  'Payroll totals last month',
  'Documents expiring soon',
];

function downloadCsv(r: AiReportResult) {
  const esc = (v: string) => (/[",\r\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);
  const lines = [r.columns, ...r.rows].map((row) => row.map(esc).join(','));
  const url = URL.createObjectURL(new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = `${r.report}-${r.params.from}-${r.params.to}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

/**
 * Reports in plain words: HR types a question, the AI only picks one of a fixed list of reports and fills its filters,
 * and the server runs the query. Without AI a keyword match picks the report, and the chips run one directly.
 */
export function AiReportsPage() {
  const { t } = useT();
  const [q, setQ] = useState('');
  const catalogue = useQuery({ queryKey: ['ai', 'reports'], queryFn: aiApi.reports, staleTime: Infinity });
  const run = useMutation({ mutationFn: aiApi.report });
  const r = run.data;
  const ask = (question: string) => question.trim().length >= 3 && run.mutate({ question: question.trim() });

  return (
    <>
      <PageHeader title="Ask for a report" description="Type what you want to see. The AI picks a ready-made report and its filters; it never writes queries." />
      <Card className="mb-5 p-5">
        <form
          className="flex gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            ask(q);
          }}
        >
          <Input
            value={q}
            maxLength={500}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t('e.g. Lahore ke kitne log is mahine late aaye')}
            aria-label={t('Ask for a report')}
          />
          <Button type="submit" loading={run.isPending} disabled={q.trim().length < 3} icon={<Search className="size-4" />}>
            {t('Show')}
          </Button>
        </form>
        <div className="mt-3 flex flex-wrap gap-2">
          {EXAMPLES.map((e) => (
            <button
              key={e}
              type="button"
              className="rounded-full border border-zinc-200 px-3 py-1 text-xs text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
              onClick={() => {
                setQ(t(e));
                ask(e);
              }}
            >
              {t(e)}
            </button>
          ))}
        </div>
        {catalogue.data && (
          <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-zinc-500">
            <span>{t('Reports')}:</span>
            {catalogue.data.map((c) => (
              <button
                key={c.key}
                type="button"
                title={c.about}
                className="text-accent-600 hover:underline"
                onClick={() => run.mutate({ report: c.key })}
              >
                {t(c.title)}
              </button>
            ))}
          </div>
        )}
        {run.isError && (
          <div className="mt-3">
            <FormError message={errorMessage(run.error)} />
          </div>
        )}
      </Card>

      {r && (
        <Card>
          <div className="flex flex-wrap items-start justify-between gap-3 p-5">
            <div>
              <h2 className="flex items-center gap-2 font-semibold">
                {t(r.title)}
                {r.usedAi && (
                  <Badge tone="violet">
                    <Sparkles className="me-1 inline size-3" />
                    AI
                  </Badge>
                )}
              </h2>
              {r.explanation && <p className="mt-1 text-sm text-zinc-500">{r.explanation}</p>}
              <div className="mt-2 flex flex-wrap gap-2 text-xs">
                <Badge>
                  {formatDate(r.params.from)} – {formatDate(r.params.to)}
                </Badge>
                {r.params.department && <Badge tone="blue">{r.params.department}</Badge>}
                {r.params.location && <Badge tone="blue">{r.params.location}</Badge>}
                {r.params.status && <Badge tone="blue">{r.params.status}</Badge>}
                <Badge tone="gray">
                  {r.rows.length} {t('rows')}
                  {r.truncated ? '+' : ''}
                </Badge>
              </div>
            </div>
            <Button variant="secondary" icon={<Download className="size-4" />} disabled={r.rows.length === 0} onClick={() => downloadCsv(r)}>
              {t('Export CSV')}
            </Button>
          </div>
          {r.rows.length === 0 ? (
            <EmptyState icon={TableProperties} title="Nothing found" description="No records match this report and its filters." />
          ) : (
            <Table>
              <THead>
                <Tr>
                  {r.columns.map((c) => (
                    <Th key={c}>{c}</Th>
                  ))}
                </Tr>
              </THead>
              <TBody>
                {r.rows.map((row, i) => (
                  <Tr key={i}>
                    {row.map((v, j) => (
                      <Td key={j}>{v}</Td>
                    ))}
                  </Tr>
                ))}
              </TBody>
            </Table>
          )}
        </Card>
      )}
    </>
  );
}
