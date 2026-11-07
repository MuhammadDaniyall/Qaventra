import { Fragment } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check } from 'lucide-react';
import { qk } from '../../api/keys';
import { performanceApi, POTENTIAL_LABELS, STAGE_LABELS, STAGE_ORDER, type ReviewListItem, type ReviewStage } from '../../api/performance';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

/** 1–5 buttons with the company's rating names. */
export function RatingPicker({ value, onChange, labels }: { value: number | null; onChange: (v: number) => void; labels?: string[] }) {
  const settings = useQuery({ queryKey: qk.performanceSettings, queryFn: performanceApi.settings, enabled: !labels });
  const names = labels ?? settings.data?.ratingLabels ?? [];
  return (
    <div className="flex flex-wrap gap-1.5" role="radiogroup" aria-label="Rating">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          onClick={() => onChange(n)}
          title={names[n - 1]}
          className={cn(
            'flex min-w-12 flex-col items-center rounded-lg border px-2.5 py-1.5 text-xs transition',
            value === n
              ? 'border-accent-500 bg-accent-600 text-white'
              : 'border-zinc-200 text-zinc-700 hover:border-accent-300 dark:border-zinc-700 dark:text-zinc-300',
          )}
        >
          <span className="text-sm font-semibold">{n}</span>
          {names[n - 1] && <span className={cn('hidden max-w-24 truncate sm:block', value === n ? 'text-white/85' : 'text-zinc-500')}>{names[n - 1]}</span>}
        </button>
      ))}
    </div>
  );
}

/** Where a review is, as steps. */
export function StageBar({
  stage,
  selfReview,
  hasGoals,
  finalReview = false,
  className,
}: {
  stage: ReviewStage;
  selfReview: boolean;
  hasGoals: boolean;
  finalReview?: boolean;
  className?: string;
}) {
  const steps = STAGE_ORDER.filter(
    (s) =>
      (hasGoals || (s !== 'Goals' && s !== 'GoalsSubmitted')) &&
      (selfReview || s !== 'SelfReview') &&
      (finalReview || stage === 'FinalReview' || s !== 'FinalReview') &&
      s !== 'GoalsSubmitted',
  );
  const current = STAGE_ORDER.indexOf(stage === 'GoalsSubmitted' ? 'Goals' : stage);
  return (
    <ol className={cn('flex flex-wrap items-center gap-2 text-xs', className)}>
      {steps.map((s, i) => {
        const idx = STAGE_ORDER.indexOf(s);
        const done = idx < current || stage === 'Done';
        const now = idx === current && stage !== 'Done';
        return (
          <li key={s} className="flex items-center gap-2">
            {i > 0 && <span className={cn('h-px w-6', done || now ? 'bg-accent-400' : 'bg-zinc-200 dark:bg-zinc-700')} />}
            <span
              className={cn(
                'flex items-center gap-1 rounded-full px-2.5 py-1 font-medium',
                done
                  ? 'bg-accent-100 text-accent-700 dark:bg-accent-500/15 dark:text-accent-300'
                  : now
                    ? 'bg-accent-600 text-white'
                    : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800',
              )}
            >
              {done && <Check className="size-3" />}
              {s === 'Goals' && stage === 'GoalsSubmitted' ? STAGE_LABELS.GoalsSubmitted : STAGE_LABELS[s]}
            </span>
          </li>
        );
      })}
    </ol>
  );
}

/** One colour per review stage, used by chips, the stage mix bar and its legend. */
export const STAGE_COLORS: Record<ReviewStage, { dot: string; chip: string }> = {
  Goals: { dot: 'bg-amber-400', chip: 'bg-amber-50 text-amber-800 ring-amber-600/20 dark:bg-amber-500/10 dark:text-amber-300' },
  GoalsSubmitted: { dot: 'bg-orange-400', chip: 'bg-orange-50 text-orange-700 ring-orange-600/20 dark:bg-orange-500/10 dark:text-orange-300' },
  SelfReview: { dot: 'bg-sky-400', chip: 'bg-sky-50 text-sky-700 ring-sky-600/20 dark:bg-sky-500/10 dark:text-sky-300' },
  ManagerReview: { dot: 'bg-indigo-500', chip: 'bg-indigo-50 text-indigo-700 ring-indigo-600/20 dark:bg-indigo-500/10 dark:text-indigo-300' },
  FinalReview: { dot: 'bg-violet-500', chip: 'bg-violet-50 text-violet-700 ring-violet-600/20 dark:bg-violet-500/10 dark:text-violet-300' },
  Calibration: { dot: 'bg-fuchsia-500', chip: 'bg-fuchsia-50 text-fuchsia-700 ring-fuchsia-600/20 dark:bg-fuchsia-500/10 dark:text-fuchsia-300' },
  SignOff: { dot: 'bg-teal-500', chip: 'bg-teal-50 text-teal-700 ring-teal-600/20 dark:bg-teal-500/10 dark:text-teal-300' },
  Done: { dot: 'bg-emerald-500', chip: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-500/10 dark:text-emerald-300' },
};

/** A review stage as a coloured chip with a dot. */
export function StageChip({ stage }: { stage: ReviewStage }) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset', STAGE_COLORS[stage].chip)}>
      <span className={cn('size-1.5 rounded-full', STAGE_COLORS[stage].dot)} aria-hidden="true" />
      {tx(STAGE_LABELS[stage])}
    </span>
  );
}

