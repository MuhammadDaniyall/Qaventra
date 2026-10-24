import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Check, TrendingDown, TrendingUp, UserMinus, UserPlus } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { qk } from '../../api/keys';
import { Card, CardHeader } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatCurrency, monthLabel } from '../../lib/format';
import type { PayrollRun, PayrollRunDetail } from '../../types';

/** Draft → (approval) → finalized → paid, with the current step highlighted. */
export function RunStepper({ run, needsApproval }: { run: PayrollRun; needsApproval: boolean }) {
  const steps = [
    { label: 'Processed', hint: 'Review payslips' },
    ...(needsApproval ? [{ label: 'Approved', hint: 'Company admin signs off' }] : []),
    { label: 'Finalized', hint: 'Payslips go to employees' },
    { label: 'Paid', hint: 'Salaries transferred' },
  ];
  const reached =
    run.status === 'Finalized'
      ? run.disbursedAtUtc
        ? steps.length
        : steps.length - 1
      : run.status === 'Approved'
        ? 2
        : 1;
  const current = Math.min(reached, steps.length - 1);

  return (
    <ol className="mb-6 grid gap-3 sm:flex sm:items-center sm:gap-0" aria-label="Payroll progress">
      {steps.map((s, i) => {
        const done = i < reached;
        const active = i === current && !done;
        return (
          <li key={s.label} className="flex items-center gap-3 sm:flex-1">
            <span
              className={cn(
                'flex size-8 shrink-0 items-center justify-center rounded-full border text-sm font-semibold',
                done && 'border-emerald-500 bg-emerald-500 text-white',
                active && 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10 dark:text-accent-300',
                !done && !active && 'border-zinc-200 text-zinc-500 dark:text-zinc-400 dark:border-zinc-700',
              )}
              aria-current={active ? 'step' : undefined}
            >
              {done ? <Check className="size-4" /> : i + 1}
            </span>
            <span className="min-w-0">
              <span className={cn('block text-sm font-medium', done || active ? 'text-zinc-900 dark:text-white' : 'text-zinc-500 dark:text-zinc-400')}>{s.label}</span>
              <span className="block text-xs text-zinc-500 dark:text-zinc-400">{s.hint}</span>
            </span>
            {i < steps.length - 1 && <span className="mx-4 hidden h-px flex-1 bg-zinc-200 sm:block dark:bg-zinc-700" aria-hidden="true" />}
          </li>
        );
      })}
    </ol>
  );
}

/** Net-pay changes against the previous month: big moves, joiners and leavers. HR checks these before finalizing. */
export function RunVariance({ detail }: { detail: PayrollRunDetail }) {
  const { run } = detail;
  const runs = useQuery({ queryKey: qk.payrollRuns, queryFn: payrollApi.runs });
  const prevRun = (runs.data ?? [])
    .filter((r) => r.year * 12 + r.month < run.year * 12 + run.month)
    .sort((a, b) => b.year * 12 + b.month - (a.year * 12 + a.month))[0];
  const prev = useQuery({ queryKey: qk.payrollRun(prevRun?.id ?? ''), queryFn: () => payrollApi.run(prevRun!.id), enabled: !!prevRun });

  if (!prevRun || !prev.data) return null;

  const before = new Map(prev.data.payslips.map((p) => [p.employeeCode, p]));
  const now = new Set(detail.payslips.map((p) => p.employeeCode));
  const moved = detail.payslips
    .map((p) => {
      const b = before.get(p.employeeCode);
      if (!b || !b.netPay) return null;
      const diff = p.netPay - b.netPay;
      return Math.abs(diff) / b.netPay >= 0.1 ? { id: p.id, name: p.employeeName, diff, pct: Math.round((diff / b.netPay) * 100) } : null;
    })
    .filter((x): x is NonNullable<typeof x> => !!x)
    .sort((a, b) => Math.abs(b.pct) - Math.abs(a.pct));
  const joined = detail.payslips.filter((p) => !before.has(p.employeeCode));
  const left = prev.data.payslips.filter((p) => !now.has(p.employeeCode));
  const netDiff = run.totalNet - prevRun.totalNet;

  return (
    <Card className="mb-6">
      <CardHeader
        title={`Compared with ${monthLabel(prevRun.year, prevRun.month)}`}
        description={`Net pay ${netDiff >= 0 ? 'up' : 'down'} ${formatCurrency(Math.abs(netDiff))}. Changes of 10% or more are listed.`}
      />
      {!moved.length && !joined.length && !left.length ? (
        <p className="px-5 py-4 text-sm text-zinc-500 dark:text-zinc-400">No big changes. Every employee is within 10% of last month.</p>
      ) : (
        <ul className="divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {moved.slice(0, 8).map((m) => (
            <li key={m.id}>
              <Link to={`/payslips/${m.id}`} className="flex items-center gap-3 px-5 py-2.5 hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                {m.diff > 0 ? <TrendingUp className="size-4 text-emerald-500" /> : <TrendingDown className="size-4 text-red-500" />}
                <span className="flex-1 truncate font-medium text-zinc-900 dark:text-zinc-100">{m.name}</span>
                <span className={cn('tabular-nums', m.diff > 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400')}>
                  {m.diff > 0 ? '+' : '−'}
                  {formatCurrency(Math.abs(m.diff))} ({m.pct > 0 ? '+' : ''}
                  {m.pct}%)
                </span>
              </Link>
            </li>
          ))}
          {joined.map((p) => (
            <li key={p.id} className="flex items-center gap-3 px-5 py-2.5">
              <UserPlus className="size-4 text-sky-500" />
              <span className="flex-1 truncate font-medium text-zinc-900 dark:text-zinc-100">{p.employeeName}</span>
              <span className="text-zinc-500 dark:text-zinc-400">New this month</span>
            </li>
          ))}
          {left.map((p) => (
            <li key={p.id} className="flex items-center gap-3 px-5 py-2.5">
              <UserMinus className="size-4 text-amber-500" />
              <span className="flex-1 truncate font-medium text-zinc-900 dark:text-zinc-100">{p.employeeName}</span>
              <span className="text-zinc-500 dark:text-zinc-400">Not in this run</span>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
