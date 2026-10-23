import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Circle, Trash2 } from 'lucide-react';
import { qk } from '../../api/keys';
import { onboardingApi, TASK_CATEGORY_LABELS, type OnboardingTask } from '../../api/onboarding';
import { Badge, IconButton, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { formatDate } from '../../lib/format';

/** A checklist of onboarding tasks; people tick off the ones that are theirs. */
export function TaskList({ tasks, showFor, onDelete }: { tasks: OnboardingTask[]; showFor?: boolean; onDelete?: (t: OnboardingTask) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const toggle = useMutation({
    mutationFn: (t: OnboardingTask) => onboardingApi.setTask(t.id, !t.completedAtUtc),
    onSuccess: (_, t) => {
      if (!t.completedAtUtc) toast.success('Done', t.title);
      qc.invalidateQueries({ queryKey: qk.onboarding });
    },
    onError: (err) => toast.error(err),
  });
  return (
    <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
      {tasks.map((t) => {
        const done = !!t.completedAtUtc;
        return (
          <li key={t.id} className="flex items-start gap-3 px-5 py-3">
            <button
              type="button"
              disabled={!t.canDo || toggle.isPending}
              onClick={() => toggle.mutate(t)}
              aria-label={done ? `Mark "${t.title}" not done` : `Mark "${t.title}" done`}
              className={cn('mt-0.5 shrink-0', t.canDo ? 'cursor-pointer' : 'cursor-default opacity-60')}
            >
              {done ? <CheckCircle2 className="size-5 text-emerald-500" /> : <Circle className="size-5 text-zinc-300 dark:text-zinc-600" />}
            </button>
            <div className="min-w-0 flex-1">
              <p className={cn('text-sm font-medium', done ? 'text-zinc-400 line-through' : 'text-zinc-900 dark:text-zinc-100')}>{t.title}</p>
              <p className="flex flex-wrap items-center gap-x-2 text-xs text-zinc-500">
                {showFor && t.forEmployee && <span className="font-medium text-zinc-700 dark:text-zinc-300">For {t.forEmployee}</span>}
                <span>{TASK_CATEGORY_LABELS[t.category]}</span>
                <span>· {t.assignee ?? 'Not assigned'}</span>
                <span className={cn(t.isOverdue && 'font-medium text-red-600 dark:text-red-400')}>· due {formatDate(t.dueOn)}</span>
                {done && t.completedByName && <span>· done by {t.completedByName}</span>}
              </p>
              {t.description && <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">{t.description}</p>}
            </div>
            {t.isOverdue && <Badge tone="red">Late</Badge>}
            {onDelete && (
              <IconButton label="Remove task" onClick={() => onDelete(t)}>
                <Trash2 className="size-4" />
              </IconButton>
            )}
          </li>
        );
      })}
    </ul>
  );
}
