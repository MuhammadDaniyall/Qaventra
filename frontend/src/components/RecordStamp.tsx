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

/** Compact two-line version for a table column ("Added / modified"): when on top, who below. */
export function RecordStampCell({ stamps }: { stamps: RecordStamps }) {
  const { t } = useT();
  const line = (label: string, when: string | null, who: string | null) => (
    <span className="block" title={`${t(label)} ${formatDateTimePk(when)} · ${who ?? '—'}`}>
      <span className="text-zinc-500 dark:text-zinc-400">{t(label)} </span>
      <span className="tabular-nums">{formatDateTimePk(when)}</span>
      <span className="block max-w-56 truncate text-zinc-500 dark:text-zinc-400">{who ?? '—'}</span>
    </span>
  );
  return (
    <div className="space-y-1 text-xs">
      {line('Added', stamps.createdAtUtc, stamps.createdBy)}
      {wasModified(stamps) && line('Modified', stamps.modifiedAtUtc, stamps.modifiedBy)}
    </div>
  );
}
