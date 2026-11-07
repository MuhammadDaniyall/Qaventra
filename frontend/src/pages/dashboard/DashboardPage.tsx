import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ClipboardCheck, UserCheck, UserPlus, Users, Wallet } from 'lucide-react';
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
  Skeleton,
  StatCard,
  StatusBadge,
} from '../../components/ui';
import { formatCurrency, monthLabel } from '../../lib/format';
import type { Dashboard } from '../../types';
import { CheckInCard } from '../attendance/CheckInCard';
import { DashboardNews } from '../announcements/DashboardNews';
import { WelcomeBanner } from '../onboarding/WelcomePage';
import { AttendanceTrendChart, DepartmentChart } from './Charts';
import { DashboardHero, heroLinks } from './DashboardHero';
import { ApprovalsPreview, ModuleOverview } from './DashboardExtras';
import { useDepartments } from '../../api/hooks';
import { NewRequestMenu } from '../requests/MyRequestsPage';
import { SetupGuide } from './SetupGuide';
import { WhoIsInCard } from './WhoIsInCard';
import { ActionCenter, CelebrationsCard, HolidaysCard, LeaveBalanceCard, MyRequestsCard } from './HomeCards';
import { tx, txf } from '../../lib/i18n';

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
}

const todayLabel = () => new Date().toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' });

