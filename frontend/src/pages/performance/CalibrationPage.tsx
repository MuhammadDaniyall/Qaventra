import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { Lock, Send } from 'lucide-react';
import { qk } from '../../api/keys';
import { performanceApi, POTENTIAL_LABELS, STAGE_LABELS } from '../../api/performance';
import { Badge, Button, Card, CardBody, CardHeader, ErrorState, PageHeader, PageSpinner, Table, TBody, Td, Th, THead, Tr, useToast } from '../../components/ui';
import { NineBox } from './shared';

/** HR: everyone in a cycle with the 9-box, the bell curve against target, suggested raises, and releasing reviews. */
export function CalibrationPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const qc = useQueryClient();
  const data = useQuery({ queryKey: qk.calibration(id), queryFn: () => performanceApi.calibration(id) });
  const cycles = useQuery({ queryKey: qk.performanceCycles, queryFn: performanceApi.cycles });
  const cycle = cycles.data?.find((c) => c.id === id);
  const release = useMutation({
    mutationFn: () => performanceApi.release(id),
    onSuccess: (r) => {
      toast.success(r.released ? `${r.released} review${r.released === 1 ? '' : 's'} released` : 'Nothing to release', r.released ? 'Employees can now read and sign off.' : 'No reviews are with HR right now.');
      qc.invalidateQueries({ queryKey: qk.performance });
    },
    onError: (err) => toast.error(err),
  });
  const close = useMutation({
    mutationFn: () => performanceApi.closeCycle(id),
    onSuccess: () => {
      toast.success('Cycle closed');
      qc.invalidateQueries({ queryKey: qk.performance });
    },
    onError: (err) => toast.error(err),
  });
  if (data.isLoading) return <PageSpinner />;
  if (data.isError || !data.data)
    return (
      <Card>
        <ErrorState error={data.error} onRetry={() => data.refetch()} />
      </Card>
    );
  const d = data.data;
  const withHr = d.reviews.filter((r) => r.stage === 'Calibration').length;
  const max = Math.max(1, ...d.bellTarget, ...d.bellActual);
  return (
    <>
      <PageHeader
        back={{ to: '/performance?tab=cycles', label: 'Review cycles' }}
        title={cycle?.name ?? 'Review cycle'}
        description={`${d.reviews.length} people · ${withHr} waiting for release`}
        actions={
          cycle?.status === 'Active' && (
            <>
              <Button variant="ghost" icon={<Lock className="size-4" />} loading={close.isPending} onClick={() => close.mutate()}>
                Close cycle
              </Button>
              <Button icon={<Send className="size-4" />} disabled={!withHr} loading={release.isPending} onClick={() => release.mutate()}>
                Release {withHr || ''} to employees
              </Button>
            </>
          )
        }
      />
      <div className="mb-6 grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader title="9-box" description="Click a name to open their review." />
          <CardBody>
            <NineBox reviews={d.reviews} onPick={(r) => navigate(`/performance/reviews/${r.id}`)} />
          </CardBody>
        </Card>
        <Card>
          <CardHeader title="Bell curve" description="Share of people at each final rating, against your target." />
          <CardBody>
            <div className="flex h-56 items-end gap-4">
              {d.labels.map((l, i) => (
                <div key={l} className="flex flex-1 flex-col items-center gap-1">
                  <div className="flex h-44 w-full items-end justify-center gap-1">
                    <div className="w-1/3 rounded-t bg-zinc-200 dark:bg-zinc-700" style={{ height: `${(d.bellTarget[i] / max) * 100}%` }} title={`Target ${d.bellTarget[i]}%`} />
                    <div className="w-1/3 rounded-t bg-accent-500" style={{ height: `${(d.bellActual[i] / max) * 100}%` }} title={`Actual ${d.bellActual[i]}%`} />
                  </div>
                  <span className="text-xs font-semibold text-zinc-900 dark:text-zinc-100">{i + 1}</span>
                  <span className="text-center text-[11px] text-zinc-500">
                    {d.bellActual[i]}% / {d.bellTarget[i]}%
                  </span>
                </div>
              ))}
            </div>
            <p className="mt-2 flex gap-4 text-xs text-zinc-500">
              <span className="flex items-center gap-1">
                <span className="size-2.5 rounded-sm bg-accent-500" /> Actual
              </span>
              <span className="flex items-center gap-1">
                <span className="size-2.5 rounded-sm bg-zinc-300 dark:bg-zinc-600" /> Target
              </span>
            </p>
          </CardBody>
        </Card>
      </div>
      <Card>
        <Table>
          <THead>
            <tr>
              <Th>Employee</Th>
              <Th>Stage</Th>
              <Th className="text-right">Self</Th>
              <Th className="text-right">Manager</Th>
              <Th className="text-right">Final</Th>
              <Th>Potential</Th>
              <Th className="text-right">Raise</Th>
            </tr>
          </THead>
          <TBody>
            {d.reviews.map((r) => (
              <Tr key={r.id} onClick={() => navigate(`/performance/reviews/${r.id}`)}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.employeeName}</p>
                  <p className="text-xs text-zinc-500">{[r.department, r.reviewer && `by ${r.reviewer}`].filter(Boolean).join(' · ')}</p>
                </Td>
                <Td>
                  <Badge tone={r.stage === 'Done' ? 'green' : r.stage === 'Calibration' ? 'violet' : 'amber'}>{STAGE_LABELS[r.stage]}</Badge>
                  {r.employeeAgrees === false && <Badge tone="red">Disagrees</Badge>}
                </Td>
                <Td className="text-right tabular-nums">{r.selfScore ?? '—'}</Td>
                <Td className="text-right tabular-nums">{r.managerScore ?? '—'}</Td>
                <Td className="text-right font-semibold tabular-nums">{r.finalRating ?? '—'}</Td>
                <Td className="text-sm">{r.potential ? POTENTIAL_LABELS[r.potential] : '—'}</Td>
                <Td className="text-right tabular-nums">{r.suggestedRaise !== null ? `${r.suggestedRaise}%` : '—'}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      </Card>
    </>
  );
}
