import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlarmClockPlus, Coffee, Search, Timer } from 'lucide-react';
import { attendanceApi } from '../../api/attendance';
import { qk } from '../../api/keys';
import { orgApi } from '../../api/organization';
import {
  Badge,
  Card,
  EmptyState,
  ErrorState,
  Field,
  Input,
  PageHeader,
  Select,
  SkeletonRows,
  StatCard,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { formatDate, formatMinutes, formatTime, todayIso } from '../../lib/format';
import { tx } from '../../lib/i18n';

/** Who took breaks, for how long, and who went over the shift's allowance. */
export function BreaksPage() {
  const [from, setFrom] = useState(todayIso());
  const [to, setTo] = useState(todayIso());
  const [department, setDepartment] = useState('');
  const [search, setSearch] = useState('');
  const departments = useQuery({ queryKey: qk.departments, queryFn: orgApi.departments });
  const breaks = useQuery({
    queryKey: ['attendance', 'breaks', from, to, department],
    queryFn: () => attendanceApi.breaks(from, to, department),
    enabled: !!from && !!to && from <= to,
    refetchInterval: 60_000,
  });

  const rows = useMemo(() => {
    const s = search.trim().toLowerCase();
    const list = breaks.data ?? [];
    return s ? list.filter((r) => r.fullName.toLowerCase().includes(s) || r.employeeCode.toLowerCase().includes(s)) : list;
  }, [breaks.data, search]);
  const totals = useMemo(() => {
    const list = breaks.data ?? [];
    return {
      onBreak: list.filter((r) => r.onBreak).length,
      over: list.filter((r) => r.extraMinutes > 0).length,
      average: list.length ? Math.round(list.reduce((s, r) => s + r.minutes, 0) / list.length) : 0,
    };
  }, [breaks.data]);

  return (
    <>
      <PageHeader title="Breaks" description="Lunch, prayer, tea and personal breaks marked from the check-in card. Longest over the allowance first." />
      <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4">
        <StatCard label="On a break now" value={totals.onBreak} icon={Coffee} tone="blue" loading={breaks.isLoading} />
        <StatCard label="Over the allowance" value={totals.over} icon={AlarmClockPlus} tone="red" loading={breaks.isLoading} />
        <StatCard label="Average per day" value={formatMinutes(totals.average)} icon={Timer} loading={breaks.isLoading} />
      </div>
      <Card>
        <div className="grid gap-3 border-b border-zinc-100 p-4 sm:grid-cols-2 lg:grid-cols-4 dark:border-zinc-800">
          <Field label="From">
            <Input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
          </Field>
          <Field label="To">
            <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
          </Field>
          <Field label="Department">
            <Select value={department} onChange={(e) => setDepartment(e.target.value)}>
              <option value="">{tx('All departments')}</option>
              {(departments.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Search">
            <div className="relative">
              <Search className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-zinc-400" aria-hidden="true" />
              <Input className="ps-9" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={tx('Name or code')} />
            </div>
          </Field>
        </div>
        {breaks.isLoading ? (
          <SkeletonRows rows={5} cols={5} />
        ) : breaks.isError ? (
          <ErrorState error={breaks.error} onRetry={() => breaks.refetch()} />
        ) : !rows.length ? (
          <EmptyState icon={Coffee} title="No breaks" description="Nobody marked a break in these days." className="py-10" />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Date</Th>
                <Th>Breaks</Th>
                <Th className="text-end">Total</Th>
                <Th className="text-end">Allowed</Th>
                <Th className="text-end">Extra</Th>
              </tr>
            </THead>
            <TBody>
              {rows.map((r) => (
                <Tr key={`${r.employeeId}-${r.date}`}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{r.fullName}</p>
                    <p className="text-xs text-zinc-500 dark:text-zinc-400">
                      {r.employeeCode}
                      {r.department ? ` · ${r.department}` : ''}
                    </p>
                  </Td>
                  <Td className="whitespace-nowrap">{formatDate(r.date)}</Td>
                  <Td>
                    <ul className="space-y-0.5 text-xs text-zinc-600 dark:text-zinc-300">
                      {r.breaks.map((b) => (
                        <li key={b.id} className="whitespace-nowrap">
                          {tx(b.kind)} · {formatTime(b.start)} – {b.end ? formatTime(b.end) : <Badge tone="amber">{tx('On break')}</Badge>}
                          <span className="text-zinc-400"> ({formatMinutes(b.minutes)})</span>
                        </li>
                      ))}
                    </ul>
                  </Td>
                  <Td className="text-end tabular-nums">{formatMinutes(r.minutes)}</Td>
                  <Td className="text-end text-zinc-500 tabular-nums dark:text-zinc-400">{r.allowedMinutes ? formatMinutes(r.allowedMinutes) : '—'}</Td>
                  <Td className="text-end">{r.extraMinutes > 0 ? <Badge tone="red">+{formatMinutes(r.extraMinutes)}</Badge> : '—'}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
    </>
  );
}
