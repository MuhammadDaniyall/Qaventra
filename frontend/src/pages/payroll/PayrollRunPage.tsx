import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { Banknote, Check, Lock, MinusCircle, RefreshCw, Search, Send, Undo2, Users, Wallet } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isCompanyAdmin } from '../../auth/roles';
import { ExportButton } from '../../components/excel/ExcelButtons';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  Field,
  Input,
  Modal,
  PageHeader,
  PageSpinner,
  StatCard,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Textarea,
  Tr,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDateTimeUtc, formatNumber, monthLabel } from '../../lib/format';
import { RunPayrollModal } from './PayrollRunsPage';
import { RunStepper, RunVariance } from './RunProgress';
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
  const [sendingBack, setSendingBack] = useState(false);
  const [note, setNote] = useState('');
  const detail = useQuery({ queryKey: qk.payrollRun(id), queryFn: () => payrollApi.run(id) });
  const settings = useQuery({ queryKey: qk.payrollSettings, queryFn: payrollApi.settings });
  const admin = isCompanyAdmin(useUser());
  const refresh = () => {
    qc.invalidateQueries({ queryKey: qk.payroll });
    qc.invalidateQueries({ queryKey: qk.dashboard });
  };
  const submit = useMutation({
    mutationFn: () => payrollApi.submit(id),
    onSuccess: () => {
      toast.success('Sent for approval');
      refresh();
    },
    onError: (err) => toast.error(err),
  });
  const decide = useMutation({
    mutationFn: (approve: boolean) => payrollApi.decide(id, approve, note.trim() || null),
    onSuccess: (_, approve) => {
      toast.success(approve ? 'Payroll approved' : 'Sent back to HR');
      setSendingBack(false);
      setNote('');
      refresh();
    },
    onError: (err) => toast.error(err),
  });
  const undisburse = useMutation({
    mutationFn: () => payrollApi.undisburse(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.payrollRun(id) }),
    onError: (e) => toast.error(e),
  });
  const disburse = useMutation({
    mutationFn: () => payrollApi.disburse(id, 'Bank transfer'),
    onSuccess: () => {
      toast.success('Marked as paid');
      refresh();
    },
    onError: (err) => toast.error(err),
  });

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
      ? list.filter(
          (p) => p.employeeName.toLowerCase().includes(s) || p.employeeCode.toLowerCase().includes(s) || (p.department ?? '').toLowerCase().includes(s),
        )
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
  const needsApproval = settings.data?.requireApproval ?? false;
  const canFinalize = needsApproval ? run.status === 'Approved' : draft || run.status === 'Approved';
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
          run.approvedByName ? ` · approved by ${run.approvedByName}` : ''
        }${run.finalizedAtUtc ? ` · finalized ${formatDateTimeUtc(run.finalizedAtUtc)}` : ''}${
          run.disbursedAtUtc ? ` · paid ${formatDateTimeUtc(run.disbursedAtUtc)} (${run.disbursedVia})` : ''
        }`}
        actions={
          <>
            <ExportButton
              path={`/exports/payroll/${run.id}`}
              fileName={`payroll-${run.year}-${String(run.month).padStart(2, '0')}${draft ? '-draft' : ''}.xlsx`}
              label="Export to Excel"
            />
            {run.status !== 'Finalized' && (
              <Button variant="secondary" icon={<RefreshCw className="size-4" />} onClick={() => setReprocessOpen(true)}>
                Reprocess
              </Button>
            )}
            {draft && needsApproval && (
              <Button icon={<Send className="size-4" />} loading={submit.isPending} onClick={() => submit.mutate()}>
                Send for approval
              </Button>
            )}
            {run.status === 'Submitted' && admin && (
              <>
                <Button variant="secondary" icon={<Undo2 className="size-4" />} onClick={() => setSendingBack(true)}>
                  Send back
                </Button>
                <Button variant="success" icon={<Check className="size-4" />} loading={decide.isPending} onClick={() => decide.mutate(true)}>
                  Approve
                </Button>
              </>
            )}
            {canFinalize && (
              <Button icon={<Lock className="size-4" />} onClick={() => setFinalizeOpen(true)}>
                Finalize
              </Button>
            )}
            {run.status === 'Finalized' && run.disbursedAtUtc && (
              <Button variant="ghost" loading={undisburse.isPending} onClick={() => undisburse.mutate()}>
                Not paid yet
              </Button>
            )}
            {run.status === 'Finalized' && !run.disbursedAtUtc && (
              <Button variant="secondary" icon={<Banknote className="size-4" />} loading={disburse.isPending} onClick={() => disburse.mutate()}>
                Mark as paid
              </Button>
            )}
          </>
        }
      />

      <RunStepper run={run} needsApproval={needsApproval} />

      {run.status === 'Submitted' && (
        <div className="mb-4 rounded-lg bg-amber-50 px-4 py-2.5 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
          Waiting for the company admin to approve.
        </div>
      )}
      {run.status === 'Draft' && run.approvalNote && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-2.5 text-sm text-red-800 dark:bg-red-500/10 dark:text-red-200">Sent back: {run.approvalNote}</div>
      )}
      {run.held > 0 && (
        <div className="mb-4 rounded-lg bg-amber-50 px-4 py-2.5 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
          {run.held} salar{run.held === 1 ? 'y is' : 'ies are'} on hold and left out of the bank advice until released (Pay changes → Holds).
        </div>
      )}
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

      <div className="mt-6">{run.status !== 'Finalized' && <RunVariance detail={detail.data} />}</div>

      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-zinc-100 p-4 dark:border-zinc-800">
          <h2 className="text-sm font-semibold">Payslips</h2>
          <div className="relative w-full max-w-xs">
            <Search className="pointer-events-none absolute top-1/2 start-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400" />
            <Input aria-label="Search payslips" placeholder="Search employee" className="ps-9" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
        </div>
        {slips.length === 0 ? (
          <EmptyState
            icon={Wallet}
            title={search ? 'No matching payslips' : 'No payslips in this run'}
            description={search ? undefined : 'Employees need a salary to be included.'}
          />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Employee</Th>
                <Th>Department</Th>
                <Th className="text-end">Paid days</Th>
                <Th className="text-end">Gross</Th>
                <Th className="text-end">Income tax</Th>
                <Th className="text-end">EOBI</Th>
                <Th className="text-end">Other deductions</Th>
                <Th className="text-end">Net pay</Th>
              </tr>
            </THead>
            <TBody>
              {slips.map((p) => (
                <Tr key={p.id} onClick={() => navigate(`/payslips/${p.id}`)}>
                  <Td>
                    <p className="font-medium text-zinc-900 dark:text-zinc-100">{p.employeeName}</p>
                    <p className="font-mono text-xs text-zinc-500 dark:text-zinc-400">{p.employeeCode}</p>
                    {p.heldReason && <Badge tone="amber">On hold: {p.heldReason}</Badge>}
                  </Td>
                  <Td>{p.department ?? '—'}</Td>
                  <Td className="text-end tabular-nums">{formatNumber(p.paidDays)}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(p.grossEarnings)}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(p.incomeTax)}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(p.eobiEmployee)}</Td>
                  <Td className="text-end tabular-nums">{formatCurrency(p.otherDeductions)}</Td>
                  <Td className="text-end font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(p.netPay)}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {reprocessOpen && (
        <RunPayrollModal initial={{ year: run.year, month: run.month }} onClose={() => setReprocessOpen(false)} onDone={(d) => setWarnings(d.warnings)} />
      )}
      {sendingBack && (
        <Modal
          open
          onClose={() => setSendingBack(false)}
          size="sm"
          title="Send the payroll back"
          footer={
            <Button disabled={!note.trim()} loading={decide.isPending} onClick={() => decide.mutate(false)}>
              Send back
            </Button>
          }
        >
          <Field label="What should HR check?" required>
            <Textarea rows={3} value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
          </Field>
        </Modal>
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
