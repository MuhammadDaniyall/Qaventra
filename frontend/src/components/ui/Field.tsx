import {
  cloneElement,
  forwardRef,
  isValidElement,
  useId,
  type InputHTMLAttributes,
  type ReactElement,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react';
import { ChevronDown } from 'lucide-react';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

const control =
  // text-base below sm stops iOS Safari from zooming into a focused field.
  'block w-full rounded-lg border bg-white px-3 text-base text-zinc-900 shadow-xs transition-colors placeholder:text-zinc-400 sm:text-sm ' +
  'focus:outline-none focus:ring-2 focus:ring-accent-500/30 focus:border-accent-500 ' +
  'disabled:cursor-not-allowed disabled:bg-zinc-50 disabled:text-zinc-500 ' +
  'dark:bg-zinc-900 dark:text-zinc-100 dark:placeholder:text-zinc-500 dark:disabled:bg-zinc-800/50';

function borderFor(invalid?: boolean) {
  return invalid ? 'border-red-400 dark:border-red-500/70' : 'border-zinc-200 dark:border-zinc-700';
}

interface FieldProps {
  label: string;
  hint?: ReactNode;
  error?: string;
  required?: boolean;
  className?: string;
  children: ReactElement<{ id?: string; 'aria-invalid'?: boolean; 'aria-describedby'?: string }>;
}

/** Label + control + hint/error. Wires id, aria-invalid and aria-describedby onto the child. */
export function Field({ label, hint, error, required, className, children }: FieldProps) {
  const id = useId();
  const descId = `${id}-desc`;
  const child = isValidElement(children)
    ? cloneElement(children, {
        id,
        'aria-invalid': error ? true : undefined,
        'aria-describedby': error || hint ? descId : undefined,
      })
    : children;
  return (
    <div className={cn('space-y-1.5', className)}>
      <label htmlFor={id} className="block text-sm font-medium text-zinc-700 dark:text-zinc-300">
        {tx(label)}
        {required && <span className="ms-0.5 text-red-500">*</span>}
      </label>
      {child}
      {error ? (
        <p id={descId} className="text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      ) : hint ? (
        <p id={descId} className="text-xs text-zinc-500 dark:text-zinc-400">
          {tx(hint)}
        </p>
      ) : null}
    </div>
  );
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(function Input(
  { className, ...rest },
  ref,
) {
  return (
    <input
      ref={ref}
      className={cn(control, 'h-10 sm:h-9', borderFor(rest['aria-invalid'] === true), className)}
      {...rest}
    />
  );
});

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  function Textarea({ className, rows = 3, ...rest }, ref) {
    return (
      <textarea
        ref={ref}
        rows={rows}
        className={cn(control, 'py-2', borderFor(rest['aria-invalid'] === true), className)}
        {...rest}
      />
    );
  },
);

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(function Select(
  { className, children, ...rest },
  ref,
) {
  return (
    <div className="relative">
      <select
        ref={ref}
        className={cn(control, 'h-10 appearance-none pe-9 sm:h-9', borderFor(rest['aria-invalid'] === true), className)}
        {...rest}
      >
        {children}
      </select>
      <ChevronDown
        className="pointer-events-none absolute top-1/2 end-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400"
        aria-hidden="true"
      />
    </div>
  );
});

export function Checkbox({
  label,
  description,
  className,
  ...rest
}: Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> & { label: ReactNode; description?: string }) {
  const id = useId();
  return (
    <div className={cn('flex items-start gap-2.5', className)}>
      <input
        id={id}
        type="checkbox"
        className="mt-0.5 size-4 rounded border-zinc-300 accent-accent-600 focus:ring-2 focus:ring-accent-500/30 dark:border-zinc-600"
        {...rest}
      />
      <label htmlFor={id} className="text-sm leading-5 select-none">
        <span className="font-medium text-zinc-800 dark:text-zinc-200">{tx(label)}</span>
        {description && <span className="block text-xs text-zinc-500 dark:text-zinc-400">{tx(description)}</span>}
      </label>
    </div>
  );
}

export function Switch({
  checked,
  onChange,
  label,
  description,
  disabled,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  description?: string;
  disabled?: boolean;
}) {
  const id = useId();
  return (
    <div className="flex items-start justify-between gap-4">
      <div>
        <label htmlFor={id} className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
          {tx(label)}
        </label>
        {description && <p className="text-xs text-zinc-500 dark:text-zinc-400">{tx(description)}</p>}
      </div>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={cn(
          'relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors',
          'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500 disabled:opacity-50',
          checked ? 'bg-accent-600' : 'bg-zinc-300 dark:bg-zinc-700',
        )}
      >
        <span
          className={cn(
            'inline-block size-5 rounded-full bg-white shadow transition-transform',
            checked ? 'translate-x-5.5 rtl:-translate-x-5.5' : 'translate-x-0.5 rtl:-translate-x-0.5',
          )}
        />
      </button>
    </div>
  );
}

export function FormError({ message }: { message?: string | null }) {
  if (!message) return null;
  return (
    <div
      role="alert"
      className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300"
    >
      {message}
    </div>
  );
}
