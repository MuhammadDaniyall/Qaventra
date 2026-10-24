import { Check, Star } from 'lucide-react';
import { cn } from '../../lib/cn';
import type { Separation, SeparationType } from '../../types/workflows';

export const SEPARATION_TYPES: Array<{ value: SeparationType; label: string }> = [
  { value: 'Resignation', label: 'Resignation' },
  { value: 'Termination', label: 'Termination' },
  { value: 'Retirement', label: 'Retirement' },
  { value: 'ContractEnd', label: 'Contract end' },
];

export const typeLabel = (t: SeparationType) => SEPARATION_TYPES.find((x) => x.value === t)?.label ?? t;

export const EXIT_REASONS = [
  'Better opportunity',
  'Compensation',
  'Career growth',
  'Work-life balance',
  'Management',
  'Relocation',
  'Higher studies',
  'Health or family',
  'Other',
];

/** Where a separation stands: submitted → accepted → clearance → completed. */
export function SeparationSteps({ s }: { s: Separation }) {
  const status = s.summary.status;
  const closed = status === 'Rejected' || status === 'Withdrawn';
  const allCleared = s.clearance.length > 0 && s.clearance.every((c) => c.isCleared);
  const steps = [
    { label: s.summary.type === 'Resignation' ? 'Resignation submitted' : 'Separation started', done: true },
    { label: 'Accepted by HR', done: status === 'InProgress' || status === 'Completed' },
    { label: 'Clearance', done: status === 'Completed' || allCleared },
    { label: 'Settled & closed', done: status === 'Completed' },
  ];
  const current = steps.findIndex((x) => !x.done);

  if (closed) {
    return (
      <p className="rounded-xl bg-zinc-100 px-4 py-3 text-sm text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
        This {typeLabel(s.summary.type).toLowerCase()} was {status === 'Rejected' ? 'declined by HR' : 'withdrawn'}.{s.decisionNote && <> “{s.decisionNote}”</>}
      </p>
    );
  }

  return (
    <ol className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {steps.map((step, i) => (
        <li key={step.label} className="flex items-center gap-2.5">
          <span
            className={cn(
              'flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
              step.done
                ? 'bg-accent-600 text-white'
                : i === current
                  ? 'bg-accent-50 text-accent-700 ring-2 ring-accent-500 dark:bg-accent-500/15 dark:text-accent-200'
                  : 'bg-zinc-100 text-zinc-500 dark:text-zinc-400 dark:bg-zinc-800',
            )}
            aria-hidden="true"
          >
            {step.done ? <Check className="size-3.5" /> : i + 1}
          </span>
          <span className={cn('text-sm', step.done || i === current ? 'font-medium text-zinc-900 dark:text-white' : 'text-zinc-500 dark:text-zinc-400')}>
            {step.label}
            <span className="sr-only">{step.done ? ' (done)' : i === current ? ' (current)' : ''}</span>
          </span>
        </li>
      ))}
    </ol>
  );
}

export function Stars({ value, onChange, size = 'md' }: { value: number; onChange?: (v: number) => void; size?: 'sm' | 'md' }) {
  const cls = size === 'sm' ? 'size-4' : 'size-7';
  if (!onChange) {
    return (
      <span className="inline-flex gap-0.5" aria-label={`${value} out of 5`}>
        {[1, 2, 3, 4, 5].map((n) => (
          <Star key={n} className={cn(cls, n <= value ? 'fill-amber-400 text-amber-400' : 'text-zinc-300 dark:text-zinc-600')} aria-hidden="true" />
        ))}
      </span>
    );
  }
  return (
    <div className="flex gap-1" role="radiogroup" aria-label="Rating">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          aria-label={`${n} star${n === 1 ? '' : 's'}`}
          onClick={() => onChange(n)}
          className="rounded transition-transform hover:scale-110 focus-visible:outline-2 focus-visible:outline-accent-500"
        >
          <Star className={cn(cls, n <= value ? 'fill-amber-400 text-amber-400' : 'text-zinc-300 dark:text-zinc-600')} aria-hidden="true" />
        </button>
      ))}
    </div>
  );
}
