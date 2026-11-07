import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useChartColors } from '../../lib/chartColors';
import { formatDate } from '../../lib/format';
import type { NameCount, TrendPoint } from '../../types';

interface TipPayload {
  name?: string | number;
  value?: number | string;
  color?: string;
  dataKey?: string | number;
}

export function ChartTooltip({ active, payload, label, labelFormat }: {
  active?: boolean;
  payload?: TipPayload[];
  label?: string | number;
  labelFormat?: (l: string) => string;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border border-zinc-200 bg-white px-3 py-2 text-xs shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
      <p className="mb-1 font-medium text-zinc-900 dark:text-zinc-100">
        {labelFormat ? labelFormat(String(label)) : label}
      </p>
      {payload.map((p) => (
        <p key={String(p.dataKey)} className="flex items-center gap-2 text-zinc-600 dark:text-zinc-300">
          <span className="size-2 rounded-sm" style={{ background: p.color }} />
          <span>{p.name}</span>
          <span className="ms-auto ps-3 font-medium text-zinc-900 tabular-nums dark:text-zinc-100">{p.value}</span>
        </p>
      ))}
    </div>
  );
}

function shortDay(iso: string) {
  return formatDate(iso).split(' ').slice(0, 2).join(' ');
}

/** 14-day attendance as stacked bars: on time + late = everyone who checked in. */
export function AttendanceTrendChart({ data }: { data: TrendPoint[] }) {
  const c = useChartColors();
  const rows = data.map((d) => ({ date: d.date, onTime: Math.max(0, d.present - d.late), late: d.late }));
  return (
    <div>
      <div className="mb-3 flex items-center gap-4 text-xs text-zinc-600 dark:text-zinc-400">
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm" style={{ background: c.series1 }} /> On time
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm" style={{ background: c.series2 }} /> Late
        </span>
      </div>
      <div className="h-64" role="img" aria-label="Attendance over the last 14 days, on time versus late" dir="ltr">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={{ top: 4, right: 4, left: -20, bottom: 0 }} barCategoryGap="32%">
            <defs>
              <linearGradient id="att-on-time" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#8b5cf6" />
                <stop offset="100%" stopColor={c.series1} />
              </linearGradient>
            </defs>
            <CartesianGrid vertical={false} stroke={c.grid} strokeDasharray="4 4" />
            <XAxis
              dataKey="date"
              tickFormatter={shortDay}
              tick={{ fill: c.axis, fontSize: 11 }}
              axisLine={false}
              tickLine={false}
              interval="preserveStartEnd"
              minTickGap={12}
            />
            <YAxis allowDecimals={false} tick={{ fill: c.axis, fontSize: 11 }} axisLine={false} tickLine={false} />
            <Tooltip
              cursor={{ fill: c.cursor }}
              content={<ChartTooltip labelFormat={(l) => formatDate(l)} />}
            />
            <Bar dataKey="onTime" name="On time" stackId="a" fill="url(#att-on-time)" stroke={c.surface} strokeWidth={2} />
            <Bar dataKey="late" name="Late" stackId="a" fill={c.series2} stroke={c.surface} strokeWidth={2} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

/** A bar row: a link to the list behind it when there is one. */
function Row({ to, children }: { to?: string; children: ReactNode }) {
  return to ? (
    <Link
      to={to}
      className="-mx-2 block rounded-lg px-2 py-1 transition-colors hover:bg-indigo-50/60 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:bg-white/[0.03]"
    >
      {children}
    </Link>
  ) : (
    <div className="py-1">{children}</div>
  );
}

/** Headcount per department as gradient bars with the count and share (single series; the card title names it). */
export function DepartmentChart({ data, hrefFor }: { data: NameCount[]; hrefFor?: (name: string) => string }) {
  const rows = data.slice(0, 8);
  const max = Math.max(1, ...rows.map((r) => r.count));
  const total = data.reduce((sum, r) => sum + r.count, 0) || 1;
  return (
    <ul className="space-y-1.5" aria-label="Headcount by department">
      {rows.map((r, i) => (
        <li key={r.name}>
          <Row to={hrefFor?.(r.name)}>
          <div className="mb-1.5 flex items-baseline gap-2 text-sm">
            <span className="min-w-0 truncate text-zinc-700 dark:text-zinc-300">{r.name}</span>
            <span className="ms-auto font-semibold text-zinc-900 tabular-nums dark:text-white">{r.count}</span>
            <span className="w-9 text-end text-xs text-zinc-400 tabular-nums">{Math.round((r.count / total) * 100)}%</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-zinc-100 dark:bg-white/[0.06]">
            <div
              className="h-full rounded-full bg-gradient-to-r from-indigo-500 via-violet-500 to-fuchsia-500 transition-[width] duration-700 ease-out rtl:bg-gradient-to-l"
              style={{ width: `${Math.max(4, (r.count / max) * 100)}%`, opacity: 1 - i * 0.07 }}
            />
          </div>
          </Row>
        </li>
      ))}
    </ul>
  );
}
