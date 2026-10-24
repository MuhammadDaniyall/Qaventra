import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Area, AreaChart, Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Clock, Palmtree, TrendingDown, Users } from 'lucide-react';
import { dashboardApi, type MonthTrend } from '../../api/dashboard';
import { Card, CardBody, CardHeader, ErrorState, PageHeader, Skeleton, StatCard } from '../../components/ui';
import { useChartColors } from '../../lib/chartColors';
import { formatNumber, monthLabel, monthName } from '../../lib/format';
import { ChartTooltip } from '../dashboard/Charts';
import { tx, txf } from '../../lib/i18n';

type Row = MonthTrend & { label: string };

function ChartCard({ title, description, label, children }: { title: string; description: string; label: string; children: ReactNode }) {
  return (
    <Card>
      <CardHeader title={title} description={description} />
      <CardBody>
        {/* Time runs left to right in both languages. */}
        <div className="h-56" role="img" aria-label={tx(label)} dir="ltr">
          <ResponsiveContainer width="100%" height="100%">
            {children as React.ReactElement}
          </ResponsiveContainer>
        </div>
      </CardBody>
    </Card>
  );
}

/** Twelve months of workforce numbers for HR: headcount, movement, leave and punctuality. */
export function InsightsPage() {
  const c = useChartColors();
  const trends = useQuery({ queryKey: ['dashboard', 'trends'], queryFn: dashboardApi.trends });
  const rows: Row[] = (trends.data ?? []).map((m) => ({ ...m, label: `${monthName(m.month, false)} ${String(m.year).slice(2)}` }));
  const tooltipLabel = (l: string) => {
    const r = rows.find((x) => x.label === l);
    return r ? monthLabel(r.year, r.month) : l;
  };

  const last = rows[rows.length - 1];
  const leavers = rows.reduce((t, r) => t + r.leavers, 0);
  const avgHeadcount = rows.length ? rows.reduce((t, r) => t + r.headcount, 0) / rows.length : 0;
  const turnover = avgHeadcount ? Math.round((leavers / avgHeadcount) * 1000) / 10 : 0;
  const leavePerMonth = rows.length ? rows.reduce((t, r) => t + r.leaveDays, 0) / rows.length : 0;

  const axis = {
    x: <XAxis dataKey="label" tick={{ fill: c.axis, fontSize: 11 }} axisLine={false} tickLine={false} interval="preserveStartEnd" minTickGap={8} />,
    y: <YAxis allowDecimals={false} tick={{ fill: c.axis, fontSize: 11 }} axisLine={false} tickLine={false} width={36} />,
    grid: <CartesianGrid vertical={false} stroke={c.grid} />,
    tip: <Tooltip cursor={{ fill: c.cursor }} content={<ChartTooltip labelFormat={tooltipLabel} />} />,
  };

  return (
    <>
      <PageHeader title="Workforce insights" description="The last 12 months: headcount, joiners and leavers, leave and punctuality." />
      {trends.isError ? (
        <Card>
          <ErrorState error={trends.error} onRetry={() => trends.refetch()} />
        </Card>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatCard label="Headcount now" value={last?.headcount} icon={Users} loading={trends.isLoading} />
            <StatCard label="Turnover, 12 months" value={trends.isLoading ? undefined : `${formatNumber(turnover)}%`} icon={TrendingDown} tone="red" loading={trends.isLoading} hint={txf('{count} left', { count: leavers })} />
            <StatCard label="Leave days a month" value={trends.isLoading ? undefined : formatNumber(Math.round(leavePerMonth * 10) / 10)} icon={Palmtree} tone="blue" loading={trends.isLoading} hint="Average" />
            <StatCard label="Hours a day" value={last ? formatNumber(last.averageWorkedHours) : undefined} icon={Clock} tone="green" loading={trends.isLoading} hint="This month, average" />
          </div>

          {trends.isLoading ? (
            <div className="mt-6 grid gap-6 lg:grid-cols-2">
              {[0, 1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-72 w-full rounded-xl" />
              ))}
            </div>
          ) : (
            <div className="mt-6 grid gap-6 lg:grid-cols-2">
              <ChartCard title="Headcount" description="People on the books at the end of each month" label="Headcount by month">
                <AreaChart data={rows} margin={{ top: 4, right: 4, left: -8, bottom: 0 }}>
                  {axis.grid}
                  {axis.x}
                  {axis.y}
                  {axis.tip}
                  <Area type="monotone" dataKey="headcount" name="Headcount" stroke={c.series1} strokeWidth={2} fill={c.series1} fillOpacity={0.12} />
                </AreaChart>
              </ChartCard>

              <ChartCard title="Joiners and leavers" description="Movement each month" label="Joiners and leavers by month">
                <BarChart data={rows} margin={{ top: 4, right: 4, left: -8, bottom: 0 }} barCategoryGap="24%">
                  {axis.grid}
                  {axis.x}
                  {axis.y}
                  {axis.tip}
                  <Bar dataKey="joiners" name="Joined" fill={c.series1} radius={[4, 4, 0, 0]} />
                  <Bar dataKey="leavers" name="Left" fill={c.series2} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ChartCard>

              <ChartCard title="Leave taken" description="Approved leave days, by the month they start" label="Leave days by month">
                <BarChart data={rows} margin={{ top: 4, right: 4, left: -8, bottom: 0 }} barCategoryGap="28%">
                  {axis.grid}
                  {axis.x}
                  {axis.y}
                  {axis.tip}
                  <Bar dataKey="leaveDays" name="Leave days" fill={c.series1} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ChartCard>

              <ChartCard title="Late arrivals" description="Check-ins after shift start and grace" label="Late arrivals by month">
                <BarChart data={rows} margin={{ top: 4, right: 4, left: -8, bottom: 0 }} barCategoryGap="28%">
                  {axis.grid}
                  {axis.x}
                  {axis.y}
                  {axis.tip}
                  <Bar dataKey="lateArrivals" name="Late arrivals" fill={c.series2} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ChartCard>
            </div>
          )}
          <div className="mt-3 flex items-center gap-4 text-xs text-zinc-500 dark:text-zinc-400">
            <span className="inline-flex items-center gap-1.5">
              <span className="size-2.5 rounded-sm" style={{ background: c.series1 }} /> Joined, headcount, leave
            </span>
            <span className="inline-flex items-center gap-1.5">
              <span className="size-2.5 rounded-sm" style={{ background: c.series2 }} /> Left, late
            </span>
          </div>
        </>
      )}
    </>
  );
}
