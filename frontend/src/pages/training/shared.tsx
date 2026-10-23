import { Star } from 'lucide-react';
import { COMPLIANCE_LABELS, type ComplianceState } from '../../api/training';
import { Badge } from '../../components/ui';
import type { Tone } from '../../components/ui/Badge';
import { cn } from '../../lib/cn';

const TONES: Record<ComplianceState, Tone> = { Done: 'green', DueSoon: 'amber', Overdue: 'red', NotDone: 'gray' };

export function ComplianceBadge({ state, count }: { state: ComplianceState; count?: number }) {
  return (
    <Badge tone={TONES[state]} dot>
      {COMPLIANCE_LABELS[state]}
      {count != null && ` ${count}`}
    </Badge>
  );
}

export function Rating({ value, onChange }: { value: number | null; onChange: (v: number) => void }) {
  return (
    <span className="inline-flex gap-1" role="radiogroup">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          title={`${n} of 5`}
          onClick={() => onChange(n)}
          className="rounded p-0.5 hover:bg-zinc-100 dark:hover:bg-zinc-800"
        >
          <Star className={cn('size-6', (value ?? 0) >= n ? 'fill-amber-400 text-amber-400' : 'text-zinc-300 dark:text-zinc-600')} />
        </button>
      ))}
    </span>
  );
}
