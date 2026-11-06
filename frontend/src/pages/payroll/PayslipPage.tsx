import { useMutation, useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { Printer, Sparkles } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { tx, useT } from '../../lib/i18n';
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
  const { lang } = useT();
  const explain = useMutation({ mutationFn: () => payrollApi.explainPayslip(p.id, lang) });
  useDocumentTitle(`Payslip ${p.employeeCode} ${(p.label || monthLabel(p.year, p.month))}`);
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
          title={`Payslip · ${(p.label || monthLabel(p.year, p.month))}`}
          description={p.employeeName}
          actions={
            <>
              <Button variant="secondary" icon={<Sparkles className="size-4" />} loading={explain.isPending} onClick={() => explain.mutate()}>
                Explain with AI
              </Button>
              <Button icon={<Printer className="size-4" />} onClick={() => window.print()}>
                Print
              </Button>
            </>
          }
        />
        {(explain.data || explain.error) && (
          <div className="mx-auto mb-4 max-w-3xl rounded-xl border border-indigo-200/70 bg-gradient-to-br from-indigo-50 to-fuchsia-50 p-5 text-sm dark:border-indigo-400/20 dark:from-indigo-500/10 dark:to-fuchsia-500/10">
            <p className="flex items-center gap-1.5 font-semibold text-indigo-800 dark:text-indigo-200">
              <Sparkles className="size-4" /> {tx('Your payslip, explained')}
            </p>
            {explain.error ? (
              <p className="mt-2 text-red-600 dark:text-red-400">{errorMessage(explain.error)}</p>
            ) : (
              explain.data && (
                <>
                  <p className="mt-2 leading-relaxed text-zinc-800 dark:text-zinc-200">{explain.data.summary}</p>
                  {explain.data.changes.length > 0 && (
                    <>
                      <p className="mt-3 font-medium text-zinc-900 dark:text-white">{tx('What changed since last time')}</p>
                      <ul className="mt-1 list-disc space-y-0.5 ps-5 text-zinc-700 dark:text-zinc-300">
                        {explain.data.changes.map((c, i) => <li key={i}>{c}</li>)}
                      </ul>
                    </>
                  )}
                  <ul className="mt-3 list-disc space-y-0.5 ps-5 text-zinc-700 dark:text-zinc-300">
                    {explain.data.points.map((c, i) => <li key={i}>{c}</li>)}
                  </ul>
                  <p className="mt-3 text-xs text-zinc-500 dark:text-zinc-400">{tx('Written by AI from the numbers above. If something looks wrong, ask HR.')}</p>
                </>
              )
            )}
          </div>
        )}
      </div>

      <article className="print-area mx-auto max-w-3xl rounded-xl border border-zinc-200 bg-white p-6 shadow-sm sm:p-10 dark:border-zinc-800 dark:bg-zinc-900">
        <header className="flex flex-wrap items-start justify-between gap-4 border-b border-zinc-200 pb-6 dark:border-zinc-800">
          <div>
            <p className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-white">{p.companyName}</p>
            <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">Salary slip for {(p.label || monthLabel(p.year, p.month))}</p>
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
              {p.unpaidDays > 0 && <span className="text-zinc-500 dark:text-zinc-400"> ({formatNumber(p.unpaidDays)} unpaid)</span>}
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
          <div className="text-end text-xs text-zinc-500 dark:text-zinc-400">
            <p>Taxable income: {formatCurrency(p.taxableIncome)}</p>
            {p.eobiEmployer > 0 && <p>Employer EOBI contribution: {formatCurrency(p.eobiEmployer)}</p>}
          </div>
        </section>

        <p className="mt-8 text-center text-xs text-zinc-500 dark:text-zinc-400">This is a computer-generated payslip and does not require a signature.</p>
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
              <td className="py-2 text-zinc-500 dark:text-zinc-400" colSpan={2}>
                None
              </td>
            </tr>
          )}
          {lines.map((l, i) => (
            <tr key={`${l.name}-${i}`}>
              <td className="py-2 text-zinc-700 dark:text-zinc-300">{l.name}</td>
              <td className="py-2 text-end text-zinc-900 tabular-nums dark:text-zinc-100">{formatCurrency(l.amount)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="border-t-2 border-zinc-200 dark:border-zinc-700">
            <td className="pt-2 font-semibold text-zinc-900 dark:text-white">{totalLabel}</td>
            <td className="pt-2 text-end font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(total)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
