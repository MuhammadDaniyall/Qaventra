import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react';
import { CheckCircle2, Info, X, XCircle } from 'lucide-react';
import { cn } from '../../lib/cn';
import { ApiError, errorMessage } from '../../api/client';

type ToastKind = 'success' | 'error' | 'info';

interface ToastItem {
  id: number;
  kind: ToastKind;
  title: string;
  description?: string;
}

interface ToastApi {
  success: (title: string, description?: string) => void;
  error: (titleOrError: unknown, description?: string) => void;
  info: (title: string, description?: string) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => setToasts((t) => t.filter((x) => x.id !== id)), []);

  const push = useCallback(
    (kind: ToastKind, title: string, description?: string) => {
      const id = nextId.current++;
      setToasts((t) => [...t.slice(-3), { id, kind, title, description }]);
      window.setTimeout(() => dismiss(id), kind === 'error' ? 7000 : 4000);
    },
    [dismiss],
  );

  const api = useMemo<ToastApi>(
    () => ({
      success: (title, description) => push('success', title, description),
      info: (title, description) => push('info', title, description),
      error: (titleOrError, description) => {
        if (titleOrError instanceof ApiError && titleOrError.handledGlobally) return;
        push('error', typeof titleOrError === 'string' ? titleOrError : errorMessage(titleOrError), description);
      },
    }),
    [push],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        aria-live="polite"
        className="no-print pointer-events-none fixed inset-x-0 bottom-0 z-[60] flex flex-col items-center gap-2 p-4 sm:items-end"
      >
        {toasts.map((t) => (
          <div
            key={t.id}
            role={t.kind === 'error' ? 'alert' : 'status'}
            className="pointer-events-auto flex w-full max-w-sm animate-pop-in items-start gap-3 rounded-xl border border-zinc-200 bg-white p-3.5 shadow-lg dark:border-zinc-700 dark:bg-zinc-900"
          >
            {t.kind === 'success' ? (
              <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-500" />
            ) : t.kind === 'error' ? (
              <XCircle className="mt-0.5 size-5 shrink-0 text-red-500" />
            ) : (
              <Info className="mt-0.5 size-5 shrink-0 text-accent-500" />
            )}
            <div className="min-w-0 flex-1">
              <p className={cn('text-sm font-medium text-zinc-900 dark:text-zinc-100')}>{t.title}</p>
              {t.description && <p className="mt-0.5 text-sm text-zinc-500 dark:text-zinc-400">{t.description}</p>}
            </div>
            <button
              type="button"
              onClick={() => dismiss(t.id)}
              aria-label="Dismiss"
              className="rounded-md p-0.5 text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:text-zinc-200"
            >
              <X className="size-4" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside ToastProvider');
  return ctx;
}
