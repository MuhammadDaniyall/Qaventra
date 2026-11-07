import { cn } from '../lib/cn';
import { formatDateTimePk } from '../lib/format';
import { useT } from '../lib/i18n';
import type { RecordStamps } from '../types';

/** A record counts as modified once its last change differs from when (or by whom) it was added. */
function wasModified(s: RecordStamps) {
  return !!s.modifiedAtUtc && (s.modifiedAtUtc !== s.createdAtUtc || s.modifiedBy !== s.createdBy);
}

/**
 * "Added 3 Nov 2026, 10:42 by Ali Raza (EMP-0042) · Modified 4 Nov 2026, 09:10 by Sara Khan (HR-0003)", in Pakistan
 * time. For detail views, drawers and modals; tables use RecordStampCell. Unknown names (older records) show "—".
 */
export function RecordStamp({ stamps, className }: { stamps: RecordStamps; className?: string }) {
  const { t } = useT();
  const fill = (template: string, when: string | null, who: string | null) =>
    t(template).replace('{when}', formatDateTimePk(when)).replace('{who}', who ?? '—');
  return (
    <p className={cn('text-xs text-zinc-500 dark:text-zinc-400', className)}>
      <span>{fill('Added {when} by {who}', stamps.createdAtUtc, stamps.createdBy)}</span>
      {wasModified(stamps) && (
        <>
          <span aria-hidden="true"> · </span>
          <span>{fill('Modified {when} by {who}', stamps.modifiedAtUtc, stamps.modifiedBy)}</span>
        </>
      )}
    </p>
  );
}

/**
 * Compact version for a table column ("Added / modified"): the latest change on top, who made it below, both on one
 * short line each. The full "Added … · Modified …" text is in the tooltip.
 */
export function RecordStampCell({ stamps }: { stamps: RecordStamps }) {
  const { t } = useT();
  const modified = wasModified(stamps);
  const when = modified ? stamps.modifiedAtUtc : stamps.createdAtUtc;
  const who = (modified ? stamps.modifiedBy : stamps.createdBy) ?? '—';
  const full = [
    `${t('Added')} ${formatDateTimePk(stamps.createdAtUtc)} · ${stamps.createdBy ?? '—'}`,
    modified && `${t('Modified')} ${formatDateTimePk(stamps.modifiedAtUtc)} · ${stamps.modifiedBy ?? '—'}`,
  ]
    .filter(Boolean)
    .join('\n');
  return (
    <div className="text-xs leading-5" title={full}>
      <span className="flex items-center gap-1.5">
        <span
          className={cn('size-1.5 shrink-0 rounded-full', modified ? 'bg-amber-400' : 'bg-emerald-400')}
          aria-hidden="true"
        />
        <span className="text-zinc-500 dark:text-zinc-400">{t(modified ? 'Modified' : 'Added')}</span>
        <span className="text-zinc-700 tabular-nums dark:text-zinc-300">{formatDateTimePk(when)}</span>
      </span>
      <span className="block max-w-56 truncate ps-3 text-zinc-500 dark:text-zinc-400">{who}</span>
    </div>
  );
}
