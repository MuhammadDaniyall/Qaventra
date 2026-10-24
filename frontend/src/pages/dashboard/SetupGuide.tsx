import { useState } from 'react';
import { useQueries } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Check, ChevronRight, Rocket, X } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { qk } from '../../api/keys';
import { orgApi, setupApi } from '../../api/organization';
import { payrollApi } from '../../api/payroll';
import { Card, IconButton } from '../../components/ui';
import { cn } from '../../lib/cn';
import { tx, txf } from '../../lib/i18n';

const HIDE_KEY = 'hrms.setup-guide.hidden';

function readHidden() {
  try {
    return localStorage.getItem(HIDE_KEY) === '1';
  } catch {
    return false;
  }
}

/** First-run checklist for HR: the setup a company needs before attendance, leave and payroll work. */
export function SetupGuide({ className }: { className?: string }) {
  const [hidden, setHidden] = useState(readHidden);
  const year = new Date().getFullYear();
  const [departments, designations, shifts, offices, holidays, leaveTypes, employees, runs] = useQueries({
    queries: [
      { queryKey: qk.departments, queryFn: orgApi.departments },
      { queryKey: qk.designations, queryFn: orgApi.designations },
      { queryKey: qk.shifts, queryFn: setupApi.shifts },
      { queryKey: qk.offices, queryFn: setupApi.offices },
      { queryKey: qk.holidays(year), queryFn: () => setupApi.holidays(year) },
      { queryKey: qk.leaveTypes, queryFn: setupApi.leaveTypes },
      { queryKey: ['employees', 'setup-count'], queryFn: () => employeesApi.list({ pageSize: 1 }) },
      { queryKey: qk.payrollRuns, queryFn: payrollApi.runs },
    ],
  });

  const loading = [departments, designations, shifts, offices, holidays, leaveTypes, employees, runs].some((q) => q.isLoading);
  const steps = [
    { label: 'Add departments', hint: 'Production, Sales, Finance…', done: !!departments.data?.length, to: '/settings?tab=departments' },
    { label: 'Add designations', hint: 'Job titles people hold', done: !!designations.data?.length, to: '/settings?tab=designations' },
    { label: 'Set working hours', hint: 'Shifts with start, end and grace time', done: !!shifts.data?.length, to: '/settings?tab=shifts' },
    { label: 'Add office locations', hint: 'For geo-fenced check-in', done: !!offices.data?.length, to: '/settings?tab=offices' },
    { label: "Add this year's holidays", hint: 'Public holidays skip attendance', done: !!holidays.data?.length, to: '/settings?tab=holidays' },
    { label: 'Set up leave types', hint: 'Annual, casual, sick…', done: !!leaveTypes.data?.length, to: '/settings?tab=leave-types' },
    { label: 'Add your employees', hint: 'One by one or from Excel', done: (employees.data?.total ?? 0) > 0, to: '/employees' },
    { label: 'Run your first payroll', hint: 'Once salaries are entered', done: !!runs.data?.length, to: '/payroll' },
  ];
  const done = steps.filter((s) => s.done).length;

  if (hidden || loading || done === steps.length) return null;
  const pct = Math.round((done / steps.length) * 100);
  const next = steps.find((s) => !s.done);

  return (
    <Card className={cn('overflow-hidden', className)}>
      <div className="flex items-start gap-4 p-5">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-accent-50 text-accent-600 dark:bg-accent-500/10 dark:text-accent-300">
          <Rocket className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="text-base font-semibold text-zinc-900 dark:text-white">{tx('Get your company ready')}</h2>
          <p className="mt-0.5 text-sm text-zinc-500 dark:text-zinc-400">
            {txf('{done} of {total} done', { done, total: steps.length })}
            {next ? ` · ${txf('next: {step}', { step: tx(next.label) })}` : ''}
          </p>
          <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Setup progress">
            <div className="h-full rounded-full bg-accent-500 transition-[width]" style={{ width: `${pct}%` }} />
          </div>
        </div>
        <IconButton
          label="Hide the setup guide"
          onClick={() => {
            setHidden(true);
            try {
              localStorage.setItem(HIDE_KEY, '1');
            } catch {
              // ignore
            }
          }}
        >
          <X className="size-4" />
        </IconButton>
      </div>
      <ol className="grid border-t border-zinc-100 sm:grid-cols-2 lg:grid-cols-4 dark:border-zinc-800">
        {steps.map((s, i) => (
          <li key={s.label} className="border-b border-zinc-100 sm:odd:border-e lg:border-e dark:border-zinc-800">
            <Link
              to={s.to}
              className="group flex h-full items-center gap-3 px-5 py-3 transition-colors hover:bg-zinc-50 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:bg-zinc-800/50"
            >
              <span
                className={cn(
                  'flex size-6 shrink-0 items-center justify-center rounded-full border text-xs font-semibold',
                  s.done ? 'border-emerald-500 bg-emerald-500 text-white' : 'border-zinc-300 text-zinc-500 dark:text-zinc-400 dark:border-zinc-600',
                )}
              >
                {s.done ? <Check className="size-3.5" aria-label="Done" /> : i + 1}
              </span>
              <span className="min-w-0 flex-1">
                <span className={cn('block truncate text-sm font-medium', s.done ? 'text-zinc-500 dark:text-zinc-400 line-through' : 'text-zinc-900 dark:text-zinc-100')}>
                  {tx(s.label)}
                </span>
                <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">{tx(s.hint)}</span>
              </span>
              {!s.done && <ChevronRight className="size-4 shrink-0 text-zinc-300 transition-transform group-hover:translate-x-0.5 dark:text-zinc-600" />}
            </Link>
          </li>
        ))}
      </ol>
    </Card>
  );
}
