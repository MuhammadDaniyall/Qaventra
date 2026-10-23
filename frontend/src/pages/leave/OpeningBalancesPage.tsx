import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Save, Search } from 'lucide-react';
import { qk } from '../../api/keys';
import { leaveRulesApi } from '../../api/leaveRules';
import { Button, Card, EmptyState, ErrorState, Input, PageHeader, SkeletonRows, Table, TBody, Td, Th, THead, Tr, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';

/** HR: everyone's leave balances for a year; set days brought forward and adjustments in one go (e.g. when starting on the system). */
export function OpeningBalancesPage() {
  const toast = useToast();
  const qc = useQueryClient();
  const [year, setYear] = useState(new Date().getFullYear());
  const [search, setSearch] = useState('');
  const [edits, setEdits] = useState<Record<string, { carriedForward: string; adjustment: string }>>({});
  const [note, setNote] = useState('Opening balance');
  const rows = useQuery({ queryKey: qk.openingBalances(year), queryFn: () => leaveRulesApi.balances(year) });
  const key = (e: string, t: string) => `${e}|${t}`;
  const shown = useMemo(() => {
    const q = search.trim().toLowerCase();
    return (rows.data ?? []).filter((r) => !q || `${r.name} ${r.code} ${r.leaveType}`.toLowerCase().includes(q));
  }, [rows.data, search]);
  const changed = Object.keys(edits).length;
  const save = useMutation({
    mutationFn: () =>
      leaveRulesApi.saveBalances(
        year,
        Object.entries(edits).map(([k, v]) => {
          const [employeeId, leaveTypeId] = k.split('|');
          return { employeeId, leaveTypeId, carriedForward: Number(v.carriedForward) || 0, adjustment: Number(v.adjustment) || 0 };
        }),
        note.trim() || null,
      ),
    onSuccess: (r) => {
      toast.success(`${r.changed} balances updated`);
      setEdits({});
      qc.invalidateQueries({ queryKey: qk.openingBalances(year) });
      qc.invalidateQueries({ queryKey: qk.leave });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <>
      <PageHeader
        title="Opening leave balances"
        description="Days brought forward from before, and corrections, for everyone at once."
        back={{ to: '/leave/approvals', label: 'Leave' }}
        actions={
          <Button icon={<Save className="size-4" />} disabled={!changed} loading={save.isPending} onClick={() => save.mutate()}>
            Save {changed || ''}
          </Button>
        }
      />
      <Card>
        <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
          <Input
            className="w-28"
            type="number"
            min={2000}
            max={2100}
            value={year}
            onChange={(e) => (setYear(Number(e.target.value)), setEdits({}))}
            aria-label="Year"
          />
          <div className="relative w-72">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
            <Input className="pl-9" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Name, code or leave type" />
          </div>
          <Input
            className="w-64"
            value={note}
            maxLength={200}
            onChange={(e) => setNote(e.target.value)}
            aria-label="Note"
            placeholder="Note saved with the change"
          />
        </div>
        {rows.isLoading ? (
          <SkeletonRows rows={8} cols={6} />
        ) : rows.isError ? (
          <ErrorState error={rows.error} onRetry={() => rows.refetch()} />
        ) : !shown.length ? (
          <EmptyState icon={Search} title="Nothing to show" />
        ) : (
          <div className="max-h-[70vh] overflow-auto">
            <Table>
              <THead>
                <Tr>
                  <Th>Employee</Th>
                  <Th>Leave</Th>
                  <Th className="text-right">Entitled</Th>
                  <Th className="w-32">Brought forward</Th>
                  <Th className="w-32">Adjustment</Th>
                  <Th className="text-right">Used</Th>
                  <Th className="text-right">Remaining</Th>
                </Tr>
              </THead>
              <TBody>
                {shown.map((r) => {
                  const k = key(r.employeeId, r.leaveTypeId);
                  const e = edits[k];
                  const cf = e ? Number(e.carriedForward) || 0 : r.carriedForward;
                  const adj = e ? Number(e.adjustment) || 0 : r.adjustment;
                  const remaining = r.remaining - r.carriedForward - r.adjustment + cf + adj;
                  const set = (patch: Partial<{ carriedForward: string; adjustment: string }>) =>
                    setEdits((x) => ({ ...x, [k]: { ...(x[k] ?? { carriedForward: String(r.carriedForward), adjustment: String(r.adjustment) }), ...patch } }));
                  return (
                    <Tr key={k} className={cn(e && 'bg-amber-50/60 dark:bg-amber-500/5')}>
                      <Td>
                        {r.name} <span className="text-xs text-zinc-500">{r.code}</span>
                      </Td>
                      <Td>{r.leaveType}</Td>
                      <Td className="text-right tabular-nums">{r.entitled}</Td>
                      <Td>
                        <Input
                          aria-label="Brought forward"
                          type="number"
                          min={0}
                          step={0.5}
                          value={e?.carriedForward ?? r.carriedForward}
                          onChange={(ev) => set({ carriedForward: ev.target.value })}
                        />
                      </Td>
                      <Td>
                        <Input
                          aria-label="Adjustment"
                          type="number"
                          step={0.5}
                          value={e?.adjustment ?? r.adjustment}
                          onChange={(ev) => set({ adjustment: ev.target.value })}
                        />
                      </Td>
                      <Td className="text-right tabular-nums">{r.used}</Td>
                      <Td className="text-right font-medium tabular-nums">{remaining}</Td>
                    </Tr>
                  );
                })}
              </TBody>
            </Table>
          </div>
        )}
      </Card>
    </>
  );
}
