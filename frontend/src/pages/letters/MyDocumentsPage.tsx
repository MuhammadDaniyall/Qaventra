import { useQuery } from '@tanstack/react-query';
import { MyLetterRequests, RequestLetterButton } from './Requests';
import { Link } from 'react-router-dom';
import { ChevronRight, FileText } from 'lucide-react';
import { lettersApi } from '../../api/letters';
import { qk } from '../../api/keys';
import { Card, EmptyState, ErrorState, PageHeader, Skeleton } from '../../components/ui';
import { formatDate } from '../../lib/format';
import { categoryLabel } from './shared';

export function MyDocumentsPage() {
  const letters = useQuery({ queryKey: qk.myLetters, queryFn: lettersApi.my });

  return (
    <>
      <PageHeader
        title="My documents"
        description="Letters and certificates HR has issued to you. Open one to print or save as PDF."
        actions={<RequestLetterButton />}
      />
      <MyLetterRequests />
      <Card>
        {letters.isLoading ? (
          <div className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="flex items-center gap-4 p-4">
                <Skeleton className="size-10 rounded-lg" />
                <div className="flex-1">
                  <Skeleton className="h-4 w-48" />
                  <Skeleton className="mt-2 h-3 w-32" />
                </div>
              </div>
            ))}
          </div>
        ) : letters.isError ? (
          <ErrorState error={letters.error} onRetry={() => letters.refetch()} />
        ) : !letters.data?.length ? (
          <EmptyState icon={FileText} title="No documents yet" description="When HR issues you a letter or certificate it appears here." />
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {letters.data.map((l) => (
              <li key={l.id}>
                <Link
                  to={`/letters/${l.id}`}
                  className="flex items-center gap-4 p-4 transition-colors hover:bg-zinc-50 focus-visible:bg-zinc-50 focus-visible:outline-none dark:hover:bg-zinc-800/50 dark:focus-visible:bg-zinc-800/50"
                >
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent-50 text-accent-600 dark:bg-accent-500/10 dark:text-accent-300">
                    <FileText className="size-5" aria-hidden="true" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium text-zinc-900 dark:text-white">{l.subject}</span>
                    <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                      {categoryLabel(l.category)} · {formatDate(l.issuedOn)} · <span className="font-mono">{l.referenceNo}</span>
                    </span>
                  </span>
                  <ChevronRight className="size-4 text-zinc-400" aria-hidden="true" />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </>
  );
}
