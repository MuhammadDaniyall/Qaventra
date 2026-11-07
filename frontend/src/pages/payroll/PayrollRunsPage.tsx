import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { tx } from '../../lib/i18n';
import { cn } from '../../lib/cn';
import { useNavigate } from 'react-router-dom';
import { Play, BookOpen, Coins, FilePen, Settings2, TrendingUp, PiggyBank, Users, Wallet } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  StatCard,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDateTimeUtc, monthLabel, monthName } from '../../lib/format';
import type { PayFrequency, PayPeriod, PayrollRun, PayrollRunDetail } from '../../types';
import { WarningsPanel } from './WarningsPanel';
import { RecordStampCell } from '../../components/RecordStamp';

/** The coloured edge on each run: draft amber, finalized green, anything else brand. */
const statusBar: Record<string, string> = {
  Draft: 'bg-amber-400',
  Finalized: 'bg-emerald-500',
};

/** The latest run at a glance, above the list. */
function RunsSummary({ runs }: { runs: PayrollRun[] }) {
  const latest = runs[0];
  const open = runs.filter((r) => r.status !== 'Finalized').length;
  return (
    <div className="mb-5 grid grid-cols-2 gap-4 sm:grid-cols-3">
      <div className="col-span-2 sm:col-span-1">
      <StatCard
        label="Latest net pay"
        value={formatCurrency(latest.totalNet)}
        icon={Wallet}
        tone="violet"
        hint={latest.label || monthLabel(latest.year, latest.month)}
      />
      </div>
      <StatCard label="People in the latest run" value={latest.employees} icon={Users} tone="blue" hint={latest.label || monthLabel(latest.year, latest.month)} />
      <StatCard label="Not finalized yet" value={open} icon={FilePen} tone={open ? 'amber' : 'green'} hint="Finalize to release payslips" />
    </div>
  );
}

