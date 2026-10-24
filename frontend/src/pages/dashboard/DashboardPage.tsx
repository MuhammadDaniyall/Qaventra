import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import { ClipboardCheck, Clock, Palmtree, UserCheck, UserPlus, Users, Wallet } from 'lucide-react';
import { dashboardApi } from '../../api/dashboard';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isApprover, isHr } from '../../auth/roles';
import {
  Button,
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
import { formatCurrency, monthLabel } from '../../lib/format';
import type { Dashboard } from '../../types';
import { CheckInCard } from '../attendance/CheckInCard';
import { DashboardNews } from '../announcements/DashboardNews';
import { WelcomeBanner } from '../onboarding/WelcomePage';
import { AttendanceTrendChart, DepartmentChart } from './Charts';
import { NewRequestMenu } from '../requests/MyRequestsPage';
import { SetupGuide } from './SetupGuide';
import { ActionCenter, HolidaysCard, LeaveBalanceCard, MyRequestsCard } from './HomeCards';
import { tx } from '../../lib/i18n';

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
}

const todayLabel = () => new Date().toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' });

/** Home: the employee's day first, then the team, then company numbers for HR. */
export function DashboardPage() {
  const user = useUser();
  const navigate = useNavigate();
  const hr = isHr(user);
  const approver = isApprover(user);
  const self = !!user.employeeId;
  const dash = useQuery({ queryKey: qk.dashboard, queryFn: dashboardApi.get });
  const d = dash.data;
  const loading = dash.isLoading;
  const showCheckIn = self && (user.role === 'Employee' || user.role === 'Manager');

  return (
    <>
      <PageHeader
        title={`${tx(greeting())}, ${user.fullName.split(' ')[0]}`}
        description={`${todayLabel()} · ${user.companyName ?? 'your company'}`}
        actions={
          <>
            {self && (
              <NewRequestMenu variant={hr ? 'secondary' : 'primary'} />
            )}
            {hr && (
              <Button icon={<UserPlus className="size-4" />} onClick={() => navigate('/employees/new')}>
                Add employee
              </Button>
            )}
          </>
        }
      />

      {hr && <SetupGuide className="mb-6" />}
      {self && <WelcomeBanner className="mb-6" />}
      <DashboardNews className="mb-6" />

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
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3 2xl:grid-cols-6">
            <StatCard label="Headcount" value={d?.headcount} icon={Users} loading={loading} />
            <StatCard label="Present today" value={d?.presentToday} icon={UserCheck} tone="green" loading={loading} />
            <StatCard label="Late today" value={d?.lateToday} icon={Clock} tone="amber" loading={loading} />
            <StatCard label="On leave" value={d?.onLeaveToday} icon={Palmtree} tone="blue" loading={loading} />
            <StatCard label="Pending approvals" value={d?.pendingApprovals} icon={ClipboardCheck} tone="violet" loading={loading} />
            <StatCard label="New joiners" value={d?.newJoinersThisMonth} icon={UserPlus} tone="accent" loading={loading} hint="This month" />
          </div>

          <div className="mt-6 grid gap-6 lg:grid-cols-3">
            <ActionCenter d={d} hr={hr} />
            <Card className="lg:col-span-2">
              <CardHeader title="Attendance" description="Check-ins over the last 14 days" />
              <CardBody>
                {loading || !d ? <Skeleton className="h-64 w-full" /> : <AttendanceTrendChart data={d.attendanceTrend} />}
              </CardBody>
            </Card>
          </div>

          <div className="mt-6 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
            {showCheckIn && <MyRequestsCard />}
            <Card className={showCheckIn ? undefined : 'lg:col-span-1'}>
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
            {hr && <LastPayrollCard d={d} loading={loading} />}
            <HolidaysCard holidays={d?.upcomingHolidays} loading={loading} />
          </div>
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
          <Link to={`/payroll/runs/${run.id}`} className="block rounded-lg focus-visible:outline-2 focus-visible:outline-accent-500">
            <div className="flex flex-wrap items-center gap-2">
              <p className="font-semibold text-zinc-900 dark:text-white">{monthLabel(run.year, run.month)}</p>
              <StatusBadge status={run.status} />
            </div>
            <p className="mt-3 text-xs text-zinc-500 dark:text-zinc-400">Net pay · {run.employees} employees</p>
            <p className="text-2xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">{formatCurrency(run.totalNet)}</p>
            <div className="mt-3 flex gap-4 text-xs text-zinc-500 dark:text-zinc-400">
              <span>Gross {formatCurrency(run.totalGross)}</span>
              <span>Deductions {formatCurrency(run.totalDeductions)}</span>
            </div>
          </Link>
        )}
      </CardBody>
    </Card>
  );
}
