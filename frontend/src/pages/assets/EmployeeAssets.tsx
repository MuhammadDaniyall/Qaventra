import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Layers, Package, PackageCheck } from 'lucide-react';
import { assetsApi, type Assignment } from '../../api/assets';
import { qk } from '../../api/keys';
import { Badge, Button, Card, CardBody, CardHeader, EmptyState, Select, Spinner, useToast } from '../../components/ui';
import { AssignmentCards, requestLabel, TakeBackModal } from './shared';

/** An employee's assets on their profile: what they hold, open requests, and (HR) giving a starter kit. */
export function EmployeeAssetsTab({ employeeId, hr }: { employeeId: string; hr: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const data = useQuery({ queryKey: qk.employeeAssets(employeeId), queryFn: () => assetsApi.forEmployee(employeeId) });
  const kits = useQuery({ queryKey: qk.assetKits, queryFn: assetsApi.kits, enabled: hr });
  const handler = useQuery({ queryKey: qk.assetsMe, queryFn: assetsApi.me });
  const [kitId, setKitId] = useState('');
  const [returning, setReturning] = useState<Assignment | null>(null);
  const apply = useMutation({
    mutationFn: () => assetsApi.applyKit(kitId, employeeId),
    onSuccess: (r) => {
      toast.success(r.requests ? `${r.requests} hand-over${r.requests === 1 ? '' : 's'} queued` : 'Nothing new to queue', r.requests ? 'The handlers have been told.' : 'Those requests are already open.');
      qc.invalidateQueries({ queryKey: qk.assets });
    },
    onError: (err) => toast.error(err),
  });
  if (data.isLoading) return <Spinner />;
  const d = data.data;
  return (
    <div className="space-y-6">
      {hr && !!kits.data?.length && (
        <Card>
          <CardBody className="flex flex-wrap items-center gap-3">
            <Layers className="size-4 text-zinc-400" />
            <span className="text-sm text-zinc-700 dark:text-zinc-300">Give a starter kit</span>
            <Select className="w-52" value={kitId} onChange={(e) => setKitId(e.target.value)} aria-label="Kit">
              <option value="">Choose a kit…</option>
              {kits.data.map((k) => (
                <option key={k.id} value={k.id}>
                  {k.name} ({k.types.join(', ')})
                </option>
              ))}
            </Select>
            <Button size="sm" disabled={!kitId} loading={apply.isPending} onClick={() => apply.mutate()}>
              Queue hand-over
            </Button>
          </CardBody>
        </Card>
      )}
      {d?.holding.length ? (
        <AssignmentCards
          items={d.holding}
          action={(a) =>
            handler.data?.isHandler && (
              <Button variant="ghost" size="sm" onClick={() => setReturning(a)}>
                Take back
              </Button>
            )
          }
        />
      ) : (
        <Card>
          <EmptyState icon={Package} title="No assets" description="Nothing is in this person's name." />
        </Card>
      )}
      {!!d?.open.length && (
        <Card>
          <CardHeader title="Open requests" />
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {d.open.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-3 px-5 py-3 text-sm">
                <span className="text-zinc-900 dark:text-zinc-100">
                  {r.kind === 'Return' ? `Return ${r.assetLabel}` : r.type} <span className="text-zinc-500">· {r.reason}</span>
                </span>
                <Badge tone="amber">{requestLabel(r)}</Badge>
              </li>
            ))}
          </ul>
        </Card>
      )}
      {!!d?.returned.length && (
        <div>
          <h2 className="mb-3 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Returned</h2>
          <AssignmentCards items={d.returned} />
        </div>
      )}
      {returning && <TakeBackModal assignmentId={returning.id} label={`${returning.assetName} from ${returning.employeeName}`} onClose={() => setReturning(null)} />}
    </div>
  );
}

/** On a separation: what must come back before the person leaves. Hidden when nothing is outstanding. */
export function SeparationAssetsCard({ employeeId }: { employeeId: string }) {
  const data = useQuery({ queryKey: qk.employeeAssets(employeeId), queryFn: () => assetsApi.forEmployee(employeeId) });
  const handler = useQuery({ queryKey: qk.assetsMe, queryFn: assetsApi.me });
  const [returning, setReturning] = useState<Assignment | null>(null);
  const due = (data.data?.holding ?? []).filter((a) => a.returnOnExit);
  if (!data.data) return null;
  return (
    <Card>
      <CardHeader title="Company assets" description={due.length ? 'To be returned before the last day.' : undefined} />
      {due.length ? (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {due.map((a) => (
            <li key={a.id} className="flex items-center justify-between gap-3 px-5 py-3">
              <div className="text-sm">
                <p className="font-medium text-zinc-900 dark:text-zinc-100">{a.assetName}</p>
                <p className="text-xs text-zinc-500">
                  <span className="font-mono">{a.tag}</span> · {a.type}
                </p>
              </div>
              {handler.data?.isHandler && (
                <Button size="sm" variant="secondary" onClick={() => setReturning(a)}>
                  Take back
                </Button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <CardBody className="flex items-center gap-2 text-sm text-emerald-700 dark:text-emerald-300">
          <PackageCheck className="size-4" /> Nothing outstanding.
        </CardBody>
      )}
      {returning && <TakeBackModal assignmentId={returning.id} label={`${returning.assetName}`} onClose={() => setReturning(null)} />}
    </Card>
  );
}
