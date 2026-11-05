import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

export interface TabItem<T extends string> {
  value: T;
  label: ReactNode;
  icon?: ReactNode;
}

/**
 * Underlined tab bar with arrow-key navigation. When the tabs do not fit, arrow buttons and a fade appear at the
 * hidden side(s), and the mouse wheel scrolls the bar sideways, so no tab is ever out of sight without a hint.
 */
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
  const bar = useRef<HTMLDivElement>(null);
  const refs = useRef<Array<HTMLButtonElement | null>>([]);
  const [edges, setEdges] = useState({ start: false, end: false });
  const activeIndex = items.findIndex((item) => item.value === value);

  const measure = useCallback(() => {
    const el = bar.current;
    if (!el) return;
    // scrollLeft is negative in right-to-left layouts (Urdu); compare distances, not signs.
    const left = Math.abs(el.scrollLeft);
    const hidden = el.scrollWidth - el.clientWidth;
    const before = left > 2;
    const after = left < hidden - 2;
    setEdges({ start: before, end: after });
  }, []);

  useEffect(() => {
    measure();
    const el = bar.current;
    if (!el) return;
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, [measure, items.length]);

  // Keep the selected tab visible when the bar scrolls sideways (narrow screens, many tabs).
  useEffect(() => {
    refs.current[activeIndex]?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }, [activeIndex]);

  // A vertical mouse wheel over the bar scrolls it sideways while there is somewhere to go.
  useEffect(() => {
    const el = bar.current;
    if (!el) return;
    const onWheel = (e: WheelEvent) => {
      if (el.scrollWidth <= el.clientWidth || Math.abs(e.deltaX) > Math.abs(e.deltaY)) return;
      const before = el.scrollLeft;
      el.scrollLeft += e.deltaY;
      if (el.scrollLeft !== before) e.preventDefault();
    };
    el.addEventListener('wheel', onWheel, { passive: false });
    return () => el.removeEventListener('wheel', onWheel);
  }, []);

  const nudge = (dir: 1 | -1) => {
    const el = bar.current;
    if (!el) return;
    const rtl = getComputedStyle(el).direction === 'rtl';
    el.scrollBy({ left: dir * (rtl ? -1 : 1) * Math.max(160, el.clientWidth * 0.6), behavior: 'smooth' });
  };

  const arrow = 'absolute top-0 bottom-px z-10 flex w-10 items-center text-zinc-500 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-white';
  return (
    <div className={cn('relative', className)}>
      <div
        ref={bar}
        role="tablist"
        onScroll={measure}
        className="no-scrollbar -mx-1 flex gap-1 overflow-x-auto overflow-y-hidden border-b border-zinc-200 px-1 dark:border-zinc-800"
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
              {tx(item.label)}
            </button>
          );
        })}
      </div>
      {edges.start && (
        <button
          type="button"
          tabIndex={-1}
          aria-label={tx('Earlier tabs')}
          onClick={() => nudge(-1)}
          className={cn(arrow, 'start-0 justify-start bg-gradient-to-r from-zinc-50 via-zinc-50/90 to-transparent rtl:bg-gradient-to-l dark:from-zinc-950 dark:via-zinc-950/90')}
        >
          <ChevronLeft className="size-5 rtl:rotate-180" />
        </button>
      )}
      {edges.end && (
        <button
          type="button"
          tabIndex={-1}
          aria-label={tx('More tabs')}
          onClick={() => nudge(1)}
          className={cn(arrow, 'end-0 justify-end bg-gradient-to-l from-zinc-50 via-zinc-50/90 to-transparent rtl:bg-gradient-to-r dark:from-zinc-950 dark:via-zinc-950/90')}
        >
          <ChevronRight className="size-5 rtl:rotate-180" />
        </button>
      )}
    </div>
  );
}
