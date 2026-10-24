import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import {
  Activity,
  AlarmClock,
  Banknote,
  Building,
  Building2,
  CalendarX,
  CircleOff,
  LogIn,
  ShieldX,
  TrendingUp,
  UserCog,
  Users,
} from 'lucide-react';
import { platformApi } from '../../api/platform';
import { qk } from '../../api/keys';
import { Button, Card, CardHeader, EmptyState, ErrorState, PageHeader, SkeletonRows, StatCard, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatCurrency, formatNumber } from '../../lib/format';
import { ExpiresCell } from './companyUi';
import { useCompanyActions } from './useCompanyActions';

export function PlatformDashboardPage() {
  const navigate = useNavigate();
  const stats = useQuery({ queryKey: qk.platformStats, queryFn: platformApi.stats });
  const actions = useCompanyActions();
  const s = stats.data;
  const loading = stats.isLoading;

  return (
    <>
      <PageHeader title="Platform overview" description="Every company on the platform at a glance." />
      {stats.isError ? (
        <Card>
          <ErrorState error={stats.error} onRetry={() => stats.refetch()} />
        </Card>
      ) : (
        <>
          <h2 className="mb-3 text-xs font-semibold tracking-wider text-zinc-500 uppercase dark:text-zinc-400">Companies & revenue</h2>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-4">
            <StatCard label="Companies" value={formatNumber(s?.companies)} icon={Building2} loading={loading} />
            <StatCard label="Active" value={formatNumber(s?.activeCompanies)} icon={Building} tone="green" loading={loading} />
            <StatCard label="Expiring soon (7 days)" value={formatNumber(s?.expiringSoon)} icon={AlarmClock} tone="amber" loading={loading} />
            <StatCard label="Expired" value={formatNumber(s?.expiredCompanies)} icon={CalendarX} tone="red" loading={loading} />
            <StatCard label="Inactive" value={formatNumber(s?.inactiveCompanies)} icon={CircleOff} tone="violet" loading={loading} />
            <StatCard label="Revenue this month" value={formatCurrency(s?.revenueThisMonth)} icon={Banknote} tone="green" loading={loading} />
            <StatCard label="Revenue this year" value={formatCurrency(s?.revenueThisYear)} icon={TrendingUp} tone="blue" loading={loading} />
            <StatCard label="Employees (all companies)" value={formatNumber(s?.employees)} icon={Users} tone="blue" loading={loading} />
          </div>
          <h2 className="mt-6 mb-3 text-xs font-semibold tracking-wider text-zinc-500 uppercase dark:text-zinc-400">Activity today</h2>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <StatCard label="User logins" value={formatNumber(s?.users)} icon={UserCog} tone="violet" loading={loading} />
            <StatCard label="Sign-ins" value={formatNumber(s?.loginsToday)} icon={LogIn} tone="green" loading={loading} />
            <StatCard label="Failed sign-ins" value={formatNumber(s?.failedLoginsToday)} icon={ShieldX} tone="red" loading={loading} />
            <StatCard label="Audit events" value={formatNumber(s?.eventsToday)} icon={Activity} tone="amber" loading={loading} />
          </div>
        </>
      )}

      <Card className="mt-6">
        <CardHeader
          title="Expiring soon"
          description="Subscriptions ending within 7 days."
          actions={
            <Link to="/platform/companies" className="text-sm font-medium text-accent-600 hover:underline dark:text-accent-400">
              All companies
            </Link>
          }
        />
        {loading ? (
          <SkeletonRows rows={3} cols={4} />
        ) : !s?.expiringCompanies.length ? (
          <EmptyState icon={AlarmClock} title="Nothing expiring this week" className="py-10" />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Company</Th>
                <Th>Expires</Th>
                <Th className="text-end">Bill per cycle</Th>
                <Th className="text-end">Action</Th>
              </tr>
            </THead>
            <TBody>
              {s.expiringCompanies.map((c) => (
                <Tr key={c.id} onClick={() => navigate(`/platform/companies/${c.id}`)}>
                  <Td className="font-medium text-zinc-900 dark:text-zinc-100">{c.name}</Td>
                  <Td>
                    <ExpiresCell company={c} />
                  </Td>
                  <Td className="text-end tabular-nums">{c.amountPerCycle != null ? formatCurrency(c.amountPerCycle) : '—'}</Td>
                  <Td className="text-end" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
                    <Button
                      size="sm"
                      variant="secondary"
                      icon={<Banknote className="size-3.5" />}
                      disabled={!c.billingCycle}
                      title={c.billingCycle ? undefined : 'Set a billing cycle first'}
                      onClick={() => actions.run('payment', c)}
                    >
                      Record payment
                    </Button>
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {actions.dialogs}
    </>
  );
}
