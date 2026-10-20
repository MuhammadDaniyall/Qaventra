import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { Banknote, Lock, MinusCircle, RefreshCw, Search, Users, Wallet } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { qk } from '../../api/keys';
import {
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Input,
  PageHeader,
  PageSpinner,
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
import { formatCurrency, formatDateTimeUtc, formatNumber, monthLabel } from '../../lib/format';
import { RunPayrollModal } from './PayrollRunsPage';
import { WarningsPanel } from './WarningsPanel';

export function PayrollRunPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [reprocessOpen, setReprocessOpen] = useState(false);
  const [finalizeOpen, setFinalizeOpen] = useState(false);
  const [warnings, setWarnings] = useState<string[]>([]);
  const detail = useQuery({ queryKey: qk.payrollRun(id), queryFn: () => payrollApi.run(id) });

  const finalize = useMutation({
    mutationFn: () => payrollApi.finalize(id),
    onSuccess: () => {
      toast.success('Payroll finalized', 'Employees can now see their payslips.');
      qc.invalidateQueries({ queryKey: qk.payroll });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      setFinalizeOpen(false);
    },
    onError: (err) => toast.error(err),
  });

  const slips = useMemo(() => {
    const s = search.trim().toLowerCase();
    const list = detail.data?.payslips ?? [];
    return s
      ? list.filter((p) => p.employeeName.toLowerCase().includes(s) || p.employeeCode.toLowerCase().includes(s) || (p.department ?? '').toLowerCase().includes(s))
      : list;
  }, [detail.data, search]);

  if (detail.isLoading) return <PageSpinner />;
  if (detail.isError || !detail.data) {
    return (
      <>
        <PageHeader title="Payroll run" back={{ to: '/payroll', label: 'Payroll' }} />
        <Card>
          <ErrorState error={detail.error} onRetry={() => detail.refetch()} />
        </Card>
      </>
    );
  }

  const { run } = detail.data;
  const draft = run.status === 'Draft';
  const allWarnings = warnings.length ? warnings : detail.data.warnings;

  return (
    <>
      <PageHeader
        back={{ to: '/payroll', label: 'Payroll' }}
        title={
          <span className="flex flex-wrap items-center gap-3">
            {monthLabel(run.year, run.month)} <StatusBadge status={run.status} />
          </span>
        }
        description={`Tax year ${run.taxYearCode} · processed ${formatDateTimeUtc(run.processedAtUtc)}${
          run.finalizedAtUtc ? ` · finalized ${formatDateTimeUtc(run.finalizedAtUtc)}` : ''
        }`}
        actions={
          draft && (
            <>
              <Button variant="secondary" icon={<RefreshCw className="size-4" />} onClick={() => setReprocessOpen(true)}>
                Reprocess
              </Button>
              <Button icon={<Lock className="size-4" />} onClick={() => setFinalizeOpen(true)}>
                Finalize
              </Button>
            </>
          )
        }
      />

      {allWarnings.length > 0 && (
        <div className="mb-6">
          <WarningsPanel warnings={allWarnings} />
        </div>
      )}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard label="Employees" value={run.employees} icon={Users} />
        <StatCard label="Total gross" value={formatCurrency(run.totalGross)} icon={Banknote} tone="blue" />
        <StatCard label="Total deductions" value={formatCurrency(run.totalDeductions)} icon={MinusCircle} tone="red" />
        <StatCard label="Total net pay" value={formatCurrency(run.totalNet)} icon={Wallet} tone="green" />
      </div>

      <Card className="mt-6">
        <div className="flex items-center justify-between gap-3 border-b border-zinc-100 p-4 dark:border-zinc-800">
          <h2 className="text-sm font-semibold">Payslips</h2>
          <div className="relative w-full max-w-xs">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
            <Input aria-label="Search payslips" placeholder="Search employee" className="pl-9" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
        </div>
        {slips.length === 0 ? (
          <EmptyState icon={Wallet} title={search ? 'No matching payslips' : 'No payslips in this run'} description={search ? undefined : 'Employees need a salary to be included.'} />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Department</Th>
                <Th className="text-right">Paid days</Th>
                <Th className="text-right">Gross</Th>
                <Th className="text-right">Income tax</Th>
                <Th className="text-right">EOBI</Th>
                <Th className="text-right">Other deductions</Th>
                <Th className="text-right">Net pay</Th>
              </tr>
            </THead>
            <TBody>
              {slips.map((p) => (
                <Tr key={p.id} onClick={() => navigate(`/payslips/${p.id}`)}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{p.employeeName}</p>
                    <p className="font-mono text-xs text-zinc-500">{p.employeeCode}</p>
                  </Td>
                  <Td>{p.department ?? '—'}</Td>
                  <Td className="text-right tabular-nums">{formatNumber(p.paidDays)}</Td>
                  <Td className="text-right tabular-nums">{formatCurrency(p.grossEarnings)}</Td>
                  <Td className="text-right tabular-nums">{formatCurrency(p.incomeTax)}</Td>
                  <Td className="text-right tabular-nums">{formatCurrency(p.eobiEmployee)}</Td>
                  <Td className="text-right tabular-nums">{formatCurrency(p.otherDeductions)}</Td>
                  <Td className="text-right font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(p.netPay)}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {reprocessOpen && (
        <RunPayrollModal
          initial={{ year: run.year, month: run.month }}
          onClose={() => setReprocessOpen(false)}
          onDone={(d) => setWarnings(d.warnings)}
        />
      )}
      <ConfirmDialog
        open={finalizeOpen}
        title={`Finalize ${monthLabel(run.year, run.month)}?`}
        message="Finalizing locks this payroll run. It can no longer be reprocessed, and employees will be able to see their payslips."
        confirmLabel="Finalize payroll"
        loading={finalize.isPending}
        onConfirm={() => finalize.mutate()}
        onClose={() => setFinalizeOpen(false)}
      />
    </>
  );
}
