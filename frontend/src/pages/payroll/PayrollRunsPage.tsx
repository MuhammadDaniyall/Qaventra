import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Play, Coins, Settings2, TrendingUp, PiggyBank, Wallet } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import {
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
import type { PayrollRunDetail } from '../../types';
import { WarningsPanel } from './WarningsPanel';

export function PayrollRunsPage() {
  const navigate = useNavigate();
  const [runOpen, setRunOpen] = useState(false);
  const runs = useQuery({ queryKey: qk.payrollRuns, queryFn: payrollApi.runs });

  return (
    <>
      <PageHeader
        title="Payroll"
        description="Process monthly payroll with income tax and EOBI, then finalize to release payslips."
        actions={
          <>
            <Button variant="secondary" icon={<Settings2 className="size-4" />} onClick={() => navigate('/payroll/settings')}>
              Settings
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
                <Th>Month</Th>
                <Th>Status</Th>
                <Th>Tax year</Th>
                <Th className="text-end">Employees</Th>
                <Th className="text-end">Gross</Th>
                <Th className="text-end">Deductions</Th>
                <Th className="text-end">Net pay</Th>
                <Th>Processed</Th>
              </tr>
            </THead>
            <TBody>
              {runs.data.map((r) => (
                <Tr key={r.id} onClick={() => navigate(`/payroll/runs/${r.id}`)}>
                  <Td className="font-medium text-zinc-900 dark:text-zinc-100">{monthLabel(r.year, r.month)}</Td>
                  <Td>
                    <StatusBadge status={r.status} />
                  </Td>
                  <Td>{r.taxYearCode}</Td>
                  <Td className="text-end tabular-nums">{r.employees}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(r.totalGross)}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(r.totalDeductions)}</Td>
                  <Td className="text-end font-medium text-zinc-900 tabular-nums dark:text-zinc-100">{formatCurrency(r.totalNet)}</Td>
                  <Td>{formatDateTimeUtc(r.processedAtUtc)}</Td>
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

export function RunPayrollModal({
  onClose,
  initial,
  onDone,
}: {
  onClose: () => void;
  initial?: { year: number; month: number };
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

  const process = useMutation({
    mutationFn: () => payrollApi.process(year, month),
    onSuccess: (d) => {
      qc.invalidateQueries({ queryKey: qk.payroll });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      qc.setQueryData(qk.payrollRun(d.run.id), d);
      toast.success(`Payroll processed for ${monthLabel(d.run.year, d.run.month)}`, `${d.payslips.length} payslips`);
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
        description={`${monthLabel(result.run.year, result.run.month)} · ${result.payslips.length} payslips`}
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
          <Button loading={process.isPending} icon={<Play className="size-4" />} onClick={() => (setError(null), process.mutate())}>
            {initial ? 'Reprocess' : 'Process payroll'}
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
      </div>
    </Modal>
  );
}
