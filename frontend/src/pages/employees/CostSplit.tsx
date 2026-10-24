import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2 } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useLookups } from '../../api/employeeFile';
import { fundsApi } from '../../api/funds';
import { Button, Card, CardHeader, FormError, IconButton, Input, Select, useToast } from '../../components/ui';

/** HR: split a person's salary cost across cost centers (shares add up to 100%). */
export function CostSplitCard({ employeeId }: { employeeId: string }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = ['funds', 'splits', employeeId];
  const splits = useQuery({ queryKey: key, queryFn: () => fundsApi.splits(employeeId) });
  const lookups = useLookups();
  const centers = (lookups.data ?? []).filter((l) => l.kind === 'CostCenter');
  const [draft, setDraft] = useState<{ costCenterId: string; percent: string }[] | null>(null);
  const save = useMutation({
    mutationFn: () =>
      fundsApi.saveSplits(
        employeeId,
        draft!.filter((d) => d.costCenterId).map((d) => ({ costCenterId: d.costCenterId, percent: Number(d.percent) })),
      ),
    onSuccess: () => {
      toast.success('Saved');
      setDraft(null);
      qc.invalidateQueries({ queryKey: key });
    },
  });
  if (!centers.length) return null;
  const v = draft ?? (splits.data ?? []).map((s) => ({ costCenterId: s.costCenterId, percent: String(s.percent) }));
  const total = v.reduce((t, x) => t + (Number(x.percent) || 0), 0);
  const name = (id: string) => centers.find((c) => c.id === id)?.name ?? '';
  return (
    <Card className="mt-6">
      <CardHeader
        title="Cost center split"
        description="Book this person's salary cost to several cost centers. Empty: all of it goes to their own cost center."
        actions={
          draft ? (
            <div className="flex gap-2">
              <Button size="sm" variant="secondary" onClick={() => setDraft(null)}>
                Cancel
              </Button>
              <Button size="sm" loading={save.isPending} disabled={v.length > 0 && Math.abs(total - 100) > 0.01} onClick={() => save.mutate()}>
                Save
              </Button>
            </div>
          ) : (
            <Button size="sm" variant="secondary" onClick={() => setDraft(v.length ? v : [{ costCenterId: '', percent: '100' }])}>
              Edit
            </Button>
          )
        }
      />
      <div className="space-y-2 px-5 pb-5">
        <FormError message={save.isError ? errorMessage(save.error) : null} />
        {!draft &&
          (v.length ? (
            v.map((x) => (
              <p key={x.costCenterId} className="text-sm">
                {name(x.costCenterId)} <span className="text-zinc-500">{x.percent}%</span>
              </p>
            ))
          ) : (
            <p className="text-sm text-zinc-500">Not split.</p>
          ))}
        {draft && (
          <>
            {draft.map((x, i) => (
              <div key={i} className="flex items-center gap-2">
                <Select
                  aria-label="Cost center"
                  value={x.costCenterId}
                  onChange={(e) => setDraft(draft.map((y, j) => (j === i ? { ...y, costCenterId: e.target.value } : y)))}
                >
                  <option value="">Choose…</option>
                  {centers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </Select>
                <div className="w-28 shrink-0">
                  <Input
                    type="number"
                    min={1}
                    max={100}
                    aria-label="Percent"
                    value={x.percent}
                    onChange={(e) => setDraft(draft.map((y, j) => (j === i ? { ...y, percent: e.target.value } : y)))}
                  />
                </div>
                <IconButton label="Remove" onClick={() => setDraft(draft.filter((_, j) => j !== i))}>
                  <Trash2 className="size-4" />
                </IconButton>
              </div>
            ))}
            <div className="flex items-center justify-between">
              <Button size="sm" variant="ghost" icon={<Plus className="size-4" />} onClick={() => setDraft([...draft, { costCenterId: '', percent: '' }])}>
                Add cost center
              </Button>
              <span className={total === 100 || !draft.length ? 'text-sm text-zinc-500 dark:text-zinc-400' : 'text-sm text-red-600'}>{total}%</span>
            </div>
          </>
        )}
      </div>
    </Card>
  );
}
