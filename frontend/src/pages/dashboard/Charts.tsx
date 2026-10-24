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
          <BarChart data={rows} margin={{ top: 4, right: 4, left: -20, bottom: 0 }} barCategoryGap="28%">
            <CartesianGrid vertical={false} stroke={c.grid} strokeDasharray="0" />
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
            <Bar dataKey="onTime" name="On time" stackId="a" fill={c.series1} stroke={c.surface} strokeWidth={2} />
            <Bar dataKey="late" name="Late" stackId="a" fill={c.series2} stroke={c.surface} strokeWidth={2} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

/** Headcount per department as horizontal bars (single series; the card title names it). */
export function DepartmentChart({ data }: { data: NameCount[] }) {
  const c = useChartColors();
  const rows = data.slice(0, 8);
  const height = Math.max(160, rows.length * 34 + 24);
  return (
    <div style={{ height }} role="img" aria-label="Headcount by department" dir="ltr">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={rows} layout="vertical" margin={{ top: 0, right: 28, left: 0, bottom: 0 }} barCategoryGap="30%">
          <XAxis type="number" hide allowDecimals={false} />
          <YAxis
            type="category"
            dataKey="name"
            width={110}
            tick={{ fill: c.axis, fontSize: 12 }}
            axisLine={false}
            tickLine={false}
          />
          <Tooltip cursor={{ fill: c.cursor }} content={<ChartTooltip />} />
          <Bar
            dataKey="count"
            name="Employees"
            fill={c.series1}
            radius={[0, 4, 4, 0]}
            label={{ position: 'right', fill: c.axis, fontSize: 11 }}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
