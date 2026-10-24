import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ChevronRight, Network, Search } from 'lucide-react';
import { employeesApi, type OrgChartNode } from '../../api/employees';
import { useUser } from '../../auth/AuthContext';
import { isApprover } from '../../auth/roles';
import { Avatar, Button, Card, EmptyState, ErrorState, Input, PageHeader, SkeletonRows } from '../../components/ui';
import { cn } from '../../lib/cn';
import { tx, txf } from '../../lib/i18n';

interface Node extends OrgChartNode {
  reports: Node[];
  /** Everyone below, at any depth. */
  teamSize: number;
}

/** Builds the reporting tree. People whose manager has left (or is missing) become roots. */
function buildTree(list: OrgChartNode[]): Node[] {
  const byId = new Map<string, Node>(list.map((n) => [n.id, { ...n, reports: [], teamSize: 0 }]));
  const roots: Node[] = [];
  for (const n of byId.values()) {
    const manager = n.managerId ? byId.get(n.managerId) : undefined;
    if (manager && manager !== n) manager.reports.push(n);
    else roots.push(n);
  }
  const size = (n: Node, seen: Set<string>): number => {
    if (seen.has(n.id)) return 0; // guards against a reporting loop in bad data
    seen.add(n.id);
    n.reports.sort((a, b) => a.name.localeCompare(b.name));
    n.teamSize = n.reports.reduce((t, r) => t + 1 + size(r, seen), 0);
    return n.teamSize;
  };
  const seen = new Set<string>();
  roots.forEach((r) => size(r, seen));
  return roots.sort((a, b) => b.teamSize - a.teamSize || a.name.localeCompare(b.name));
}

/** Ids on the path from the roots down to every match, so matches are visible. */
function pathsTo(roots: Node[], match: (n: Node) => boolean): Set<string> {
  const open = new Set<string>();
  const walk = (n: Node, trail: string[]): boolean => {
    let hit = match(n);
    for (const r of n.reports) if (walk(r, [...trail, n.id])) hit = true;
    if (hit) trail.forEach((id) => open.add(id));
    return hit;
  };
  roots.forEach((r) => walk(r, []));
  return open;
}

export function OrgChartPage() {
  const user = useUser();
  const navigate = useNavigate();
  const canOpen = isApprover(user);
  const chart = useQuery({ queryKey: ['employees', 'org-chart'], queryFn: employeesApi.orgChart });
  const [search, setSearch] = useState('');
  const [opened, setOpened] = useState<Set<string>>(new Set());
  const [allOpen, setAllOpen] = useState(false);

  const roots = useMemo(() => buildTree(chart.data ?? []), [chart.data]);
  const term = search.trim().toLowerCase();
  const matches = (n: Node) =>
    !!term && (n.name.toLowerCase().includes(term) || (n.designation ?? '').toLowerCase().includes(term) || (n.department ?? '').toLowerCase().includes(term));
  const searchOpen = useMemo(() => (term ? pathsTo(roots, matches) : new Set<string>()), [roots, term]); // eslint-disable-line react-hooks/exhaustive-deps
  // The top of the chart starts open so the first view shows the leadership and their teams.
  const isOpen = (n: Node, depth: number) => allOpen || opened.has(n.id) || searchOpen.has(n.id) || (!term && depth === 0 && !opened.has(`closed:${n.id}`));
  const toggle = (n: Node, depth: number) =>
    setOpened((o) => {
      const next = new Set(o);
      if (depth === 0) {
        const key = `closed:${n.id}`;
        if (next.has(key)) next.delete(key);
        else next.add(key);
      } else if (next.has(n.id)) next.delete(n.id);
      else next.add(n.id);
      return next;
    });

  const row = (n: Node, depth: number) => {
    const open = isOpen(n, depth);
    const hit = matches(n);
    return (
      <li key={n.id}>
        <div
          className={cn(
            'group flex items-center gap-2 rounded-xl border bg-white py-2.5 pe-3 ps-2 transition-colors dark:bg-zinc-900',
            hit ? 'border-accent-300 ring-2 ring-accent-500/20 dark:border-accent-500/50' : 'border-zinc-200/80 dark:border-zinc-800',
          )}
        >
          {n.reports.length > 0 ? (
            <button
              type="button"
              aria-label={open ? `Hide ${n.name}'s team` : `Show ${n.name}'s team`}
              aria-expanded={open}
              onClick={() => toggle(n, depth)}
              className="flex size-7 shrink-0 items-center justify-center rounded-md text-zinc-500 dark:text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800 dark:hover:text-zinc-200"
            >
              <ChevronRight className={cn('size-4 transition-transform', open && 'rotate-90')} />
            </button>
          ) : (
            <span className="size-7 shrink-0" />
          )}
          <Avatar name={n.name} size="sm" />
          <button
            type="button"
            disabled={!canOpen}
            onClick={() => navigate(`/employees/${n.id}`)}
            className="min-w-0 flex-1 text-start disabled:cursor-default"
          >
            <span className="block truncate text-sm font-medium text-zinc-900 group-hover:text-accent-700 dark:text-zinc-100 dark:group-hover:text-accent-300">
              {n.name}
            </span>
            <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">
              {[n.designation, n.department].filter(Boolean).join(' · ') || tx('No designation')}
            </span>
          </button>
          {n.teamSize > 0 && (
            <span className="shrink-0 rounded-full bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600 tabular-nums dark:bg-zinc-800 dark:text-zinc-300">
              {txf('{count} direct', { count: n.reports.length })}
              {n.teamSize > n.reports.length ? ` · ${txf('{count} total', { count: n.teamSize })}` : ''}
            </span>
          )}
        </div>
        {open && n.reports.length > 0 && (
          <ul className="mt-2 ms-5 space-y-2 border-s border-zinc-200 ps-3 sm:ms-6 sm:ps-5 dark:border-zinc-800">
            {n.reports.map((r) => row(r, depth + 1))}
          </ul>
        )}
      </li>
    );
  };

  const people = chart.data?.length ?? 0;
  return (
    <>
      <PageHeader
        title="Org chart"
        description={people ? txf('{count} people and who they report to.', { count: people }) : 'Who reports to whom.'}
        actions={
          <Button variant="secondary" onClick={() => (setAllOpen((a) => !a), setOpened(new Set()))}>
            {tx(allOpen ? 'Collapse all' : 'Expand all')}
          </Button>
        }
      />
      <div className="relative mb-4 sm:max-w-sm">
        <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" aria-hidden="true" />
        <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={tx('Find a person, role or department')} aria-label="Search the org chart" className="ps-9" />
      </div>
      {chart.isLoading ? (
        <Card>
          <SkeletonRows rows={6} cols={2} />
        </Card>
      ) : chart.isError ? (
        <Card>
          <ErrorState error={chart.error} onRetry={() => chart.refetch()} />
        </Card>
      ) : !roots.length ? (
        <Card>
          <EmptyState icon={Network} title="No one on the chart yet" description="Add employees and set their manager to build the chart." />
        </Card>
      ) : (
        <ul className="space-y-2">{roots.map((r) => row(r, 0))}</ul>
      )}
    </>
  );
}
