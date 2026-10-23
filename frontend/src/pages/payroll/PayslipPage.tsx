import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { Printer } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Badge, Button, Card, ErrorState, PageHeader, PageSpinner } from '../../components/ui';
import { formatCurrency, formatNumber, monthLabel } from '../../lib/format';
import { useDocumentTitle } from '../../lib/hooks';
import type { Payslip } from '../../types';

export function PayslipPage() {
  const { id = '' } = useParams();
  const user = useUser();
  const payslip = useQuery({ queryKey: qk.payslip(id), queryFn: () => payrollApi.payslip(id) });
  const back = isHr(user) ? { to: '/payroll', label: 'Payroll' } : { to: '/me/payslips', label: 'My payslips' };

  if (payslip.isLoading) return <PageSpinner />;
  if (payslip.isError || !payslip.data) {
    return (
      <>
        <PageHeader title="Payslip" back={back} />
        <Card>
          <ErrorState error={payslip.error} onRetry={() => payslip.refetch()} />
        </Card>
      </>
    );
  }
  return <PayslipView p={payslip.data} back={back} />;
}

function PayslipView({ p, back }: { p: Payslip; back: { to: string; label: string } }) {
  useDocumentTitle(`Payslip ${p.employeeCode} ${monthLabel(p.year, p.month)}`);
  const earnings = p.lines.filter((l) => l.kind === 'Earning');
  // Lines already include basic salary, income tax and EOBI (employee) as separate entries.
  const deductions = p.lines.filter((l) => l.kind === 'Deduction');
  const totalDeductions = deductions.reduce((t, d) => t + d.amount, 0);

  const details: Array<[string, string | null]> = [
    ['Employee', p.employeeName],
    ['Employee code', p.employeeCode],
    ['Department', p.department],
    ['Designation', p.designation],
    ['CNIC', p.cnic],
    ['Bank', p.bankName],
    ['Account', p.bankAccount],
  ];

  return (
    <>
      <div className="no-print">
        <PageHeader
          back={back}
          title={`Payslip · ${monthLabel(p.year, p.month)}`}
          description={p.employeeName}
          actions={
            <Button icon={<Printer className="size-4" />} onClick={() => window.print()}>
              Print
            </Button>
          }
        />
      </div>

      <article className="print-area mx-auto max-w-3xl rounded-xl border border-zinc-200 bg-white p-6 shadow-sm sm:p-10 dark:border-zinc-800 dark:bg-zinc-900">
        <header className="flex flex-wrap items-start justify-between gap-4 border-b border-zinc-200 pb-6 dark:border-zinc-800">
          <div>
            <p className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-white">{p.companyName}</p>
            <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">Salary slip for {monthLabel(p.year, p.month)}</p>
          </div>
          {p.runStatus === 'Draft' && (
            <Badge tone="amber" dot>
              Draft — not final
            </Badge>
          )}
        </header>

        <section className="grid gap-x-8 gap-y-2 border-b border-zinc-200 py-6 text-sm sm:grid-cols-2 dark:border-zinc-800">
          {details.map(([k, v]) => (
            <div key={k} className="flex justify-between gap-4 sm:block">
              <span className="text-zinc-500 sm:inline-block sm:w-32 dark:text-zinc-400">{k}</span>
              <span className="font-medium text-zinc-900 dark:text-zinc-100">{v || '—'}</span>
            </div>
          ))}
          <div className="flex justify-between gap-4 sm:block">
            <span className="text-zinc-500 sm:inline-block sm:w-32 dark:text-zinc-400">Paid days</span>
            <span className="font-medium text-zinc-900 dark:text-zinc-100">
              {formatNumber(p.paidDays)} of {p.daysInMonth}
              {p.unpaidDays > 0 && <span className="text-zinc-500"> ({formatNumber(p.unpaidDays)} unpaid)</span>}
            </span>
          </div>
        </section>

        <section className="grid gap-8 py-6 sm:grid-cols-2">
          <LineTable title="Earnings" lines={earnings} total={p.grossEarnings} totalLabel="Gross earnings" />
          <LineTable title="Deductions" lines={deductions} total={totalDeductions} totalLabel="Total deductions" />
        </section>

        <section className="flex flex-wrap items-end justify-between gap-4 rounded-xl bg-zinc-50 p-5 dark:bg-zinc-800/50">
          <div>
            <p className="text-xs font-medium tracking-wide text-zinc-500 uppercase dark:text-zinc-400">Net pay</p>
            <p className="mt-1 text-4xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">{formatCurrency(p.netPay)}</p>
          </div>
          <div className="text-right text-xs text-zinc-500 dark:text-zinc-400">
            <p>Taxable income: {formatCurrency(p.taxableIncome)}</p>
            {p.eobiEmployer > 0 && <p>Employer EOBI contribution: {formatCurrency(p.eobiEmployer)}</p>}
          </div>
        </section>

        <p className="mt-8 text-center text-xs text-zinc-400">This is a computer-generated payslip and does not require a signature.</p>
      </article>
    </>
  );
}

function LineTable({ title, lines, total, totalLabel }: { title: string; lines: Array<{ name: string; amount: number }>; total: number; totalLabel: string }) {
  return (
    <div>
      <h3 className="mb-2 text-xs font-semibold tracking-wide text-zinc-500 uppercase dark:text-zinc-400">{title}</h3>
      <table className="w-full text-sm">
        <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {lines.length === 0 && (
            <tr>
              <td className="py-2 text-zinc-400" colSpan={2}>
                None
              </td>
            </tr>
          )}
          {lines.map((l, i) => (
            <tr key={`${l.name}-${i}`}>
              <td className="py-2 text-zinc-700 dark:text-zinc-300">{l.name}</td>
              <td className="py-2 text-right text-zinc-900 tabular-nums dark:text-zinc-100">{formatCurrency(l.amount)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="border-t-2 border-zinc-200 dark:border-zinc-700">
            <td className="pt-2 font-semibold text-zinc-900 dark:text-white">{totalLabel}</td>
            <td className="pt-2 text-right font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(total)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
