import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, ClipboardCheck, DoorOpen, FileText, Inbox, Wallet, type LucideIcon } from 'lucide-react';
import { qk } from '../../api/keys';
import { notificationsApi, type AppNotification, type NotificationKind } from '../../api/notifications';
import { cn } from '../../lib/cn';
import { formatDateTimeUtc } from '../../lib/format';
import { Button, Switch } from '../ui';
import { tx } from '../../lib/i18n';
import { usePush } from '../../lib/push';

const ICONS: Record<NotificationKind, LucideIcon> = {
  ApprovalNeeded: Inbox,
  RequestDecided: ClipboardCheck,
  Separation: DoorOpen,
  Letter: FileText,
  Payslip: Wallet,
};

/** "5 min ago", "3 h ago", then the date. */
function ago(utc: string) {
  const ms = Date.now() - Date.parse(/[zZ]$/.test(utc) ? utc : `${utc}Z`);
  const min = Math.round(ms / 60_000);
  if (min < 1) return 'just now';
  if (min < 60) return `${min} min ago`;
  if (min < 24 * 60) return `${Math.round(min / 60)} h ago`;
  return formatDateTimeUtc(utc);
}

/** Bell with an unread count; refreshes every minute and when the tab regains focus. */
export function NotificationBell() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  const feed = useQuery({ queryKey: qk.notifications, queryFn: notificationsApi.feed, refetchInterval: 60_000 });

  // A new notification usually means the inbox or a list changed too.
  const unread = feed.data?.unread ?? 0;
  const seen = useRef(unread);
  useEffect(() => {
    if (unread > seen.current) {
      qc.invalidateQueries({ queryKey: qk.approvals });
      qc.invalidateQueries({ queryKey: qk.leave });
      qc.invalidateQueries({ queryKey: qk.expenses });
    }
    seen.current = unread;
  }, [unread, qc]);

  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent | KeyboardEvent) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !box.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', close);
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', close);
    };
  }, [open]);

  const refresh = () => qc.invalidateQueries({ queryKey: qk.notifications });
  const read = useMutation({ mutationFn: (id: string) => notificationsApi.read(id), onSuccess: refresh });
  const readAll = useMutation({ mutationFn: notificationsApi.readAll, onSuccess: refresh });

  const openItem = (n: AppNotification) => {
    if (!n.readAtUtc) read.mutate(n.id);
    setOpen(false);
    if (n.link) navigate(n.link);
  };

  return (
    <div ref={box} className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-label={unread ? `Notifications, ${unread} unread` : 'Notifications'}
        aria-expanded={open}
        className="relative inline-flex size-9 items-center justify-center rounded-lg text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 focus-visible:outline-2 focus-visible:outline-accent-500 dark:text-zinc-400 dark:hover:bg-zinc-800 dark:hover:text-white"
      >
        <Bell className="size-[18px]" />
        {unread > 0 && (
          <span className="absolute top-1 end-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-semibold text-white tabular-nums">
            {unread > 99 ? '99+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute end-0 z-40 mt-2 w-[min(24rem,calc(100vw-2rem))] animate-pop-in overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex items-center justify-between border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
            <p className="text-sm font-semibold text-zinc-900 dark:text-white">{tx('Notifications')}</p>
            {unread > 0 && (
              <Button variant="ghost" size="sm" icon={<CheckCheck className="size-3.5" />} loading={readAll.isPending} onClick={() => readAll.mutate()}>
                {tx('Mark all read')}
              </Button>
            )}
          </div>
          <ul className="max-h-[60vh] divide-y divide-zinc-100 overflow-y-auto dark:divide-zinc-800">
            {!feed.data?.items.length ? (
              <li className="px-4 py-10 text-center text-sm text-zinc-500 dark:text-zinc-400">{tx("You're all caught up.")}</li>
            ) : (
              feed.data.items.map((n) => {
                const Icon = ICONS[n.kind] ?? Bell;
                return (
                  <li key={n.id}>
                    <button
                      type="button"
                      onClick={() => openItem(n)}
                      className={cn(
                        'flex w-full gap-3 px-4 py-3 text-start transition-colors hover:bg-zinc-50 dark:hover:bg-zinc-800/60',
                        !n.readAtUtc && 'bg-accent-50/50 dark:bg-accent-500/5',
                      )}
                    >
                      <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg bg-zinc-100 text-zinc-500 dark:text-zinc-400 dark:bg-zinc-800">
                        <Icon className="size-4" aria-hidden="true" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className={cn('block text-sm', n.readAtUtc ? 'text-zinc-700 dark:text-zinc-300' : 'font-medium text-zinc-900 dark:text-white')}>
                          {n.title}
                        </span>
                        {n.body && <span className="line-clamp-2 block text-xs whitespace-pre-line text-zinc-500 dark:text-zinc-400">{n.body}</span>}
                        <span className="mt-0.5 block text-[11px] text-zinc-500 dark:text-zinc-400">{ago(n.createdAtUtc)}</span>
                      </span>
                      {!n.readAtUtc && <span className="mt-2 size-2 shrink-0 rounded-full bg-accent-500" aria-label="Unread" />}
                    </button>
                  </li>
                );
              })
            )}
          </ul>
          <PushSwitch />
        </div>
      )}
    </div>
  );
}

/** Turns phone/browser notifications on for this device; hidden where the browser has no push support. */
function PushSwitch() {
  const push = usePush();
  if (push.state === 'unsupported') return null;
  return (
    <div className="border-t border-zinc-100 px-4 py-3 dark:border-zinc-800">
      <Switch
        checked={push.state === 'on'}
        disabled={push.state === 'loading' || push.state === 'blocked'}
        onChange={(on) => (on ? push.enable() : push.disable())}
        label="Notifications on this device"
        description={
          push.state === 'blocked'
            ? 'Blocked in this browser. Allow notifications for this site in the browser settings.'
            : (push.error ?? 'Get a phone or desktop alert when something needs you.')
        }
      />
    </div>
  );
}