export function PayrollRunsPage() {
  const navigate = useNavigate();
  const [runOpen, setRunOpen] = useState(false);
  const runs = useQuery({ queryKey: qk.payrollRuns, queryFn: payrollApi.runs });

  return (
    <>
      <PageHeader
        title="Payroll"
        description="Process payroll (monthly, or weekly and bi-weekly for people paid that way) with income tax and EOBI, then finalize to release payslips."
        actions={
          <>
            <Button variant="secondary" icon={<Settings2 className="size-4" />} onClick={() => navigate('/payroll/settings')}>
              Settings
            </Button>
            <Button variant="secondary" icon={<BookOpen className="size-4" />} onClick={() => navigate('/payroll/accounting')}>
              Accounting
            </Button>
            <Button variant="secondary" icon={<TrendingUp className="size-4" />} onClick={() => navigate('/payroll/changes')}>
              Pay changes
            </Button>
            <Button variant="secondary" icon={<PiggyBank className="size-4" />} onClick={() => navigate('/payroll/funds')}>
              Funds and tax
            </Button>
            <Button variant="secondary" icon={<Coins className="size-4" />} onClick={() => navigate('/payroll/adjustments')}>
              Pay adjustments
            </Button>
            <Button icon={<Play className="size-4" />} onClick={() => setRunOpen(true)}>
              Run payroll
            </Button>
          </>
        }
      />
      {!!runs.data?.length && <RunsSummary runs={runs.data} />}
      <Card>
        {runs.isLoading ? (
          <SkeletonRows rows={5} cols={6} />
        ) : runs.isError ? (
          <ErrorState error={runs.error} onRetry={() => runs.refetch()} />
        ) : !runs.data?.length ? (
          <EmptyState
            icon={Wallet}
            title="No payroll runs yet"
            description="Make sure employees have salaries set, then run payroll for a month."
            action={
              <Button icon={<Play className="size-4" />} onClick={() => setRunOpen(true)}>
                Run payroll
              </Button>
            }
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Period</Th>
                <Th>Status</Th>
                <Th>Tax year</Th>
                <Th className="text-end">Employees</Th>
                <Th className="text-end">Gross</Th>
                <Th className="text-end">Deductions</Th>
                <Th className="text-end">Net pay</Th>
                <Th>Processed</Th>
                <Th>Added / modified</Th>
              </tr>
            </THead>
            <TBody>
              {runs.data.map((r) => (
                <Tr key={r.id} onClick={() => navigate(`/payroll/runs/${r.id}`)}>
                  <Td className="relative font-medium text-zinc-900 dark:text-zinc-100">
                    <span className={cn('absolute inset-y-2 start-0 w-1 rounded-e-full', statusBar[r.status] ?? 'bg-indigo-400')} aria-hidden="true" />
                    <span className="flex items-center gap-3">
                      <span className="flex size-10 shrink-0 flex-col items-center justify-center rounded-xl bg-gradient-to-br from-indigo-50 to-fuchsia-50 leading-none ring-1 ring-indigo-100 dark:from-indigo-500/15 dark:to-fuchsia-500/10 dark:ring-indigo-400/15">
                        <span className="text-[10px] font-semibold text-indigo-600 uppercase dark:text-indigo-300">{monthName(r.month).slice(0, 3)}</span>
                        <span className="mt-0.5 text-[11px] font-semibold text-zinc-700 tabular-nums dark:text-zinc-200">{r.year}</span>
                      </span>
                      <span>
                        {r.label || monthLabel(r.year, r.month)}
                        {r.frequency !== 'Monthly' && (
                          <Badge tone="blue" className="ms-2">
                            {tx(FREQUENCY_LABELS[r.frequency])}
                          </Badge>
                        )}
                      </span>
                    </span>
                  </Td>
                  <Td>
                    <StatusBadge status={r.status} />
                  </Td>
                  <Td>{r.taxYearCode}</Td>
                  <Td className="text-end tabular-nums">{r.employees}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(r.totalGross)}</Td>
                  <Td className="text-end text-rose-600/90 tabular-nums dark:text-rose-400/90">{formatCurrency(r.totalDeductions)}</Td>
                  <Td className="text-end font-semibold text-indigo-700 tabular-nums dark:text-indigo-300">{formatCurrency(r.totalNet)}</Td>
                  <Td>{formatDateTimeUtc(r.processedAtUtc)}</Td>
                  <Td>{r.stamps ? <RecordStampCell stamps={r.stamps} /> : '—'}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {runOpen && <RunPayrollModal onClose={() => setRunOpen(false)} />}
    </>
  );
}

export const FREQUENCY_LABELS: Record<PayFrequency, string> = {
  Monthly: 'Monthly',
  SemiMonthly: 'Twice a month',
  BiWeekly: 'Every two weeks',
  Weekly: 'Weekly',
};

export function RunPayrollModal({
  onClose,
  initial,
  onDone,
}: {
  onClose: () => void;
  /** Reprocess one existing run; frequency and periodStart pick a shorter period. */
  initial?: { year: number; month: number; frequency?: PayFrequency; periodStart?: string };
  onDone?: (d: PayrollRunDetail) => void;
}) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const now = new Date();
  const [year, setYear] = useState(initial?.year ?? now.getFullYear());
  const [month, setMonth] = useState(initial?.month ?? now.getMonth() + 1);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PayrollRunDetail | null>(null);

  const periods = useQuery({ queryKey: ['payroll', 'periods', year, month], queryFn: () => payrollApi.periods(year, month), enabled: !initial });
  const shorter = (periods.data ?? []).filter((p) => p.frequency !== 'Monthly');
  const [picked, setPicked] = useState<PayPeriod | null>(null);

  const process = useMutation({
    mutationFn: (period: PayPeriod | null) => {
      if (initial?.frequency && initial.frequency !== 'Monthly' && initial.periodStart) return payrollApi.processPeriod(initial.frequency, initial.periodStart);
      return period && period.frequency !== 'Monthly' ? payrollApi.processPeriod(period.frequency, period.start) : payrollApi.process(year, month);
    },
    onSuccess: (d) => {
      qc.invalidateQueries({ queryKey: qk.payroll });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      qc.setQueryData(qk.payrollRun(d.run.id), d);
      toast.success(`Payroll processed for ${d.run.label || monthLabel(d.run.year, d.run.month)}`, `${d.payslips.length} payslips`);
      if (onDone) onDone(d);
      if (d.warnings.length) setResult(d);
      else {
        onClose();
        if (!onDone) navigate(`/payroll/runs/${d.run.id}`);
      }
    },
    onError: (err) => setError(errorMessage(err)),
  });

  if (result) {
    return (
      <Modal
        open
        onClose={onClose}
        title="Processed with warnings"
        description={`${result.run.label || monthLabel(result.run.year, result.run.month)} · ${result.payslips.length} payslips`}
        footer={
          <Button
            onClick={() => {
              onClose();
              if (!onDone) navigate(`/payroll/runs/${result.run.id}`);
            }}
          >
            {onDone ? 'Close' : 'Open payroll run'}
          </Button>
        }
      >
        <WarningsPanel warnings={result.warnings} />
      </Modal>
    );
  }

  const years = [now.getFullYear() + 1, now.getFullYear(), now.getFullYear() - 1, now.getFullYear() - 2];
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={initial ? 'Reprocess payroll' : 'Run payroll'}
      description="Calculates every current employee's payslip. You can reprocess as often as needed until you finalize."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={process.isPending && !picked} icon={<Play className="size-4" />} onClick={() => (setError(null), setPicked(null), process.mutate(null))}>
            {initial ? 'Reprocess' : shorter.length ? 'Process monthly payroll' : 'Process payroll'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <div className="grid grid-cols-2 gap-3">
          <Field label="Month">
            <Select value={month} disabled={!!initial} onChange={(e) => setMonth(Number(e.target.value))}>
              {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                <option key={m} value={m}>
                  {monthName(m)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Year">
            <Select value={year} disabled={!!initial} onChange={(e) => setYear(Number(e.target.value))}>
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        {shorter.length > 0 && (
          <div>
            <p className="mb-2 text-sm font-medium text-zinc-900 dark:text-zinc-100">{tx('Shorter pay periods ending this month')}</p>
            <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-700">
              {shorter.map((p) => (
                <li key={`${p.frequency}-${p.start}`} className="flex items-center gap-3 px-3 py-2.5">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm text-zinc-900 dark:text-zinc-100">{p.label}</span>
                    <span className="block text-xs text-zinc-500 dark:text-zinc-400">
                      {tx(FREQUENCY_LABELS[p.frequency])} · {p.people} {tx(p.people === 1 ? 'person' : 'people')}
                    </span>
                  </span>
                  {p.status && <StatusBadge status={p.status} />}
                  {p.status === 'Finalized' ? (
                    <Button size="sm" variant="ghost" onClick={() => (onClose(), navigate(`/payroll/runs/${p.runId}`))}>
                      Open
                    </Button>
                  ) : (
                    <Button
                      size="sm"
                      variant="secondary"
                      loading={process.isPending && picked === p}
                      onClick={() => (setError(null), setPicked(p), process.mutate(p))}
                    >
                      {p.runId ? 'Reprocess' : 'Process'}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </Modal>
  );
}
