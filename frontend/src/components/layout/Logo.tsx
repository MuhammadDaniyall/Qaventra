import { APP_NAME } from '../../lib/brand';
import { cn } from '../../lib/cn';

/** The Q-check mark: a Q whose tail is a tick. Same drawing as public/favicon.svg. */
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" aria-hidden="true" className={cn('size-8 shrink-0', className)}>
      <rect width="32" height="32" rx="8" fill="#4F46E5" />
      <circle cx="15" cy="15" r="7" stroke="#fff" strokeWidth="3" fill="none" />
      <path d="M17 18.5l2.6 2.6L25 15.5" stroke="#5DCAA5" strokeWidth="3" fill="none" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function Logo({ collapsed }: { collapsed?: boolean }) {
  return (
    <div className="flex items-center gap-2.5">
      <LogoMark />
      {!collapsed && <span className="text-[15px] font-semibold tracking-tight text-zinc-900 dark:text-white">{APP_NAME}</span>}
    </div>
  );
}
