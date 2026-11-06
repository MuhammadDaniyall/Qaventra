import { useEffect, useId, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { cn } from '../../lib/cn';
import { IconButton } from './Button';
import { tx } from '../../lib/i18n';

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
      // With a dialog open on top of another (e.g. "Write with AI" over a form), only the top one reacts.
      const dialogs = document.querySelectorAll('[role="dialog"]');
      if (panel.current && dialogs.length > 1 && dialogs[dialogs.length - 1] !== panel.current) return;
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
  /** Hide the close button, for dialogs that must be completed. */
  hideClose?: boolean;
}

const modalSizes = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl', xl: 'max-w-4xl' };

export function Modal({ open, onClose, title, description, children, footer, size = 'md', hideClose = false }: ModalProps) {
  const panel = useRef<HTMLDivElement>(null);
  const titleId = useId();
  useOverlay(open, onClose, panel);
  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4">
      <div className="absolute inset-0 animate-fade-in bg-zinc-950/50 backdrop-blur-sm" onClick={onClose} />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={cn(
          // Phones: a bottom sheet that slides up, clears the home indicator and keeps the buttons in thumb reach.
          'relative flex max-h-[92dvh] w-full animate-sheet-up flex-col overflow-hidden rounded-t-3xl border border-b-0 border-zinc-200/80 bg-white shadow-2xl outline-none',
          'pb-[env(safe-area-inset-bottom)] sm:animate-pop-in sm:rounded-2xl sm:border-b sm:pb-0',
          'dark:border-white/10 dark:bg-zinc-900',
          modalSizes[size],
        )}
      >
        <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-indigo-500/60 to-transparent" aria-hidden="true" />
        <div className="mx-auto mt-2.5 h-1.5 w-10 shrink-0 rounded-full bg-zinc-300 sm:hidden dark:bg-zinc-700" aria-hidden="true" />
        <div className="flex items-start justify-between gap-4 px-5 pt-3 pb-3 sm:px-6 sm:pt-5">
          <div>
            <h2 id={titleId} className="text-base font-semibold text-zinc-900 dark:text-white">
              {tx(title)}
            </h2>
            {description && <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">{tx(description)}</p>}
          </div>
          {!hideClose && (
            <IconButton label="Close" data-close onClick={onClose} className="-mt-1 -me-2">
              <X className="size-4" />
            </IconButton>
          )}
        </div>
        {children && <div className="overscroll-contain overflow-y-auto px-5 pb-5 sm:px-6">{children}</div>}
        {footer && (
          <div className="flex flex-wrap justify-end gap-2 border-t border-zinc-100 bg-zinc-50/60 px-5 py-3.5 max-sm:[&>*]:min-h-11 max-sm:[&>*]:flex-1 sm:px-6 dark:border-white/[0.06] dark:bg-white/[0.02]">
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
      <div className="absolute inset-0 animate-fade-in bg-zinc-950/50 backdrop-blur-sm" onClick={onClose} />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={cn(
          'absolute inset-y-0 end-0 flex w-full animate-slide-in-right flex-col rtl:animate-slide-in-left border-s border-zinc-200/80 bg-white pt-[env(safe-area-inset-top)] pb-[env(safe-area-inset-bottom)] shadow-2xl outline-none',
          'dark:border-white/10 dark:bg-zinc-900',
          drawerWidths[width],
        )}
      >
        <div className="flex items-start justify-between gap-4 border-b border-zinc-100 px-6 py-4 dark:border-zinc-800">
          <div className="min-w-0">
            <h2 id={titleId} className="truncate text-base font-semibold text-zinc-900 dark:text-white">
              {tx(title)}
            </h2>
            {description && <p className="mt-0.5 text-sm text-zinc-500 dark:text-zinc-400">{tx(description)}</p>}
          </div>
          <IconButton label="Close" data-close onClick={onClose} className="-me-2">
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