/** Home: the employee's day first, then the team, then company numbers for HR. */
export function DashboardPage() {
  const user = useUser();
  const hr = isHr(user);
  // An access template without payroll hides the payroll card and reminder.
  const payroll = hr && (user.role !== 'HrManager' || !user.modules || user.modules.includes('payroll'));
  const approver = isApprover(user);
  const self = !!user.employeeId;
  const dash = useQuery({ queryKey: qk.dashboard, queryFn: dashboardApi.get });
  const d = dash.data;
  const loading = dash.isLoading;
  const showCheckIn = self && (user.role === 'Employee' || user.role === 'Manager');

  // Average check-ins a day over the last 14 days, counting only days anyone checked in (skips weekends and holidays).
  const worked = d?.attendanceTrend.filter((p) => p.present > 0) ?? [];
  const avgIn = worked.length ? Math.round(worked.reduce((sum, p) => sum + p.present, 0) / worked.length) : 0;
  // Department bars open the employee list filtered to that department.
  const departments = useDepartments();
  const departmentHref = (name: string) => {
    const id = departments.data?.find((x) => x.name === name)?.id;
    return id ? `/employees?department=${id}` : '/employees';
  };
  const news = (
    <>
      <DashboardNews className="mb-6" />
      <CelebrationsCard className="mb-6" />
    </>
  );

  return (
    <>
      <DashboardHero
        greeting={`${tx(greeting())}, ${user.fullName.split(' ')[0]}`}
        subtitle={`${todayLabel()} · ${user.companyName ?? tx('your company')}`}
        d={d}
        loading={loading}
        showToday={approver && !dash.isError}
        todayTo={hr ? '/attendance/daily' : undefined}
        links={heroLinks({ hr, approver, payroll, pending: d?.pendingApprovals ?? 0 })}
        actions={self && <NewRequestMenu variant="secondary" />}
      />

      {hr && <SetupGuide className="mb-6" />}
      {self && <WelcomeBanner className="mb-6" />}
      {!approver && news}

      {self && (
        <section aria-label="Your day" className="mb-8 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {showCheckIn && <CheckInCard className="md:col-span-2" />}
          <LeaveBalanceCard className={showCheckIn ? undefined : 'lg:col-span-1'} />
          {!showCheckIn && <MyRequestsCard className="lg:col-span-2" />}
        </section>
      )}

      {dash.isError ? (
        <Card>
          <ErrorState error={dash.error} onRetry={() => dash.refetch()} />
        </Card>
      ) : approver ? (
        <>
          <div className="mb-3 flex items-baseline justify-between">
            <h2 className="text-base font-semibold text-zinc-900 dark:text-white">{tx('Today at')} {user.companyName ?? tx('your company')}</h2>
          </div>
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatCard label="Headcount" value={d?.headcount} icon={Users} loading={loading} hint="Active and probation" to={hr ? '/employees' : '/org-chart'} />
            <StatCard label="New joiners" value={d?.newJoinersThisMonth} icon={UserPlus} tone="green" loading={loading} hint="This month" to={hr ? '/onboarding' : undefined} />
            <StatCard label="Pending approvals" value={d?.pendingApprovals} icon={ClipboardCheck} tone="violet" loading={loading} hint="Waiting for you" to="/approvals" />
            <StatCard label="Average check-ins" value={avgIn} icon={UserCheck} tone="blue" loading={loading} hint="A working day, last 14 days" to={hr ? '/attendance/monthly' : undefined} />
          </div>

          <div className="mt-6 grid gap-6 lg:grid-cols-3">
            <ActionCenter d={d} hr={hr} payroll={payroll} />
            <Card className="lg:col-span-2">
              <CardHeader
                title="Attendance"
                description="Check-ins over the last 14 days"
                actions={
                  hr && (
                    <Link to="/attendance/daily" className="text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
                      {tx('Daily attendance')}
                    </Link>
                  )
                }
              />
              <CardBody>
                {loading || !d ? <Skeleton className="h-64 w-full" /> : <AttendanceTrendChart data={d.attendanceTrend} />}
              </CardBody>
            </Card>
          </div>

          <div className="mt-6 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
            <WhoIsInCard d={d} linkPeople={hr} />
            <Card>
              <CardHeader
                title="Headcount by department"
                description="Active and probation employees"
                actions={
                  hr && (
                    <Link to="/employees" className="text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
                      {tx('See all')}
                    </Link>
                  )
                }
              />
              <CardBody>
                {loading || !d ? (
                  <Skeleton className="h-48 w-full" />
                ) : d.byDepartment.length === 0 ? (
                  <EmptyState icon={Users} title="No employees yet" className="py-8" />
                ) : (
                  <DepartmentChart data={d.byDepartment} hrefFor={hr ? departmentHref : undefined} />
                )}
              </CardBody>
            </Card>
            <div className="flex flex-col gap-6 md:col-span-2 md:grid md:grid-cols-2 lg:col-span-1 lg:flex">
              {payroll && <LastPayrollCard d={d} loading={loading} />}
              <HolidaysCard holidays={d?.upcomingHolidays} loading={loading} seeAllTo={hr ? '/attendance/settings?tab=holidays' : undefined} />
            </div>
          </div>

          {/* Both columns end on one line: the approvals card stretches to the news column, and the last news card
              grows to the approvals card. With no news, approvals take the full width. */}
          <div className="mt-6 grid gap-6 lg:grid-cols-3 lg:has-[>.news-col:empty]:grid-cols-1">
            <ApprovalsPreview className="min-w-0" />
            <div className="news-col flex min-w-0 flex-col gap-6 empty:hidden lg:col-span-2 [&>*:last-child]:flex-1">
              <DashboardNews />
              <CelebrationsCard />
            </div>
          </div>
          {showCheckIn && <MyRequestsCard className="mt-6" />}

          <ModuleOverview user={user} className="mt-8" />
        </>
      ) : (
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          <MyRequestsCard className="lg:col-span-2" />
          <HolidaysCard holidays={d?.upcomingHolidays} loading={loading} />
        </div>
      )}
    </>
  );
}

