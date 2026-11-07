import { cn } from '../../lib/cn';
import { initials } from '../../lib/format';

/** Soft two-tone tints (one per name), with a hairline ring so light avatars hold their edge on white cards. */
const palette = [
  'from-indigo-100 to-violet-200/80 text-indigo-700 ring-indigo-500/15 dark:from-indigo-500/25 dark:to-violet-500/20 dark:text-indigo-200',
  'from-violet-100 to-fuchsia-200/70 text-violet-700 ring-violet-500/15 dark:from-violet-500/25 dark:to-fuchsia-500/20 dark:text-violet-200',
  'from-sky-100 to-cyan-200/70 text-sky-700 ring-sky-500/15 dark:from-sky-500/25 dark:to-cyan-500/20 dark:text-sky-200',
  'from-emerald-100 to-teal-200/70 text-emerald-700 ring-emerald-500/15 dark:from-emerald-500/25 dark:to-teal-500/20 dark:text-emerald-200',
  'from-amber-100 to-orange-200/70 text-amber-800 ring-amber-500/20 dark:from-amber-500/25 dark:to-orange-500/20 dark:text-amber-200',
  'from-rose-100 to-pink-200/70 text-rose-700 ring-rose-500/15 dark:from-rose-500/25 dark:to-pink-500/20 dark:text-rose-200',
];

export function Avatar({ name, size = 'md', className }: { name: string; size?: 'sm' | 'md' | 'lg' | 'xl'; className?: string }) {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  const tone = palette[Math.abs(hash) % palette.length];
  const sz = { sm: 'size-7 text-[11px]', md: 'size-8 text-xs', lg: 'size-14 text-lg', xl: 'size-20 text-2xl sm:size-24 sm:text-3xl' }[size];
  return (
    <span
      className={cn('inline-flex shrink-0 items-center justify-center rounded-full bg-gradient-to-br font-semibold ring-1 ring-inset dark:ring-white/10', tone, sz, className)}
      aria-hidden="true"
    >
      {initials(name)}
    </span>
  );
}
