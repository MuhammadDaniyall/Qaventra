import { Fragment } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Check } from 'lucide-react';
import { qk } from '../../api/keys';
import { performanceApi, POTENTIAL_LABELS, STAGE_LABELS, STAGE_ORDER, type ReviewListItem, type ReviewStage } from '../../api/performance';
import { cn } from '../../lib/cn';

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
            value === n ? 'border-accent-500 bg-accent-600 text-white' : 'border-zinc-200 text-zinc-700 hover:border-accent-300 dark:border-zinc-700 dark:text-zinc-300',
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
export function StageBar({ stage, selfReview, hasGoals, className }: { stage: ReviewStage; selfReview: boolean; hasGoals: boolean; className?: string }) {
  const steps = STAGE_ORDER.filter((s) => (hasGoals || (s !== 'Goals' && s !== 'GoalsSubmitted')) && (selfReview || s !== 'SelfReview') && s !== 'GoalsSubmitted');
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
                done ? 'bg-accent-100 text-accent-700 dark:bg-accent-500/15 dark:text-accent-300' : now ? 'bg-accent-600 text-white' : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800',
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
            <div className="flex items-center pr-2 text-xs font-medium text-zinc-500 [writing-mode:vertical-rl] rotate-180">
              {POTENTIAL_LABELS[pot]}
            </div>
            {[1, 2, 3].map((perf) => {
              const key = `${perf}-${pot}`;
              const people = placed.filter((r) => band(r.finalRating!) === perf && r.potential === pot);
              const tone = perf + pot >= 5 ? 'bg-emerald-50 dark:bg-emerald-500/10' : perf + pot <= 3 ? 'bg-red-50 dark:bg-red-500/10' : 'bg-zinc-50 dark:bg-zinc-800/60';
              return (
                <div key={key} className={cn('min-h-24 rounded-lg p-2', tone)}>
                  <p className="mb-1 text-[11px] font-semibold text-zinc-600 dark:text-zinc-300">
                    {BOX_NAMES[key]} <span className="font-normal text-zinc-400">{people.length || ''}</span>
                  </p>
                  <div className="flex flex-wrap gap-1">
                    {people.map((p) => (
                      <button key={p.id} type="button" onClick={() => onPick?.(p)} className="rounded bg-white px-1.5 py-0.5 text-[11px] text-zinc-800 shadow-xs hover:ring-1 hover:ring-accent-400 dark:bg-zinc-900 dark:text-zinc-200">
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
          <p key={l} className="pt-1 text-center text-xs font-medium text-zinc-500">
            {l} performance
          </p>
        ))}
      </div>
      <p className="mt-2 text-xs text-zinc-500">Rows: potential (from the manager). Columns: final rating 1–2, 3, 4–5.</p>
    </div>
  );
}
