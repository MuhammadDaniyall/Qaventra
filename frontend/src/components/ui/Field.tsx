import {
  cloneElement,
  forwardRef,
  isValidElement,
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type KeyboardEvent,
  type InputHTMLAttributes,
  type ReactElement,
  type ReactNode,
  type RefObject,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react';
import { createPortal } from 'react-dom';
import { Check, ChevronDown, Search } from 'lucide-react';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

const control =
  // text-base below sm stops iOS Safari from zooming into a focused field.
  'block w-full rounded-xl border bg-white/90 px-3.5 text-base text-zinc-900 shadow-[inset_0_1px_2px_rgb(16_24_40/0.04)] transition-[border-color,box-shadow,background-color] duration-150 placeholder:text-zinc-400 sm:text-sm ' +
  'hover:border-zinc-300 focus:bg-white focus:outline-none focus:border-indigo-400 focus:ring-4 focus:ring-indigo-500/10 ' +
  'disabled:cursor-not-allowed disabled:bg-zinc-50 disabled:text-zinc-500 disabled:hover:border-zinc-200 ' +
  'dark:bg-white/[0.03] dark:text-zinc-100 dark:placeholder:text-zinc-500 dark:hover:border-white/20 dark:focus:bg-white/[0.05] dark:focus:border-indigo-400/70 dark:focus:ring-indigo-400/15 dark:disabled:bg-white/[0.02]';

function borderFor(invalid?: boolean) {
  return invalid
    ? 'border-red-400 focus:border-red-400 focus:ring-red-500/10 dark:border-red-500/70'
    : 'border-zinc-200/90 dark:border-white/10';
}

interface FieldProps {
  label: string;
  hint?: ReactNode;
  error?: string;
  required?: boolean;
  className?: string;
  /** A small control shown at the end of the label row, such as "Write with AI". */
  action?: ReactNode;
  children: ReactElement<{ id?: string; 'aria-invalid'?: boolean; 'aria-describedby'?: string }>;
}

/** Label + control + hint/error. Wires id, aria-invalid and aria-describedby onto the child. */
export function Field({ label, hint, error, required, className, action, children }: FieldProps) {
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
      <div className="flex items-center justify-between gap-2">
        <label htmlFor={id} className="block text-[13px] font-medium text-zinc-700 dark:text-zinc-300">
          {tx(label)}
          {required && <span className="ms-0.5 text-red-500">*</span>}
        </label>
        {action}
      </div>
      {child}
      {error ? (
        <p id={descId} className="flex items-center gap-1 text-xs font-medium text-red-600 dark:text-red-400">
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
  { className, onChange, onFocus, ...rest },
  ref,
) {
  const number = rest.type === 'number';
  return (
    <input
      ref={ref}
      className={cn(control, 'h-10 sm:h-9', borderFor(rest['aria-invalid'] === true), className)}
      {...rest}
      // A cleared number field shows 0 again; typing after it must give "15", not "015".
      onChange={(e) => {
        if (number && /^-?0\d/.test(e.target.value)) e.target.value = e.target.value.replace(/^(-?)0+(?=\d)/, '$1');
        onChange?.(e);
      }}
      onFocus={(e) => {
        if (number) e.target.select();
        onFocus?.(e);
      }}
      placeholder={tx(rest.placeholder)}
    />
  );
});

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  function Textarea({ className, rows = 3, ...rest }, ref) {
    return (
      <textarea
        ref={ref}
        rows={rows}
        className={cn(control, 'py-2.5', borderFor(rest['aria-invalid'] === true), className)}
        {...rest}
        placeholder={tx(rest.placeholder)}
      />
    );
  },
);

/** Lists longer than this get a search box (people, departments, filters…). */
const SEARCH_FROM = 8;

interface Choice {
  value: string;
  label: string;
  group?: string;
  disabled: boolean;
}

/**
 * A select. Long lists open a searchable list instead of the browser's own; the real <select> stays in the page (hidden), so
 * value, onChange, refs and form libraries work exactly as before.
 */
export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(function Select(
  { className, children, ...rest },
  ref,
) {
  const inner = useRef<HTMLSelectElement | null>(null);
  const setRefs = useCallback(
    (el: HTMLSelectElement | null) => {
      inner.current = el;
      if (typeof ref === 'function') ref(el);
      else if (ref) ref.current = el;
    },
    [ref],
  );
  const [count, setCount] = useState(0);
  const [label, setLabel] = useState('');
  // Options come from the rendered <select>, so any children (option, optgroup, mapped lists) work.
  useLayoutEffect(() => {
    const el = inner.current;
    if (!el) return;
    if (el.options.length !== count) setCount(el.options.length);
    const text = el.selectedOptions[0]?.text ?? '';
    if (text !== label) setLabel(text);
  });
  const searchable = count > SEARCH_FROM && !rest.multiple && !rest.size;

  return (
    <div className="relative">
      <select
        ref={setRefs}
        className={cn(
          control,
          'h-10 appearance-none pe-9 sm:h-9',
          borderFor(rest['aria-invalid'] === true),
          className,
          searchable && 'sr-only',
        )}
        tabIndex={searchable ? -1 : rest.tabIndex}
        {...rest}
        onChange={(e) => {
          setLabel(e.target.selectedOptions[0]?.text ?? '');
          rest.onChange?.(e);
        }}
      >
        {children}
      </select>
      {searchable ? (
        <SearchList select={inner} label={label} className={className} invalid={rest['aria-invalid'] === true} disabled={rest.disabled} id={rest.id} />
      ) : (
        <ChevronDown
          className="pointer-events-none absolute top-1/2 end-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400"
          aria-hidden="true"
        />
      )}
    </div>
  );
});

function SearchList({
  select,
  label,
  className,
  invalid,
  disabled,
  id,
}: {
  select: RefObject<HTMLSelectElement | null>;
  label: string;
  className?: string;
  invalid: boolean;
  disabled?: boolean;
  id?: string;
}) {
  const button = useRef<HTMLButtonElement>(null);
  const list = useRef<HTMLUListElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const [choices, setChoices] = useState<Choice[]>([]);
  const [place, setPlace] = useState<{ left: number; width: number; top?: number; bottom?: number }>({ left: 0, width: 0 });
  const listId = useId();

  const place_ = useCallback(() => {
    const r = button.current?.getBoundingClientRect();
    if (!r) return;
    const below = window.innerHeight - r.bottom;
    // Open upwards when there is little room below (bottom of a dialog, phone keyboard).
    setPlace(below < 300 && r.top > below ? { left: r.left, width: r.width, bottom: window.innerHeight - r.top + 4 } : { left: r.left, width: r.width, top: r.bottom + 4 });
  }, []);

  const show = () => {
    const el = select.current;
    if (!el || disabled) return;
    const all: Choice[] = Array.from(el.options).map((o) => ({
      value: o.value,
      label: o.text,
      group: o.parentElement instanceof HTMLOptGroupElement ? o.parentElement.label : undefined,
      disabled: o.disabled,
    }));
    setChoices(all);
    setQuery('');
    setActive(Math.max(0, all.findIndex((c) => c.value === el.value)));
    place_();
    setOpen(true);
  };

  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => {
      const t = e.target as Node;
      if (!button.current?.contains(t) && !list.current?.parentElement?.contains(t)) setOpen(false);
    };
    document.addEventListener('mousedown', close);
    window.addEventListener('resize', place_);
    window.addEventListener('scroll', place_, true);
    return () => {
      document.removeEventListener('mousedown', close);
      window.removeEventListener('resize', place_);
      window.removeEventListener('scroll', place_, true);
    };
  }, [open, place_]);

  const words = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
  const shown = choices.filter((c) => words.every((w) => `${c.label} ${c.group ?? ''}`.toLowerCase().includes(w)));

  useEffect(() => {
    list.current?.querySelector<HTMLElement>(`[data-index="${active}"]`)?.scrollIntoView({ block: 'nearest' });
  }, [active, open]);

  const pick = (c: Choice) => {
    const el = select.current;
    if (!el || c.disabled) return;
    if (el.value !== c.value) {
      // Set the value the way the browser would, so React's onChange and form libraries see a normal change.
      Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')?.set?.call(el, c.value);
      el.dispatchEvent(new Event('change', { bubbles: true }));
    }
    setOpen(false);
    button.current?.focus();
  };

  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((a) => Math.min(shown.length - 1, a + 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((a) => Math.max(0, a - 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (shown[active]) pick(shown[active]);
    } else if (e.key === 'Escape' || e.key === 'Tab') {
      e.stopPropagation();
      setOpen(false);
      if (e.key === 'Escape') button.current?.focus();
    }
  };

  let lastGroup: string | undefined;
  return (
    <>
      <button
        ref={button}
        id={id ? `${id}-search` : undefined}
        type="button"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-haspopup="listbox"
        disabled={disabled}
        onClick={() => (open ? setOpen(false) : show())}
        onKeyDown={(e) => {
          if (!open && (e.key === 'ArrowDown' || e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault();
            show();
          }
        }}
        className={cn(control, 'flex h-10 items-center pe-9 text-start sm:h-9', borderFor(invalid), className)}
      >
        <span className="truncate">{label}</span>
      </button>
      <ChevronDown
        className="pointer-events-none absolute top-1/2 end-3 size-4 -translate-y-1/2 text-zinc-500 dark:text-zinc-400"
        aria-hidden="true"
      />
      {open &&
        createPortal(
          <div
            className="fixed z-[70] overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-xl shadow-zinc-900/10 dark:border-white/10 dark:bg-zinc-900"
            style={{ left: place.left, top: place.top, bottom: place.bottom, width: Math.max(place.width, 240) }}
            dir={document.documentElement.dir || undefined}
          >
            <div className="flex items-center gap-2 border-b border-zinc-100 px-3 dark:border-white/10">
              <Search className="size-4 shrink-0 text-zinc-400" aria-hidden="true" />
              <input
                autoFocus
                value={query}
                onChange={(e) => {
                  setQuery(e.target.value);
                  setActive(0);
                }}
                onKeyDown={onKey}
                placeholder={tx('Type to search')}
                aria-label={tx('Type to search')}
                aria-controls={listId}
                className="h-10 w-full bg-transparent text-base text-zinc-900 placeholder:text-zinc-400 focus:outline-none sm:text-sm dark:text-zinc-100"
              />
            </div>
            <ul ref={list} id={listId} role="listbox" className="max-h-64 overflow-y-auto p-1">
              {shown.length === 0 && <li className="px-3 py-2 text-sm text-zinc-500">{tx('No matches')}</li>}
              {shown.map((c, i) => {
                const heading = c.group && c.group !== lastGroup ? c.group : null;
                lastGroup = c.group;
                const selected = c.value === select.current?.value;
                return (
                  <li key={`${c.value}-${i}`} role="presentation">
                    {heading && <div className="px-3 pt-2 pb-1 text-xs font-medium text-zinc-500">{heading}</div>}
                    <div
                      role="option"
                      aria-selected={selected}
                      aria-disabled={c.disabled}
                      data-index={i}
                      onMouseEnter={() => setActive(i)}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => pick(c)}
                      className={cn(
                        'flex cursor-pointer items-center gap-2 rounded-lg px-3 py-2 text-sm text-zinc-800 dark:text-zinc-200',
                        i === active && 'bg-indigo-50 text-indigo-900 dark:bg-indigo-500/15 dark:text-indigo-100',
                        c.disabled && 'cursor-not-allowed opacity-50',
                      )}
                    >
                      <span className="min-w-0 flex-1 truncate">{c.label || '\u00a0'}</span>
                      {selected && <Check className="size-4 shrink-0 text-indigo-600 dark:text-indigo-300" aria-hidden="true" />}
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>,
          document.body,
        )}
    </>
  );
}

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
        className="mt-0.5 size-4 rounded border-zinc-300 accent-indigo-600 focus:ring-2 focus:ring-indigo-500/30 dark:border-zinc-600"
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
          checked
            ? 'bg-gradient-to-r from-indigo-500 to-violet-500 shadow-inner shadow-indigo-900/20 rtl:bg-gradient-to-l'
            : 'bg-zinc-200 dark:bg-zinc-700',
        )}
      >
        <span
          className={cn(
            'inline-block size-5 rounded-full bg-white shadow-md ring-1 ring-black/5 transition-transform',
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
      className="rounded-xl border border-red-200/80 bg-gradient-to-r from-red-50 to-rose-50/60 px-3.5 py-2.5 text-sm text-red-700 rtl:bg-gradient-to-l dark:border-red-500/30 dark:from-red-500/10 dark:to-rose-500/5 dark:text-red-300"
    >
      {message}
    </div>
  );
}
