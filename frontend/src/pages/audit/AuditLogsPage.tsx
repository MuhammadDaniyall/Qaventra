import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { RotateCcw, ScrollText } from 'lucide-react';
import { auditApi, platformApi } from '../../api/platform';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isPlatformOwner } from '../../auth/roles';
import {
  Badge,
  Button,
  Card,
  Drawer,
  EmptyState,
  ErrorState,
  Field,
  Input,
  PageHeader,
  Pagination,
  Select,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { formatDateTimeUtc, roleLabel } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import type { AuditCategory, AuditLog } from '../../types';
import { ChangesDiff } from './ChangesDiff';

interface Filters {
  category: AuditCategory | '';
  tenantId: string;
  user: string;
  action: string;
  entityType: string;
  from: string;
  to: string;
}

const EMPTY: Filters = { category: '', tenantId: '', user: '', action: '', entityType: '', from: '', to: '' };
const PAGE_SIZE = 50;

/** Local calendar date -> UTC instant at the start (or end) of that day. */
function dayToUtc(date: string, endOfDay: boolean): string | undefined {
  if (!date) return undefined;
  const [y, m, d] = date.split('-').map(Number);
  const local = endOfDay ? new Date(y, m - 1, d, 23, 59, 59, 999) : new Date(y, m - 1, d, 0, 0, 0, 0);
  return local.toISOString();
}

function statusTone(code: number | null) {
  if (code == null) return 'gray' as const;
  if (code >= 500) return 'red' as const;
  if (code >= 400) return 'amber' as const;
  return 'green' as const;
}

export function AuditLogsPage() {
  const user = useUser();
  const isPlatform = isPlatformOwner(user);
  const [filters, setFilters] = useState<Filters>(EMPTY);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<AuditLog | null>(null);
  const debounced = useDebounced(filters, 350);

  const companies = useQuery({ queryKey: qk.companies, queryFn: platformApi.companies, enabled: isPlatform });

  const query = {
    category: debounced.category,
    tenantId: isPlatform ? debounced.tenantId || undefined : undefined,
    user: debounced.user.trim(),
    action: debounced.action.trim(),
    entityType: debounced.entityType.trim(),
    fromUtc: dayToUtc(debounced.from, false),
    toUtc: dayToUtc(debounced.to, true),
    page,
    pageSize: PAGE_SIZE,
  };
  const logs = useQuery({
    queryKey: [...qk.audit, query],
    queryFn: () => auditApi.list(query),
    placeholderData: keepPreviousData,
  });

  const set = <K extends keyof Filters>(key: K, value: Filters[K]) => {
    setFilters((f) => ({ ...f, [key]: value }));
    setPage(1);
  };
  const dirty = JSON.stringify(filters) !== JSON.stringify(EMPTY);

  return (
    <>
      <PageHeader
        title="Audit logs"
        description={
          isPlatform
            ? 'Sign-ins, API requests and data changes across every company.'
            : 'Sign-ins, API requests and data changes in your company.'
        }
      />

      <Card className="mb-4 p-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
          {isPlatform && (
            <Field label="Company">
              <Select value={filters.tenantId} onChange={(e) => set('tenantId', e.target.value)}>
                <option value="">All companies</option>
                {(companies.data ?? []).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Select>
            </Field>
          )}
          <Field label="Category">
            <Select value={filters.category} onChange={(e) => set('category', e.target.value as AuditCategory | '')}>
              <option value="">All</option>
              <option value="Auth">Auth</option>
              <option value="Request">Request</option>
              <option value="Data">Data</option>
            </Select>
          </Field>
          <Field label="User email contains">
            <Input value={filters.user} onChange={(e) => set('user', e.target.value)} placeholder="ali@" />
          </Field>
          <Field label="Action contains">
            <Input value={filters.action} onChange={(e) => set('action', e.target.value)} placeholder="Login" />
          </Field>
          <Field label="Entity type">
            <Input value={filters.entityType} onChange={(e) => set('entityType', e.target.value)} placeholder="Employee" />
          </Field>
          <Field label="From">
            <Input type="date" value={filters.from} onChange={(e) => set('from', e.target.value)} />
          </Field>
          <Field label="To">
            <Input type="date" value={filters.to} min={filters.from || undefined} onChange={(e) => set('to', e.target.value)} />
          </Field>
        </div>
        {dirty && (
          <div className="mt-3 flex justify-end">
            <Button
              variant="ghost"
              size="sm"
              icon={<RotateCcw className="size-3.5" />}
              onClick={() => {
                setFilters(EMPTY);
                setPage(1);
              }}
            >
              Reset filters
            </Button>
          </div>
        )}
      </Card>

      <Card>
        {logs.isLoading ? (
          <SkeletonRows rows={10} cols={7} />
        ) : logs.isError ? (
          <ErrorState error={logs.error} onRetry={() => logs.refetch()} />
        ) : !logs.data || logs.data.items.length === 0 ? (
          <EmptyState icon={ScrollText} title="No events found" description={dirty ? 'Try widening your filters.' : 'Activity will show up here.'} />
        ) : (
          <>
            <Table className={logs.isFetching ? 'opacity-70 transition-opacity' : undefined}>
              <THead>
                <tr>
                  <Th>Time</Th>
                  <Th>Category</Th>
                  {isPlatform && <Th>Company</Th>}
                  <Th>User</Th>
                  <Th>Role</Th>
                  <Th>Action</Th>
                  <Th>Entity / path</Th>
                  <Th className="text-right">Status</Th>
                  <Th>IP</Th>
                </tr>
              </THead>
              <TBody>
                {logs.data.items.map((l) => (
                  <Tr key={l.id} onClick={() => setSelected(l)}>
                    <Td className="tabular-nums">{formatDateTimeUtc(l.timestampUtc)}</Td>
                    <Td>
                      <StatusBadge status={l.category} />
                    </Td>
                    {isPlatform && <Td>{l.companyName ?? <span className="text-zinc-400">Platform</span>}</Td>}
                    <Td>{l.userEmail ?? '—'}</Td>
                    <Td>{l.role ? roleLabel(l.role) : '—'}</Td>
                    <Td className="font-medium text-zinc-900 dark:text-zinc-100">{l.action}</Td>
                    <Td className="max-w-xs truncate">
                      {l.entityType ? (
                        <>
                          {l.entityType}
                          {l.entityId && <span className="text-zinc-400"> · {l.entityId.slice(0, 8)}</span>}
                        </>
                      ) : (
                        <span className="font-mono text-xs">{l.path ?? '—'}</span>
                      )}
                    </Td>
                    <Td className="text-right">
                      {l.statusCode != null ? <Badge tone={statusTone(l.statusCode)}>{l.statusCode}</Badge> : '—'}
                    </Td>
                    <Td className="font-mono text-xs">{l.ipAddress ?? '—'}</Td>
                  </Tr>
                ))}
              </TBody>
            </Table>
            <Pagination page={page} pageSize={PAGE_SIZE} total={logs.data.total} onPage={setPage} />
          </>
        )}
      </Card>

      <Drawer
        open={!!selected}
        onClose={() => setSelected(null)}
        title={selected?.action ?? ''}
        description={selected ? formatDateTimeUtc(selected.timestampUtc) : undefined}
        width="lg"
      >
        {selected && <AuditDetail log={selected} />}
      </Drawer>
    </>
  );
}

function AuditDetail({ log }: { log: AuditLog }) {
  const rows: Array<[string, React.ReactNode]> = [
    ['Event ID', log.id],
    ['Category', <StatusBadge key="c" status={log.category} />],
    ['Company', log.companyName ?? 'Platform'],
    ['User', log.userEmail],
    ['Role', log.role ? roleLabel(log.role) : null],
    ['Action', log.action],
    ['Entity type', log.entityType],
    ['Entity ID', log.entityId],
    ['Path', log.path],
    ['Status code', log.statusCode],
    ['Duration', log.durationMs != null ? `${log.durationMs} ms` : null],
    ['IP address', log.ipAddress],
    ['User agent', log.userAgent],
    ['Correlation ID', log.correlationId],
  ];
  return (
    <div className="space-y-6">
      <dl className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
        {rows.map(([k, v]) => (
          <div key={k} className="grid grid-cols-[140px_1fr] gap-3 px-3 py-2 text-sm">
            <dt className="text-zinc-500 dark:text-zinc-400">{k}</dt>
            <dd className="min-w-0 break-all text-zinc-900 dark:text-zinc-100">{v ?? <span className="text-zinc-400">—</span>}</dd>
          </div>
        ))}
      </dl>
      {log.changes && (
        <div>
          <h3 className="mb-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">Changes</h3>
          <ChangesDiff changes={log.changes} />
        </div>
      )}
    </div>
  );
}
