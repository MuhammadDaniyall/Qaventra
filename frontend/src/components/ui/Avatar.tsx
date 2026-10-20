import { cn } from '../../lib/cn';
import { initials } from '../../lib/format';

const palette = [
  'bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-200',
  'bg-violet-100 text-violet-700 dark:bg-violet-500/20 dark:text-violet-200',
  'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-200',
  'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-200',
  'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-200',
  'bg-rose-100 text-rose-700 dark:bg-rose-500/20 dark:text-rose-200',
];

export function Avatar({ name, size = 'md', className }: { name: string; size?: 'sm' | 'md' | 'lg'; className?: string }) {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  const tone = palette[Math.abs(hash) % palette.length];
  const sz = size === 'sm' ? 'size-7 text-[11px]' : size === 'lg' ? 'size-14 text-lg' : 'size-8 text-xs';
  return (
    <span
      className={cn('inline-flex shrink-0 items-center justify-center rounded-full font-semibold', tone, sz, className)}
      aria-hidden="true"
    >
      {initials(name)}
    </span>
  );
}
