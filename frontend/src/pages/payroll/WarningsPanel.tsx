import { AlertTriangle } from 'lucide-react';

export function WarningsPanel({ warnings }: { warnings: string[] }) {
  if (!warnings.length) return null;
  return (
    <div
      role="alert"
      className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100"
    >
      <div className="flex items-center gap-2 font-semibold">
        <AlertTriangle className="size-4 shrink-0" />
        {warnings.length} warning{warnings.length === 1 ? '' : 's'} — review before finalizing
      </div>
      <ul className="mt-2 list-disc space-y-1 ps-6">
        {warnings.map((w, i) => (
          <li key={i}>{w}</li>
        ))}
      </ul>
    </div>
  );
}
