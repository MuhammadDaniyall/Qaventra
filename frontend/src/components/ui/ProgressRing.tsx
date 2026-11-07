import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';

/**
 * A round progress meter with the brand gradient, for completion shares (reviews done, goals met, seats filled).
 * `value` is 0–1. The label in the middle defaults to the percentage.
 */
export function ProgressRing({
  value,
  size = 56,
  stroke = 6,
  label,
  className,
  title,
}: {
  value: number;
  size?: number;
  stroke?: number;
  label?: ReactNode;
  className?: string;
  /** Spoken description, e.g. "8 of 14 reviews done". */
  title?: string;
}) {
  const share = Math.max(0, Math.min(1, Number.isFinite(value) ? value : 0));
  const r = (size - stroke) / 2;
  const circ = 2 * Math.PI * r;
  const id = `ring-${size}-${stroke}`;
  return (
    <span
      className={cn('relative inline-flex shrink-0 items-center justify-center', className)}
      style={{ width: size, height: size }}
      role="img"
      aria-label={title ?? `${Math.round(share * 100)}%`}
    >
      <svg viewBox={`0 0 ${size} ${size}`} className="size-full -rotate-90" aria-hidden="true">
        <defs>
          <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" stopColor="#6366f1" />
            <stop offset="55%" stopColor="#8b5cf6" />
            <stop offset="100%" stopColor="#d946ef" />
          </linearGradient>
        </defs>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" strokeWidth={stroke} className="stroke-zinc-100 dark:stroke-white/[0.08]" />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke={`url(#${id})`}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={circ}
          strokeDashoffset={circ * (1 - share)}
          className="transition-[stroke-dashoffset] duration-700 ease-out"
        />
      </svg>
      <span className="absolute inset-0 flex items-center justify-center text-xs font-semibold text-zinc-900 tabular-nums dark:text-white">
        {label ?? `${Math.round(share * 100)}%`}
      </span>
    </span>
  );
}

/** A thin gradient progress bar (0–1), for goals, weights and pipeline fill. */
export function ProgressBar({ value, className, tone = 'brand' }: { value: number; className?: string; tone?: 'brand' | 'green' | 'amber' }) {
  const share = Math.max(0, Math.min(1, Number.isFinite(value) ? value : 0));
  const fill = {
    brand: 'from-indigo-500 via-violet-500 to-fuchsia-500',
    green: 'from-emerald-500 to-teal-400',
    amber: 'from-amber-400 to-orange-500',
  }[tone];
  return (
    <div className={cn('h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-white/[0.07]', className)}>
      <div
        className={cn('h-full rounded-full bg-gradient-to-r transition-[width] duration-700 ease-out rtl:bg-gradient-to-l', fill)}
        style={{ width: `${share * 100}%` }}
      />
    </div>
  );
}
