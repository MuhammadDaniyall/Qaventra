import { useMutation, useQuery } from '@tanstack/react-query';
import { http } from '../../api/client';
import { Card, CardBody, CardHeader, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

interface DisplaySettings {
  use24HourTime: boolean;
}

const api = {
  get: () => http.get<DisplaySettings>('/setup/display'),
  save: (d: DisplaySettings) => http.put<DisplaySettings>('/setup/display', d),
};

/** Company-wide display choices. */
export function GeneralTab() {
  return (
    <div className="space-y-6">
      <TimeFormatCard />
    </div>
  );
}

function TimeFormatCard() {
  const toast = useToast();
  const q = useQuery({ queryKey: ['display-settings'], queryFn: api.get });
  const save = useMutation({
    mutationFn: api.save,
    // Every screen reads the clock from the session, so load it again.
    onSuccess: () => window.location.reload(),
    onError: (e) => toast.error(e),
  });
  const current = q.data?.use24HourTime;
  const choices = [
    { value: false, title: '12 hours', example: '9:05 AM · 6:30 PM' },
    { value: true, title: '24 hours', example: '09:05 · 18:30' },
  ];
  return (
    <Card>
      <CardHeader title="Time format" description="How times show for everyone in the company: screens, reports, scheduled emails and Excel." />
      <CardBody className="grid gap-3 sm:grid-cols-2">
        {choices.map((c) => (
          <button
            key={c.title}
            type="button"
            disabled={q.isLoading || save.isPending}
            aria-pressed={current === c.value}
            onClick={() => current !== c.value && save.mutate({ use24HourTime: c.value })}
            className={cn(
              'rounded-xl border p-4 text-start transition',
              current === c.value
                ? 'border-indigo-400 bg-indigo-50/70 ring-4 ring-indigo-500/10 dark:border-indigo-400/70 dark:bg-indigo-500/10'
                : 'border-zinc-200 hover:border-indigo-300 dark:border-white/10 dark:hover:border-indigo-400/50',
            )}
          >
            <div className="font-medium text-zinc-900 dark:text-white">{tx(c.title)}</div>
            <div className="mt-1 text-sm text-zinc-500 tabular-nums dark:text-zinc-400" dir="ltr">
              {c.example}
            </div>
          </button>
        ))}
      </CardBody>
    </Card>
  );
}