function LastPayrollCard({ d, loading }: { d: Dashboard | undefined; loading: boolean }) {
  const run = d?.lastPayroll;
  return (
    <Card>
      <CardHeader
        title="Last payroll"
        actions={
          <Link to="/payroll" className="text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
            {tx('Open payroll')}
          </Link>
        }
      />
      <CardBody>
        {loading ? (
          <Skeleton className="h-20 w-full" />
        ) : !run ? (
          <EmptyState icon={Wallet} title="No payroll run yet" description="Run payroll once salaries are set up." className="py-6" />
        ) : (
          <Link
            to={`/payroll/runs/${run.id}`}
            className="group relative block overflow-hidden rounded-2xl border border-indigo-100 bg-gradient-to-br from-indigo-50 via-violet-50 to-fuchsia-50 p-4 shadow-[0_14px_30px_-20px_rgb(99_102_241/0.55)] transition-all hover:-translate-y-0.5 hover:shadow-[0_20px_40px_-20px_rgb(99_102_241/0.6)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500 dark:border-indigo-400/15 dark:from-indigo-500/[0.12] dark:via-violet-500/[0.08] dark:to-fuchsia-500/[0.10]"
          >
            <div className="pointer-events-none absolute inset-0" aria-hidden="true">
              <div className="absolute -end-12 -top-14 size-40 rounded-full bg-fuchsia-300/35 blur-2xl dark:bg-fuchsia-500/20" />
              <div className="absolute -bottom-16 -start-10 size-40 rounded-full bg-indigo-300/30 blur-2xl dark:bg-indigo-500/20" />
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_1px_1px,rgb(99_102_241/0.14)_1px,transparent_0)] [background-size:16px_16px] [mask-image:linear-gradient(to_bottom_left,black,transparent_60%)]" />
            </div>

            <div className="relative flex items-center gap-3">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-fuchsia-500 text-white shadow-md shadow-indigo-500/30 ring-1 ring-inset ring-white/20">
                <Wallet className="size-[18px]" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="font-semibold text-zinc-900 dark:text-white">{monthLabel(run.year, run.month)}</p>
                <p className="text-xs text-zinc-500 dark:text-zinc-400">
                  {txf(run.employees === 1 ? '{n} employee' : '{n} employees', { n: run.employees })}
                </p>
              </div>
              <span className="ms-auto">
                <StatusBadge status={run.status} />
              </span>
            </div>

            <p className="relative mt-4 text-xs font-medium text-zinc-500 dark:text-zinc-400">{tx('Net pay')}</p>
            <p className="relative text-brand text-[28px] leading-tight font-semibold tracking-tight tabular-nums">{formatCurrency(run.totalNet)}</p>

            {/* Net as a share of gross: the rest went to tax and other deductions. */}
            <div className="relative mt-3 h-1.5 overflow-hidden rounded-full bg-white/80 ring-1 ring-indigo-100 dark:bg-white/[0.06] dark:ring-white/5">
              <div
                className="h-full rounded-full bg-gradient-to-r from-indigo-500 via-violet-500 to-fuchsia-500 rtl:bg-gradient-to-l"
                style={{ width: `${run.totalGross > 0 ? Math.min(100, (run.totalNet / run.totalGross) * 100) : 0}%` }}
              />
            </div>

            <div className="relative mt-3 grid grid-cols-2 gap-2 text-xs">
              <div className="rounded-xl bg-white/75 px-3 py-2 ring-1 ring-indigo-100/80 backdrop-blur dark:bg-white/[0.05] dark:ring-white/[0.06]">
                <p className="text-zinc-500 dark:text-zinc-400">{tx('Gross')}</p>
                <p className="font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(run.totalGross)}</p>
              </div>
              <div className="rounded-xl bg-white/75 px-3 py-2 ring-1 ring-indigo-100/80 backdrop-blur dark:bg-white/[0.05] dark:ring-white/[0.06]">
                <p className="text-zinc-500 dark:text-zinc-400">{tx('Deductions')}</p>
                <p className="font-semibold text-rose-600 tabular-nums dark:text-rose-400">{formatCurrency(run.totalDeductions)}</p>
              </div>
            </div>
          </Link>
        )}
      </CardBody>
    </Card>
  );
}
