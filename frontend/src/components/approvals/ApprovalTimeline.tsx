import { useQuery } from '@tanstack/react-query';
import { Check, Clock, Minus, X } from 'lucide-react';
import { approvalsApi, APPROVER_LABELS, type ApprovalRequestType, type ApprovalStep } from '../../api/approvals';
import { qk } from '../../api/keys';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { Skeleton } from '../ui';

const icon = {
  Approved: { el: Check, cls: 'bg-emerald-500 text-white' },
  Rejected: { el: X, cls: 'bg-red-500 text-white' },
  Current: { el: Clock, cls: 'bg-amber-100 text-amber-700 ring-2 ring-amber-400 dark:bg-amber-500/20 dark:text-amber-200' },
  Waiting: { el: Clock, cls: 'bg-zinc-100 text-zinc-400 dark:bg-zinc-800' },
  Skipped: { el: Minus, cls: 'bg-zinc-100 text-zinc-400 dark:bg-zinc-800' },
} as const;

function who(step: ApprovalStep) {
  if (step.kind === 'Hr') return 'HR';
  return step.approverName ? `${step.approverName} (${APPROVER_LABELS[step.kind].toLowerCase()})` : APPROVER_LABELS[step.kind];
}

/** Who approves a request, step by step, and where it stands. Renders nothing for requests that predate chains. */
export function ApprovalTimeline({ type, subjectId, className }: { type: ApprovalRequestType; subjectId: string; className?: string }) {
  const timeline = useQuery({ queryKey: qk.approvalTimeline(type, subjectId), queryFn: () => approvalsApi.timeline(type, subjectId) });

  if (timeline.isLoading) return <Skeleton className={cn('h-16 w-full', className)} />;
  if (!timeline.data) return null;
  if (!timeline.data.steps.length) {
    return <p className={cn('text-sm text-zinc-500', className)}>Approved automatically, no approval needed.</p>;
  }

  return (
    <>
      {timeline.data.forced && (
        <p
          className={cn(
            'mb-2 inline-flex rounded-md bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-500/10 dark:text-amber-200',
            className,
          )}
        >
          Forcefully approved by HR
        </p>
      )}
      <ol className={cn('space-y-0', className)} aria-label="Approval steps">
        {timeline.data.steps.map((step, i) => {
          const I = icon[step.status];
          const last = i === timeline.data!.steps.length - 1;
          return (
            <li key={step.order} className="relative flex gap-3 pb-4 last:pb-0">
              {!last && <span className="absolute top-7 left-3 h-[calc(100%-1.75rem)] w-px bg-zinc-200 dark:bg-zinc-700" aria-hidden="true" />}
              <span className={cn('relative flex size-6 shrink-0 items-center justify-center rounded-full', I.cls)} aria-hidden="true">
                <I.el className="size-3.5" />
              </span>
              <div className="min-w-0 pt-0.5 text-sm">
                <p className="text-zinc-900 dark:text-zinc-100">
                  <span className="font-medium">{who(step)}</span>
                  <span className="text-zinc-500">
                    {' · '}
                    {step.status === 'Current' ? 'waiting' : step.status === 'Waiting' ? 'next' : step.status.toLowerCase()}
                  </span>
                </p>
                {step.decidedAtUtc && (
                  <p className="text-xs text-zinc-500">
                    {step.decidedByName && step.decidedByName !== step.approverName ? `${step.decidedByName}, ` : ''}
                    {formatDateTimeUtc(step.decidedAtUtc)}
                  </p>
                )}
                {step.note && <p className="mt-1 text-zinc-600 dark:text-zinc-300">“{step.note}”</p>}
              </div>
            </li>
          );
        })}
      </ol>
    </>
  );
}
