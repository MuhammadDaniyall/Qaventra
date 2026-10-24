import { useMemo } from 'react';
import { ArrowRight } from 'lucide-react';

type Change = { old?: unknown; new?: unknown };

function render(value: unknown) {
  if (value === undefined) return <span className="text-zinc-500 dark:text-zinc-400">—</span>;
  if (value === null) return <span className="text-zinc-500 dark:text-zinc-400 italic">null</span>;
  if (typeof value === 'object') return <code className="text-xs break-all">{JSON.stringify(value)}</code>;
  return <span className="break-all">{String(value)}</span>;
}

/** Renders the audit `changes` JSON ({"Field":{"old":x,"new":y}}) as an old -> new table. */
export function ChangesDiff({ changes }: { changes: string }) {
  const parsed = useMemo(() => {
    try {
      const obj = JSON.parse(changes) as Record<string, Change>;
      return Object.entries(obj);
    } catch {
      return null;
    }
  }, [changes]);

  if (!parsed) {
    return (
      <pre className="overflow-x-auto rounded-lg bg-zinc-50 p-3 text-xs dark:bg-zinc-950">{changes}</pre>
    );
  }
  if (parsed.length === 0) return <p className="text-sm text-zinc-500 dark:text-zinc-400">No field changes recorded.</p>;

  return (
    <div className="overflow-x-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
      <table className="w-full text-start text-sm">
        <thead className="bg-zinc-50 text-xs text-zinc-500 dark:bg-zinc-900 dark:text-zinc-400">
          <tr>
            <th className="px-3 py-2 font-medium">Field</th>
            <th className="px-3 py-2 font-medium">Old</th>
            <th className="w-6" aria-hidden="true" />
            <th className="px-3 py-2 font-medium">New</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {parsed.map(([field, c]) => {
            const hasOld = c && typeof c === 'object' && 'old' in c;
            const hasNew = c && typeof c === 'object' && 'new' in c;
            return (
              <tr key={field} className="align-top">
                <td className="px-3 py-2 font-medium text-zinc-800 dark:text-zinc-200">{field}</td>
                <td className="px-3 py-2">
                  {hasOld ? (
                    <span className="rounded bg-red-50 px-1 text-red-700 dark:bg-red-500/10 dark:text-red-300">{render(c.old)}</span>
                  ) : (
                    <span className="text-zinc-500 dark:text-zinc-400">—</span>
                  )}
                </td>
                <td className="py-2 text-zinc-300 dark:text-zinc-600">
                  <ArrowRight className="size-3.5" />
                </td>
                <td className="px-3 py-2">
                  {hasNew ? (
                    <span className="rounded bg-emerald-50 px-1 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300">
                      {render(c.new)}
                    </span>
                  ) : (
                    <span className="text-zinc-500 dark:text-zinc-400">—</span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