/** How far a review has come through the stages, 0–1 (Done = 1). */
export function stageProgress(stage: ReviewStage) {
  return stage === 'Done' ? 1 : STAGE_ORDER.indexOf(stage) / (STAGE_ORDER.length - 1);
}

/** Where a cycle's reviews are: one coloured segment per stage, with a legend of counts. */
export function StageMix({ byStage, total, className }: { byStage: Partial<Record<ReviewStage, number>>; total: number; className?: string }) {
  const parts = STAGE_ORDER.map((s) => [s, byStage[s] ?? 0] as const).filter(([, n]) => n > 0);
  return (
    <div className={className}>
      <div className="flex h-2.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-white/[0.07]" aria-hidden="true">
        {parts.map(([s, n]) => (
          <span key={s} className={cn('h-full first:rounded-s-full last:rounded-e-full', STAGE_COLORS[s].dot)} style={{ width: `${(n / Math.max(1, total)) * 100}%` }} />
        ))}
      </div>
      <ul className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-zinc-600 dark:text-zinc-400">
        {parts.map(([s, n]) => (
          <li key={s} className="inline-flex items-center gap-1.5">
            <span className={cn('size-2 rounded-full', STAGE_COLORS[s].dot)} aria-hidden="true" />
            {tx(STAGE_LABELS[s])} <span className="font-semibold text-zinc-900 tabular-nums dark:text-zinc-100">{n}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

const BOX_NAMES: Record<string, string> = {
  '3-3': 'Stars',
  '3-2': 'High performers',
  '3-1': 'Solid experts',
  '2-3': 'Future stars',
  '2-2': 'Core players',
  '2-1': 'Steady contributors',
  '1-3': 'Rough diamonds',
  '1-2': 'Inconsistent',
  '1-1': 'Needs attention',
};

/** Potential (rows) against performance (columns): who sits in which box. */
export function NineBox({ reviews, onPick }: { reviews: ReviewListItem[]; onPick?: (r: ReviewListItem) => void }) {
  const band = (rating: number) => (rating <= 2 ? 1 : rating === 3 ? 2 : 3);
  const placed = reviews.filter((r) => r.finalRating && r.potential);
  return (
    <div>
      <div className="grid grid-cols-[auto_1fr_1fr_1fr] gap-1.5">
        {[3, 2, 1].map((pot) => (
          <Fragment key={pot}>
            <div className="flex items-center pe-2 text-xs font-medium text-zinc-500 dark:text-zinc-400 [writing-mode:vertical-rl] rotate-180">
              {POTENTIAL_LABELS[pot]}
            </div>
            {[1, 2, 3].map((perf) => {
              const key = `${perf}-${pot}`;
              const people = placed.filter((r) => band(r.finalRating!) === perf && r.potential === pot);
              const tone =
                perf + pot >= 5 ? 'bg-emerald-50 dark:bg-emerald-500/10' : perf + pot <= 3 ? 'bg-red-50 dark:bg-red-500/10' : 'bg-zinc-50 dark:bg-zinc-800/60';
              return (
                <div key={key} className={cn('min-h-24 rounded-lg p-2', tone)}>
                  <p className="mb-1 text-[11px] font-semibold text-zinc-600 dark:text-zinc-300">
                    {BOX_NAMES[key]} <span className="font-normal text-zinc-500">{people.length || ''}</span>
                  </p>
                  <div className="flex flex-wrap gap-1">
                    {people.map((p) => (
                      <button
                        key={p.id}
                        type="button"
                        onClick={() => onPick?.(p)}
                        className="rounded bg-white px-1.5 py-0.5 text-[11px] text-zinc-800 shadow-xs hover:ring-1 hover:ring-accent-400 dark:bg-zinc-900 dark:text-zinc-200"
                      >
                        {p.employeeName}
                      </button>
                    ))}
                  </div>
                </div>
              );
            })}
          </Fragment>
        ))}
        <div />
        {['Low', 'Medium', 'High'].map((l) => (
          <p key={l} className="pt-1 text-center text-xs font-medium text-zinc-500 dark:text-zinc-400">
            {l} performance
          </p>
        ))}
      </div>
      <p className="mt-2 text-xs text-zinc-500 dark:text-zinc-400">Rows: potential (from the manager). Columns: final rating 1–2, 3, 4–5.</p>
    </div>
  );
}
