import { useEffect, useId, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { cn } from '../../lib/cn';
import { IconButton } from './Button';

function useOverlay(open: boolean, onClose: () => void, panel: React.RefObject<HTMLDivElement | null>) {
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!open) return;
    const previouslyFocused = document.activeElement as HTMLElement | null;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const focusFirst = () => {
      const el = panel.current?.querySelector<HTMLElement>(
        '[autofocus], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]):not([data-close])',
      );
      (el ?? panel.current)?.focus();
    };
    const t = window.setTimeout(focusFirst, 10);

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation();
        onCloseRef.current();
      } else if (e.key === 'Tab' && panel.current) {
        const focusables = panel.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
        );
        if (!focusables.length) return;
        const first = focusables[0];
        const last = focusables[focusables.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener('keydown', onKey);
    return () => {
      window.clearTimeout(t);
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prevOverflow;
      previouslyFocused?.focus?.();
    };
  }, [open, panel]);
}

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  children?: ReactNode;
  footer?: ReactNode;
  size?: 'sm' | 'md' | 'lg' | 'xl';
}

const modalSizes = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl' };

export function Modal({ open, onClose, title, description, children, footer, size = 'md' }: ModalProps) {
  const panel = useRef<HTMLDivElement>(null);
  const titleId = useId();
  useOverlay(open, onClose, panel);
  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4">
      <div className="absolute inset-0 animate-fade-in bg-zinc-950/40 backdrop-blur-[2px]" onClick={onClose} />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={cn(
          'relative flex max-h-[92vh] w-full animate-pop-in flex-col rounded-t-2xl border border-zinc-200 bg-white shadow-2xl outline-none sm:rounded-2xl',
          'dark:border-zinc-800 dark:bg-zinc-900',
          modalSizes[size],
        )}
      >
        <div className="flex items-start justify-between gap-4 px-6 pt-5 pb-3">
          <div>
            <h2 id={titleId} className="text-base font-semibold text-zinc-900 dark:text-white">
              {title}
            </h2>
            {description && <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">{description}</p>}
          </div>
          <IconButton label="Close" data-close onClick={onClose} className="-mt-1 -mr-2">
            <X className="size-4" />
          </IconButton>
        </div>
        {children && <div className="overflow-y-auto px-6 pb-5">{children}</div>}
        {footer && (
          <div className="flex flex-wrap justify-end gap-2 rounded-b-2xl border-t border-zinc-100 bg-zinc-50/60 px-6 py-3.5 dark:border-zinc-800 dark:bg-zinc-900/60">
            {footer}
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}

interface DrawerProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  children?: ReactNode;
  footer?: ReactNode;
  width?: 'md' | 'lg' | 'xl';
}

const drawerWidths = { md: 'max-w-md', lg: 'max-w-xl', xl: 'max-w-3xl' };

/** Right-side slide-over panel. */
export function Drawer({ open, onClose, title, description, children, footer, width = 'lg' }: DrawerProps) {
  const panel = useRef<HTMLDivElement>(null);
  const titleId = useId();
  useOverlay(open, onClose, panel);
  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-50">
      <div className="absolute inset-0 animate-fade-in bg-zinc-950/40 backdrop-blur-[2px]" onClick={onClose} />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={cn(
          'absolute inset-y-0 right-0 flex w-full animate-slide-in-right flex-col border-l border-zinc-200 bg-white shadow-2xl outline-none',
          'dark:border-zinc-800 dark:bg-zinc-900',
          drawerWidths[width],
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b border-zinc-100 px-6 py-4 dark:border-zinc-800">
          <div className="min-w-0">
            <h2 id={titleId} className="truncate text-base font-semibold text-zinc-900 dark:text-white">
              {title}
            </h2>
            {description && <p className="mt-0.5 text-sm text-zinc-500 dark:text-zinc-400">{description}</p>}
          </div>
          <IconButton label="Close" data-close onClick={onClose} className="-mr-2">
            <X className="size-4" />
          </IconButton>
        </div>
        <div className="flex-1 overflow-y-auto px-6 py-5">{children}</div>
        {footer && (
          <div className="flex flex-wrap justify-end gap-2 border-t border-zinc-100 bg-zinc-50/60 px-6 py-3.5 dark:border-zinc-800 dark:bg-zinc-900/60">
            {footer}
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}
