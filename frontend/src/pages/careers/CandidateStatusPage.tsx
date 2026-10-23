import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { Briefcase } from 'lucide-react';
import { http } from '../../api/client';
import type { CandidateLetter } from '../../api/letters';
import { Button, Card, CardBody, CardHeader, EmptyState, ErrorState, PageSpinner } from '../../components/ui';
import { formatDate, formatDateTimeUtc, formatNumber } from '../../lib/format';

interface PortalApplication {
  id: string;
  job: string;
  stage: string;
  appliedAtUtc: string;
  interviews: { title: string; startsAtUtc: string; durationMinutes: number; mode: string; where: string | null; status: string }[];
  offeredSalary: number | null;
  offeredJoiningDate: string | null;
  offerStatus: 'Sent' | 'Accepted' | 'Declined' | null;
  hasOfferLetter: boolean;
  canWithdraw: boolean;
}

interface Portal {
  company: string;
  name: string;
  email: string;
  applications: PortalApplication[];
}

/** Public page a candidate reaches from their emails: status, interviews and offer. */
export function CandidateStatusPage() {
  const { token = '' } = useParams();
  const qc = useQueryClient();
  const key = ['candidate-portal', token];
  const page = useQuery({ queryKey: key, queryFn: () => http.get<Portal>(`/careers/portal/${token}`), retry: false });
  const [letter, setLetter] = useState<CandidateLetter | null>(null);
  const act = useMutation({
    mutationFn: ({ kind, id, accepted }: { kind: 'offer' | 'withdraw'; id: string; accepted?: boolean }) =>
      kind === 'offer' ? http.post(`/careers/portal/${token}/offer/${id}`, { accepted, note: null }) : http.post(`/careers/portal/${token}/withdraw/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: key }),
  });
  const openLetter = async (id: string) => setLetter(await http.get<CandidateLetter>(`/careers/portal/${token}/offer-letter/${id}`));

  if (page.isLoading) return <PageSpinner />;
  if (page.isError || !page.data)
    return (
      <div className="mx-auto max-w-2xl p-6">
        <ErrorState error={page.error} />
      </div>
    );
  const p = page.data;
  if (letter)
    return (
      <div className="mx-auto max-w-3xl p-6">
        <div className="no-print mb-4 flex gap-2">
          <Button variant="secondary" onClick={() => setLetter(null)}>
            Back
          </Button>
          <Button onClick={() => window.print()}>Print / PDF</Button>
        </div>
        <article className="print-area bg-white text-zinc-800" style={{ padding: `${letter.marginTopMm}mm ${letter.marginSideMm}mm 15mm` }}>
          <p className="text-xl font-semibold">{letter.companyName}</p>
          <p className="text-sm">
            Ref {letter.referenceNo} · {formatDate(letter.issuedOn)}
          </p>
          <h2 className="mt-6 font-semibold underline">Subject: {letter.subject}</h2>
          <div className="mt-4 font-serif leading-7 whitespace-pre-wrap">{letter.body}</div>
          {letter.footer && <p className="mt-8 border-t pt-2 text-center text-xs">{letter.footer}</p>}
        </article>
      </div>
    );
  return (
    <div className="mx-auto max-w-3xl space-y-6 p-6">
      <div>
        <p className="text-sm text-zinc-500">{p.company}</p>
        <h1 className="text-2xl font-semibold">Hello {p.name}</h1>
        <p className="text-sm text-zinc-500">Your applications and where they stand.</p>
      </div>
      {!p.applications.length && <EmptyState icon={Briefcase} title="No applications" />}
      {p.applications.map((a) => (
        <Card key={a.id}>
          <CardHeader title={a.job} description={`Applied ${formatDateTimeUtc(a.appliedAtUtc)} · ${a.stage}`} />
          <CardBody className="space-y-3 text-sm">
            {a.interviews.map((i, k) => (
              <p key={k}>
                <span className="font-medium">{i.title}</span>: {formatDateTimeUtc(i.startsAtUtc)} ({i.durationMinutes} min, {i.mode.toLowerCase()})
                {i.where && ` · ${i.where}`} {i.status !== 'Scheduled' && `· ${i.status.toLowerCase()}`}
              </p>
            ))}
            {a.offerStatus && (
              <div className="rounded-lg bg-zinc-50 p-3 dark:bg-zinc-800/50">
                <p className="font-medium">
                  Offer: PKR {formatNumber(a.offeredSalary ?? 0)} a month{a.offeredJoiningDate && `, joining ${formatDate(a.offeredJoiningDate)}`}
                </p>
                <p className="text-zinc-500">{a.offerStatus === 'Sent' ? 'Waiting for your answer' : `You ${a.offerStatus.toLowerCase()} this offer`}</p>
                <div className="mt-2 flex flex-wrap gap-2">
                  {a.hasOfferLetter && (
                    <Button size="sm" variant="secondary" onClick={() => openLetter(a.id)}>
                      Offer letter
                    </Button>
                  )}
                  {a.offerStatus === 'Sent' && (
                    <>
                      <Button size="sm" loading={act.isPending} onClick={() => act.mutate({ kind: 'offer', id: a.id, accepted: true })}>
                        Accept offer
                      </Button>
                      <Button size="sm" variant="secondary" loading={act.isPending} onClick={() => act.mutate({ kind: 'offer', id: a.id, accepted: false })}>
                        Decline
                      </Button>
                    </>
                  )}
                </div>
              </div>
            )}
            {a.canWithdraw && a.offerStatus !== 'Sent' && (
              <Button size="sm" variant="ghost" onClick={() => window.confirm('Withdraw this application?') && act.mutate({ kind: 'withdraw', id: a.id })}>
                Withdraw application
              </Button>
            )}
          </CardBody>
        </Card>
      ))}
    </div>
  );
}
