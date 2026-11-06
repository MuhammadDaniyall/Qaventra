import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { cn } from '../../lib/cn';
import { Spinner } from './Spinner';
import { tx } from '../../lib/i18n';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'success';
type Size = 'sm' | 'md' | 'lg';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  icon?: ReactNode;
}

const variants: Record<Variant, string> = {
  primary:
    'bg-gradient-to-r from-accent-600 to-violet-600 text-white shadow-md shadow-accent-600/25 ring-1 ring-inset ring-white/10 hover:from-accent-500 hover:to-violet-500 hover:shadow-lg hover:shadow-accent-600/30 dark:from-accent-500 dark:to-violet-500 dark:hover:from-accent-400 dark:hover:to-violet-400',
  secondary:
    'bg-white/80 text-zinc-800 border border-zinc-200/90 shadow-xs backdrop-blur hover:border-zinc-300 hover:bg-white dark:bg-zinc-900/70 dark:text-zinc-100 dark:border-white/10 dark:hover:bg-zinc-800',
  ghost: 'text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800 dark:hover:text-white',
  danger: 'bg-gradient-to-r from-red-600 to-rose-600 text-white shadow-md shadow-red-600/20 hover:from-red-500 hover:to-rose-500',
  success: 'bg-gradient-to-r from-emerald-600 to-teal-600 text-white shadow-md shadow-emerald-600/20 hover:from-emerald-500 hover:to-teal-500',
};

const sizes: Record<Size, string> = {
  sm: 'h-9 px-3 text-xs gap-1.5 sm:h-8',
  md: 'h-10 px-3.5 text-sm gap-2 sm:h-9',
  lg: 'h-11 px-5 text-sm gap-2',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'md', loading, icon, className, children, disabled, type = 'button', ...rest },
  ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-xl font-medium whitespace-nowrap transition-all duration-150 active:scale-[0.97]',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500',
        'disabled:pointer-events-none disabled:opacity-50',
        variants[variant],
        sizes[size],
        className,
      )}
      {...rest}
    >
      {loading ? <Spinner className="size-4" /> : icon}
      {tx(children)}
    </button>
  );
});

export function IconButton({
  label,
  className,
  children,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string }) {
  return (
    <button
      type="button"
      aria-label={tx(label)}
      title={tx(label)}
      className={cn(
        'inline-flex size-10 items-center justify-center rounded-xl text-zinc-500 dark:text-zinc-400 sm:size-9 transition-all hover:bg-zinc-900/[0.05] hover:text-zinc-900 active:scale-95',
        'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500',
        'disabled:opacity-50 dark:text-zinc-400 dark:hover:bg-white/[0.07] dark:hover:text-white',
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}
