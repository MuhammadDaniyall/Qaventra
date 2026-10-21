import { useEffect, useRef, type ReactNode } from 'react';
import { cn } from '../../lib/cn';

export interface TabItem<T extends string> {
  value: T;
  label: ReactNode;
  icon?: ReactNode;
}

/** Underlined tab bar with arrow-key navigation. */
export function Tabs<T extends string>({
  items,
  value,
  onChange,
  className,
}: {
  items: TabItem<T>[];
  value: T;
  onChange: (value: T) => void;
  className?: string;
}) {
  const refs = useRef<Array<HTMLButtonElement | null>>([]);
  const activeIndex = items.findIndex((item) => item.value === value);
  // Keep the selected tab visible when the bar scrolls sideways (narrow screens, many tabs).
  useEffect(() => {
    refs.current[activeIndex]?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }, [activeIndex]);
  return (
    <div
      role="tablist"
      className={cn('-mx-1 flex gap-1 overflow-x-auto overflow-y-hidden border-b border-zinc-200 px-1 dark:border-zinc-800', className)}
    >
      {items.map((item, i) => {
        const active = item.value === value;
        return (
          <button
            key={item.value}
            ref={(el) => {
              refs.current[i] = el;
            }}
            type="button"
            role="tab"
            aria-selected={active}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(item.value)}
            onKeyDown={(e) => {
              if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
              e.preventDefault();
              const next = (i + (e.key === 'ArrowRight' ? 1 : -1) + items.length) % items.length;
              onChange(items[next].value);
              refs.current[next]?.focus();
            }}
            className={cn(
              '-mb-px inline-flex items-center gap-2 border-b-2 px-3 py-2.5 text-sm font-medium whitespace-nowrap transition-colors',
              'focus-visible:rounded-t-md focus-visible:outline-2 focus-visible:outline-accent-500',
              active
                ? 'border-accent-600 text-accent-700 dark:border-accent-400 dark:text-accent-300'
                : 'border-transparent text-zinc-500 hover:border-zinc-300 hover:text-zinc-800 dark:text-zinc-400 dark:hover:text-zinc-200',
            )}
          >
            {item.icon}
            {item.label}
          </button>
        );
      })}
    </div>
  );
}
