import type { ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import { ChevronLeft, Sparkles } from 'lucide-react';
import { tx } from '../../lib/i18n';
import { cn } from '../../lib/cn';
import { NAV } from '../layout/nav';

/** The sidebar icon of the closest menu entry for a path ("/payroll/runs/7" → Payroll). */
function navIconFor(path: string): LucideIcon | undefined {
  let best: { to: string; icon: LucideIcon } | undefined;
  for (const section of NAV)
    for (const item of section.items)
      if ((path === item.to || path.startsWith(item.to + '/')) && (!best || item.to.length > best.to.length)) best = item;
  return best?.icon;
}

export function PageHeader({
  title,
  description,
  actions,
  back,
  hero,
  eyebrow,
  icon,
}: {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  back?: { to: string; label: string };
  /** A glowing glass banner (the home dashboard), in the style of the sign-in page. */
  hero?: boolean;
  /** A small pill above the title, shown with `hero`. */
  eyebrow?: ReactNode;
  /** Icon tile beside the title; defaults to the page's sidebar icon. Pass null for none. */
  icon?: LucideIcon | null;
}) {
  const { pathname } = useLocation();
  const Icon = icon === undefined ? navIconFor(pathname) : icon;
  const showIcon = !!Icon && !hero && typeof title === 'string';
  return (
    <div
      className={cn(
        'relative mb-6 animate-fade-in',
        hero &&
          'rounded-3xl border border-white/60 bg-white/60 p-5 shadow-[0_20px_50px_-30px_rgb(79_70_229/0.45)] backdrop-blur-xl sm:p-7 dark:border-white/[0.08] dark:bg-zinc-900/50',
      )}
    >
      {hero && (
        // Clipped separately so menus opened from the actions are not cut off.
        <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden rounded-3xl" aria-hidden="true">
          <div className="absolute -top-24 -start-16 size-72 rounded-full bg-indigo-500/25 blur-3xl motion-safe:animate-glow dark:bg-indigo-500/30" />
          <div className="absolute -end-10 -bottom-28 size-72 rounded-full bg-fuchsia-500/20 blur-3xl motion-safe:animate-glow dark:bg-fuchsia-500/25" style={{ animationDelay: '-6s' }} />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_1px_1px,rgb(99_102_241/0.12)_1px,transparent_0)] [background-size:22px_22px] [mask-image:linear-gradient(to_bottom,black,transparent)]" />
        </div>
      )}
      {back && (
        <Link
          to={back.to}
          className="mb-2 inline-flex min-h-8 items-center gap-1 rounded text-xs font-medium text-zinc-500 hover:text-zinc-900 focus-visible:outline-2 focus-visible:outline-accent-500 dark:text-zinc-400 dark:hover:text-white"
        >
          <ChevronLeft className="size-3.5" />
          {tx(back.label)}
        </Link>
      )}
      {hero && eyebrow && (
        <span className="mb-3 inline-flex w-fit items-center gap-1.5 rounded-full bg-gradient-to-r from-indigo-50 to-fuchsia-50 px-3 py-1 text-xs font-medium text-indigo-700 ring-1 ring-indigo-200/70 dark:from-indigo-500/10 dark:to-fuchsia-500/10 dark:text-indigo-200 dark:ring-indigo-400/20">
          <Sparkles className="size-3.5" aria-hidden="true" /> {tx(eyebrow)}
        </span>
      )}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex min-w-0 items-center gap-3.5">
          {showIcon && Icon && (
            <span
              className="relative flex size-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-500 via-violet-500 to-fuchsia-500 text-white shadow-lg shadow-indigo-500/25 ring-1 ring-inset ring-white/20 sm:size-12"
              aria-hidden="true"
            >
              <Icon className="size-5 sm:size-[22px]" />
            </span>
          )}
          <div className="min-w-0">
          <h1
            className={cn(
              'font-semibold tracking-tight',
              hero ? 'text-[26px] sm:text-4xl' : 'text-2xl sm:text-[28px]',
              // Plain-text titles get the brand gradient (as on the sign-in page); titles with icons keep their colors.
              typeof title === 'string'
                ? 'bg-gradient-to-br from-zinc-900 via-zinc-800 to-indigo-700 bg-clip-text pb-0.5 text-transparent dark:from-white dark:via-zinc-100 dark:to-indigo-300'
                : 'text-zinc-900 dark:text-white',
            )}
          >
            {tx(title)}
          </h1>
          {description && (
            <p className={cn('mt-1 text-zinc-500 dark:text-zinc-400', hero ? 'text-[15px]' : 'text-sm')}>{tx(description)}</p>
          )}
          </div>
        </div>
        {actions && <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto">{actions}</div>}
      </div>
    </div>
  );
}
