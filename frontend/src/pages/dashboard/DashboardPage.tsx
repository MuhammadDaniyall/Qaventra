import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CalendarHeart, ClipboardCheck, Clock, Palmtree, UserCheck, UserPlus, Users, Wallet } from 'lucide-react';
import { dashboardApi } from '../../api/dashboard';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isApprover, isHr } from '../../auth/roles';
import {
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  ErrorState,
  PageHeader,
  Skeleton,
  StatCard,
  StatusBadge,
} from '../../components/ui';
import { formatCurrency, formatDate, monthLabel } from '../../lib/format';
import { CheckInCard } from '../attendance/CheckInCard';
import { AttendanceTrendChart, DepartmentChart } from './Charts';

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
}

export function DashboardPage() {
  const user = useUser();
  const hr = isHr(user);
  const approver = isApprover(user);
  const dash = useQuery({ queryKey: qk.dashboard, queryFn: dashboardApi.get });
  const d = dash.data;
  const loading = dash.isLoading;
  const showCheckIn = !!user.employeeId && (user.role === 'Employee' || user.role === 'Manager');

  return (
    <>
      <PageHeader
        title={`${greeting()}, ${user.fullName.split(' ')[0]}`}
        description={`Here's what's happening at ${user.companyName ?? 'your company'} today.`}
      />

      {showCheckIn && <CheckInCard className="mb-6" />}

      {dash.isError ? (
        <Card>
          <ErrorState error={dash.error} onRetry={() => dash.refetch()} />
        </Card>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
            <StatCard label="Headcount" value={d?.headcount} icon={Users} loading={loading} />
            <StatCard label="Present today" value={d?.presentToday} icon={UserCheck} tone="green" loading={loading} />
            <StatCard label="Late today" value={d?.lateToday} icon={Clock} tone="amber" loading={loading} />
            <StatCard label="On leave" value={d?.onLeaveToday} icon={Palmtree} tone="blue" loading={loading} />
            <StatCard
              label="Pending approvals"
              value={d?.pendingApprovals}
              icon={ClipboardCheck}
              tone="violet"
              loading={loading}
              hint={
                approver && d && d.pendingApprovals > 0 ? (
                  <Link to="/leave/approvals" className="font-medium text-accent-600 hover:underline dark:text-accent-400">
                    Review
                  </Link>
                ) : undefined
              }
            />
            <StatCard label="New joiners" value={d?.newJoinersThisMonth} icon={UserPlus} tone="accent" loading={loading} hint="This month" />
          </div>

          <div className="mt-6 grid gap-6 lg:grid-cols-3">
            <Card className="lg:col-span-2">
              <CardHeader title="Attendance" description="Check-ins over the last 14 days" />
              <CardBody>
                {loading || !d ? <Skeleton className="h-64 w-full" /> : <AttendanceTrendChart data={d.attendanceTrend} />}
              </CardBody>
            </Card>
            <Card>
              <CardHeader title="Headcount by department" description="Active and probation employees" />
              <CardBody>
                {loading || !d ? (
                  <Skeleton className="h-48 w-full" />
                ) : d.byDepartment.length === 0 ? (
                  <EmptyState icon={Users} title="No employees yet" className="py-8" />
                ) : (
                  <DepartmentChart data={d.byDepartment} />
                )}
              </CardBody>
            </Card>
          </div>

          <div className="mt-6 grid gap-6 lg:grid-cols-3">
            {hr && (
              <Card className="lg:col-span-2">
                <CardHeader
                  title="Last payroll"
                  actions={
                    <Link to="/payroll" className="text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
                      Open payroll
                    </Link>
                  }
                />
                <CardBody>
                  {loading ? (
                    <Skeleton className="h-20 w-full" />
                  ) : !d?.lastPayroll ? (
                    <EmptyState
                      icon={Wallet}
                      title="No payroll run yet"
                      description="Run payroll once salaries are set up."
                      className="py-6"
                      action={
                        <Link to="/payroll" className="text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
                          Go to payroll
                        </Link>
                      }
                    />
                  ) : (
                    <Link to={`/payroll/runs/${d.lastPayroll.id}`} className="block rounded-lg focus-visible:outline-2 focus-visible:outline-accent-500">
                      <div className="flex flex-wrap items-center gap-3">
                        <p className="text-lg font-semibold text-zinc-900 dark:text-white">
                          {monthLabel(d.lastPayroll.year, d.lastPayroll.month)}
                        </p>
                        <StatusBadge status={d.lastPayroll.status} />
                        <span className="text-sm text-zinc-500">{d.lastPayroll.employees} employees</span>
                      </div>
                      <dl className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-3">
                        {[
                          ['Gross', d.lastPayroll.totalGross],
                          ['Deductions', d.lastPayroll.totalDeductions],
                          ['Net pay', d.lastPayroll.totalNet],
                        ].map(([k, v]) => (
                          <div key={k as string} className="rounded-lg bg-zinc-50 p-3 dark:bg-zinc-800/50">
                            <dt className="text-xs text-zinc-500 dark:text-zinc-400">{k}</dt>
                            <dd className="mt-0.5 font-semibold text-zinc-900 tabular-nums dark:text-white">
                              {formatCurrency(v as number)}
                            </dd>
                          </div>
                        ))}
                      </dl>
                    </Link>
                  )}
                </CardBody>
              </Card>
            )}
            <Card className={hr ? undefined : 'lg:col-span-3'}>
              <CardHeader title="Upcoming holidays" />
              {loading ? (
                <CardBody>
                  <Skeleton className="h-24 w-full" />
                </CardBody>
              ) : !d?.upcomingHolidays.length ? (
                <EmptyState icon={CalendarHeart} title="No upcoming holidays" className="py-8" />
              ) : (
                <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                  {d.upcomingHolidays.map((h) => (
                    <li key={h.id} className="flex items-center gap-3 px-5 py-3">
                      <div className="flex size-10 shrink-0 flex-col items-center justify-center rounded-lg bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300">
                        <span className="text-[10px] leading-none font-semibold uppercase">{formatDate(h.date).split(' ')[1]}</span>
                        <span className="text-sm leading-tight font-semibold">{formatDate(h.date).split(' ')[0]}</span>
                      </div>
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{h.name}</p>
                        <p className="text-xs text-zinc-500 dark:text-zinc-400">{formatDate(h.date)}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>
        </>
      )}
    </>
  );
}
