import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { Eye, EyeOff, Printer, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { lettersApi } from '../../api/letters';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Button, Card, ConfirmDialog, ErrorState, PageHeader, PageSpinner, useToast } from '../../components/ui';
import { formatDate } from '../../lib/format';
import { useDocumentTitle } from '../../lib/hooks';
import type { LetterDocument } from '../../types/workflows';
import { categoryLabel } from './shared';

export function LetterPage() {
  const { id = '' } = useParams();
  const user = useUser();
  const hr = isHr(user);
  const back = hr ? { to: '/letters', label: 'HR letters' } : { to: '/me/documents', label: 'My documents' };
  const letter = useQuery({ queryKey: qk.letter(id), queryFn: () => lettersApi.get(id) });

  if (letter.isLoading) return <PageSpinner />;
  if (letter.isError || !letter.data) {
    return (
      <>
        <PageHeader title="Letter" back={back} />
        <Card>
          <ErrorState error={letter.error} onRetry={() => letter.refetch()} />
        </Card>
      </>
    );
  }
  return <LetterView l={letter.data} back={back} hr={hr} />;
}

function LetterView({ l, back, hr }: { l: LetterDocument; back: { to: string; label: string }; hr: boolean }) {
  useDocumentTitle(`${l.referenceNo} ${l.employeeName}`);
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [deleting, setDeleting] = useState(false);

  const share = useMutation({
    mutationFn: () => lettersApi.setSharing(l.id, !l.sharedWithEmployee),
    onSuccess: () => {
      toast.success(l.sharedWithEmployee ? 'Hidden from the employee' : 'Shared with the employee');
      qc.invalidateQueries({ queryKey: qk.letters });
    },
    onError: (err) => toast.error(err),
  });
  const remove = useMutation({
    mutationFn: () => lettersApi.remove(l.id),
    onSuccess: () => {
      toast.success('Letter deleted', l.referenceNo);
      qc.invalidateQueries({ queryKey: qk.letters });
      navigate('/letters');
    },
    onError: (err) => toast.error(err),
  });

  return (
    <>
      <div className="no-print">
        <PageHeader
          back={back}
          title={l.subject}
          description={`${categoryLabel(l.category)} · ${l.employeeName} · ${l.referenceNo}`}
          actions={
            <>
              {hr && (
                <>
                  <Button
                    variant="secondary"
                    icon={l.sharedWithEmployee ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                    loading={share.isPending}
                    onClick={() => share.mutate()}
                  >
                    {l.sharedWithEmployee ? 'Hide from employee' : 'Share with employee'}
                  </Button>
                  <Button variant="secondary" icon={<Trash2 className="size-4" />} onClick={() => setDeleting(true)}>
                    Delete
                  </Button>
                </>
              )}
              <Button icon={<Printer className="size-4" />} onClick={() => window.print()}>
                Print / PDF
              </Button>
            </>
          }
        />
      </div>

      <article
        className="print-area mx-auto flex min-h-[29.7cm] max-w-[21cm] flex-col rounded-xl border border-zinc-200 bg-white shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
        style={{ padding: `${l.marginTopMm ?? 25}mm ${l.marginSideMm ?? 20}mm 15mm` }}
      >
        <header className="flex items-start justify-between gap-6 border-b-2 border-accent-600 pb-5">
          <div>
            <p className="text-2xl font-semibold tracking-tight text-zinc-900 dark:text-white">{l.companyName}</p>
            {l.companyAddress && <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">{l.companyAddress}</p>}
            {l.companyPhone && <p className="text-sm text-zinc-500 dark:text-zinc-400">{l.companyPhone}</p>}
          </div>
          <div className="text-right text-sm text-zinc-600 dark:text-zinc-300">
            <p>
              <span className="text-zinc-400">Ref:</span> <span className="font-mono">{l.referenceNo}</span>
            </p>
            <p>
              <span className="text-zinc-400">Date:</span> {formatDate(l.issuedOn)}
            </p>
          </div>
        </header>

        <div className="mt-8 text-sm text-zinc-700 dark:text-zinc-300">
          <p className="font-medium text-zinc-900 dark:text-white">{l.employeeName}</p>
          {l.designation && <p>{l.designation}</p>}
          <p className="font-mono text-xs text-zinc-500">{l.employeeCode}</p>
        </div>

        <h2 className="mt-8 font-semibold text-zinc-900 underline decoration-zinc-300 underline-offset-4 dark:text-white">Subject: {l.subject}</h2>

        <div className="mt-6 flex-1 font-serif text-[15px] leading-7 whitespace-pre-wrap text-zinc-800 dark:text-zinc-200">{l.body}</div>

        <footer className="mt-16">
          <div className="w-56 border-t border-zinc-400 pt-2 text-sm">
            <p className="font-medium text-zinc-900 dark:text-white">{l.issuedByName ?? 'Human Resources'}</p>
            <p className="text-zinc-500">For {l.companyName}</p>
          </div>
          {l.footer && <p className="mt-10 border-t border-zinc-200 pt-2 text-center text-xs text-zinc-500 dark:border-zinc-700">{l.footer}</p>}
        </footer>
      </article>

      <ConfirmDialog
        open={deleting}
        title="Delete this letter?"
        message={<>{l.referenceNo} will be removed for everyone. Its reference number will not be reused.</>}
        confirmLabel="Delete letter"
        tone="danger"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={() => setDeleting(false)}
      />
    </>
  );
}
