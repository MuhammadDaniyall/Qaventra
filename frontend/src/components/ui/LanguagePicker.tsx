import { Languages } from 'lucide-react';
import { cn } from '../../lib/cn';
import { LANGS, tx, useT } from '../../lib/i18n';

/** English / اردو / العربية as a segmented control; each option is written in its own language. */
export function LanguagePicker({ className, onPicked }: { className?: string; onPicked?: () => void }) {
  const { lang, setLang } = useT();
  return (
    <div
      role="radiogroup"
      aria-label={tx('Language')}
      className={cn(
        'inline-flex items-center gap-0.5 rounded-full border border-zinc-200/80 bg-white/70 p-0.5 shadow-xs backdrop-blur',
        'dark:border-zinc-800 dark:bg-zinc-900/70',
        className,
      )}
    >
      <Languages className="ms-2 me-1 hidden size-3.5 shrink-0 text-zinc-400 sm:block" aria-hidden="true" />
      {LANGS.map((l) => {
        const active = l.value === lang;
        return (
          <button
            key={l.value}
            type="button"
            role="radio"
            aria-checked={active}
            lang={l.value}
            onClick={() => {
              setLang(l.value);
              onPicked?.();
            }}
            className={cn(
              'min-h-8 rounded-full px-2.5 text-[13px] sm:px-3 font-medium whitespace-nowrap transition-all',
              'focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent-500',
              active
                ? 'bg-gradient-to-r from-indigo-600 to-fuchsia-600 text-white shadow-sm shadow-indigo-500/30'
                : 'text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800 dark:hover:text-white',
            )}
          >
            {l.label}
          </button>
        );
      })}
    </div>
  );
}
