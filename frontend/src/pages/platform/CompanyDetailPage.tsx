import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { Activity, Banknote, KeyRound, LogIn, Pencil, Power, Receipt, Trash2, UserCog, Users } from 'lucide-react';
import { platformApi } from '../../api/platform';
import { qk } from '../../api/keys';
import {
  Button,
  Card,
  CardBody,
  CardHeader,
  EmptyState,
  ErrorState,
  PageHeader,
  PageSpinner,
  SkeletonRows,
  StatCard,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { accessKind, CYCLE_LABELS, daysLeftLabel } from '../../lib/billing';
import { cn } from '../../lib/cn';
import { formatCurrency, formatDate, formatDateTimeUtc } from '../../lib/format';
import type { Company } from '../../types';
import { AccessBadge, seatsLabel } from './companyUi';
import { useCompanyActions } from './useCompanyActions';

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="grid grid-cols-1 gap-1 px-5 py-3 sm:grid-cols-[180px_1fr] sm:gap-4">
      <dt className="text-sm text-zinc-500 dark:text-zinc-400">{label}</dt>
      <dd className="text-sm break-words text-zinc-900 dark:text-zinc-100">{value ?? <span className="text-zinc-500 dark:text-zinc-400">—</span>}</dd>
    </div>
  );
}

export function CompanyDetailPage() {
  const { id = '' } = useParams();
  const company = useQuery({ queryKey: [...qk.companies, id], queryFn: () => platformApi.company(id) });
  const payments = useQuery({ queryKey: [...qk.companies, id, 'payments'], queryFn: () => platformApi.payments(id) });
  const actions = useCompanyActions();
  const back = { to: '/platform/companies', label: 'Companies' };

  if (company.isLoading) return <PageSpinner />;
  if (company.isError || !company.data) {
    return (
      <>
        <PageHeader title="Company" back={back} />
        <Card>
          <ErrorState error={company.error} onRetry={() => company.refetch()} />
        </Card>
      </>
    );
  }
  const c = company.data;

  return (
    <>
      <PageHeader
        back={back}
        title={
          <span className="flex flex-wrap items-center gap-3">
            {c.name} <AccessBadge company={c} />
          </span>
        }
        description={`Customer since ${formatDate(c.createdAtUtc)}${c.city ? ` · ${c.city}` : ''}`}
        actions={
          <>
            <Button variant="secondary" icon={<Pencil className="size-4" />} onClick={() => actions.run('edit', c)}>
              Edit
            </Button>
            <Button variant="secondary" icon={<KeyRound className="size-4" />} onClick={() => actions.run('reset', c)}>
              Reset admin password
            </Button>
            <Button
              variant={c.isActive ? 'secondary' : 'success'}
              icon={<Power className="size-4" />}
              onClick={() => actions.run('status', c)}
            >
              {c.isActive ? 'Make inactive' : 'Activate'}
            </Button>
            <Button icon={<LogIn className="size-4" />} loading={actions.opening === c.id} onClick={() => actions.openCompany(c)}>
              Open company
            </Button>
          </>
        }
      />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard label="Active employees / licensed" value={seatsLabel(c)} icon={Users} />
        <StatCard label="User logins" value={c.users} icon={UserCog} tone="violet" />
        <StatCard label="Last activity" value={<span className="text-base">{formatDateTimeUtc(c.lastActivityUtc)}</span>} icon={Activity} tone="amber" />
      </div>

      <SubscriptionCard company={c} onRecordPayment={() => actions.run('payment', c)} onEdit={() => actions.run('edit', c)} />

      <Card className="mt-6">
        <CardHeader title="Payments" description="Subscription payments you have recorded for this company." />
        {payments.isLoading ? (
          <SkeletonRows rows={3} cols={5} />
        ) : payments.isError ? (
          <ErrorState error={payments.error} onRetry={() => payments.refetch()} />
        ) : !payments.data?.length ? (
          <EmptyState
            icon={Receipt}
            title="No payments yet"
            description={c.billingCycle ? 'Record a payment to extend the subscription.' : 'Set a billing cycle, then record payments here.'}
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Paid on</Th>
                <Th className="text-end">Amount</Th>
                <Th>Period</Th>
                <Th>Method</Th>
                <Th>Reference</Th>
                <Th>Notes</Th>
              </tr>
            </THead>
            <TBody>
              {payments.data.map((p) => (
                <Tr key={p.id}>
                  <Td>{formatDate(p.paidOn)}</Td>
                  <Td className="text-end font-medium text-zinc-900 tabular-nums dark:text-zinc-100">{formatCurrency(p.amount)}</Td>
                  <Td>
                    {formatDate(p.periodFrom)} – {formatDate(p.periodTo)}
                  </Td>
                  <Td>{p.method ?? '—'}</Td>
                  <Td className="font-mono text-xs">{p.reference ?? '—'}</Td>
                  <Td className="max-w-xs truncate" title={p.notes ?? undefined}>
                    {p.notes ?? '—'}
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader title="Company" />
          <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
            <Row label="Name" value={c.name} />
            <Row label="Phone" value={c.phone} />
            <Row label="City" value={c.city} />
            <Row label="Address" value={c.address} />
          </dl>
        </Card>
        <Card>
          <CardHeader title="Company Super Admin" description="Creates departments, designations and employees, and gives them logins." />
          <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
            <Row label="Name" value={c.adminName} />
            <Row label="Email" value={c.adminEmail} />
          </dl>
        </Card>
      </div>

      <Card className="mt-6 border-red-200 dark:border-red-500/30">
        <CardHeader title={<span className="text-red-700 dark:text-red-400">Danger zone</span>} />
        <CardBody className="flex flex-wrap items-center justify-between gap-4">
          <div className="text-sm text-zinc-600 dark:text-zinc-300">
            <p className="font-medium text-zinc-900 dark:text-zinc-100">Delete this company</p>
            <p>Permanently deletes all employees, attendance, leave, payroll and logins. This cannot be undone.</p>
          </div>
          <Button variant="danger" icon={<Trash2 className="size-4" />} onClick={() => actions.run('delete', c)}>
            Delete company
          </Button>
        </CardBody>
      </Card>
      {actions.dialogs}
    </>
  );
}

function SubscriptionCard({ company: c, onRecordPayment, onEdit }: { company: Company; onRecordPayment: () => void; onEdit: () => void }) {
  const kind = accessKind(c);
  const pct = c.maxEmployees ? Math.min(100, (c.employees / c.maxEmployees) * 100) : 0;
  return (
    <Card className="mt-6">
      <CardHeader
        title={
          <span className="flex items-center gap-2">
            Subscription <AccessBadge company={c} />
          </span>
        }
        description={c.lastPaymentOn ? `Last payment ${formatDate(c.lastPaymentOn)}` : 'No payments recorded yet'}
        actions={
          <div className="flex flex-col items-end gap-1">
            <Button size="sm" icon={<Banknote className="size-3.5" />} disabled={!c.billingCycle} onClick={onRecordPayment}>
              Record payment
            </Button>
            {!c.billingCycle && (
              <button type="button" onClick={onEdit} className="text-xs text-accent-600 hover:underline dark:text-accent-400">
                Set a billing cycle first
              </button>
            )}
          </div>
        }
      />
      <CardBody className="grid gap-6 md:grid-cols-3">
        <div>
          <p className="text-xs font-medium text-zinc-500 dark:text-zinc-400">Licensed seats</p>
          <p className="mt-1 text-lg font-semibold text-zinc-900 tabular-nums dark:text-white">
            {c.employees} <span className="text-sm font-normal text-zinc-500 dark:text-zinc-400">of {c.maxEmployees ?? 'unlimited'} active employees</span>
          </p>
          {c.maxEmployees != null && (
            <div className="mt-2 h-2 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" role="progressbar" aria-valuenow={c.employees} aria-valuemin={0} aria-valuemax={c.maxEmployees}>
              <div className={cn('h-full rounded-full', pct >= 100 ? 'bg-red-500' : pct >= 85 ? 'bg-amber-500' : 'bg-accent-500')} style={{ width: `${pct}%` }} />
            </div>
          )}
        </div>
        <dl className="space-y-1.5 text-sm">
          <div className="flex justify-between gap-3">
            <dt className="text-zinc-500 dark:text-zinc-400">Billing cycle</dt>
            <dd>{c.billingCycle ? CYCLE_LABELS[c.billingCycle] : 'None'}</dd>
          </div>
          <div className="flex justify-between gap-3">
            <dt className="text-zinc-500 dark:text-zinc-400">Per employee / month</dt>
            <dd className="tabular-nums">{formatCurrency(c.pricePerEmployee ?? 0)}</dd>
          </div>
          <div className="flex justify-between gap-3">
            <dt className="text-zinc-500 dark:text-zinc-400">Base fee / cycle</dt>
            <dd className="tabular-nums">{formatCurrency(c.planPrice ?? 0)}</dd>
          </div>
          <div className="flex justify-between gap-3 border-t border-zinc-100 pt-1.5 font-medium dark:border-zinc-800">
            <dt>Bill per cycle</dt>
            <dd className="tabular-nums">{c.amountPerCycle != null ? formatCurrency(c.amountPerCycle) : '—'}</dd>
          </div>
        </dl>
        <div>
          <p className="text-xs font-medium text-zinc-500 dark:text-zinc-400">Expires on</p>
          <p className="mt-1 text-lg font-semibold text-zinc-900 dark:text-white">
            {c.subscriptionEndsOn ? formatDate(c.subscriptionEndsOn) : 'No expiry'}
          </p>
          <p
            className={cn(
              'text-sm',
              kind === 'expired' ? 'text-red-600 dark:text-red-400' : kind === 'expiring' ? 'text-amber-700 dark:text-amber-300' : 'text-zinc-500 dark:text-zinc-400',
            )}
          >
            {c.subscriptionEndsOn ? daysLeftLabel(c) : 'Users can sign in indefinitely.'}
          </p>
          {kind === 'inactive' && <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">Inactive — no one at this company can sign in.</p>}
        </div>
      </CardBody>
    </Card>
  );
}
