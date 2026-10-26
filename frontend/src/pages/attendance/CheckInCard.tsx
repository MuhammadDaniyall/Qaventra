import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Clock, Coffee, LogIn, LogOut, MapPin, MapPinOff, Play } from 'lucide-react';
import { attendanceApi } from '../../api/attendance';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { Button, Card, Skeleton, StatusBadge, useToast } from '../../components/ui';
import type { BreakKind } from '../../types';
import { tx } from '../../lib/i18n';
import { cn } from '../../lib/cn';
import { formatDate, formatMinutes, formatTime, pad } from '../../lib/format';
import { getCurrentPosition, type Coords } from '../../lib/geo';

function useClock() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const t = window.setInterval(() => setNow(new Date()), 1000 * 15);
    return () => window.clearInterval(t);
  }, []);
  return now;
}

/** Minutes between a company-local "YYYY-MM-DDTHH:mm:ss" and now (browser time, assumed same zone). */
function minutesSince(localDateTime: string, now: Date): number {
  const d = new Date(localDateTime.replace(/[zZ]$/, ''));
  return Number.isNaN(d.getTime()) ? 0 : Math.max(0, Math.round((now.getTime() - d.getTime()) / 60000));
}

export function CheckInCard({ className }: { className?: string }) {
  const toast = useToast();
  const qc = useQueryClient();
  const now = useClock();
  const [geoError, setGeoError] = useState<string | null>(null);
  const today = useQuery({ queryKey: qk.attendanceToday, queryFn: attendanceApi.today });

  const record = today.data?.record ?? null;
  const mode: 'in' | 'out' | 'done' = !record ? 'in' : record.checkOut ? 'done' : 'out';

  const punch = useMutation({
    mutationFn: async () => {
      setGeoError(null);
      let coords: Coords | null = null;
      try {
        coords = await getCurrentPosition();
      } catch (err) {
        const msg = errorMessage(err);
        if (today.data?.locationRequired) {
          setGeoError(msg);
          throw Object.assign(new Error(msg), { geo: true });
        }
      }
      const body = { latitude: coords?.latitude ?? null, longitude: coords?.longitude ?? null, source: 'Web' as const };
      return mode === 'in' ? attendanceApi.checkIn(body) : attendanceApi.checkOut(body);
    },
    onSuccess: (r) => {
      toast.success(mode === 'in' ? `Checked in at ${formatTime(r.checkIn)}` : `Checked out at ${formatTime(r.checkOut)}`,
        r.office ? `Location: ${r.office}` : undefined);
      qc.invalidateQueries({ queryKey: qk.attendance });
      qc.invalidateQueries({ queryKey: qk.dashboard });
    },
    onError: (err) => {
      if ((err as { geo?: boolean }).geo) return;
      toast.error(err);
    },
  });

  const onBreak = today.data?.breaks?.find((b) => !b.end) ?? null;
  const breakAction = useMutation({
    mutationFn: (kind: BreakKind | null) => (kind ? attendanceApi.startBreak(kind) : attendanceApi.endBreak()),
    onSuccess: (b) => {
      toast.success(b.end ? `${tx('Break ended')} · ${formatMinutes(b.minutes)}` : `${tx(b.kind)} ${tx('break started')}`);
      qc.invalidateQueries({ queryKey: qk.attendanceToday });
    },
    onError: (err) => toast.error(err),
  });

  if (today.isLoading) {
    return (
      <Card className={cn('p-6', className)}>
        <Skeleton className="h-5 w-40" />
        <Skeleton className="mt-4 h-10 w-56" />
        <Skeleton className="mt-6 h-12 w-full max-w-xs" />
      </Card>
    );
  }
  if (today.isError) {
    return (
      <Card className={cn('p-6 text-sm text-zinc-500 dark:text-zinc-400', className)}>
        Check-in is unavailable: {errorMessage(today.error)}
      </Card>
    );
  }

  const t = today.data!;
  const worked = record ? (record.checkOut ? record.workedMinutes : minutesSince(record.checkIn, now)) : 0;
  const breakTotal = (t.breaks ?? []).reduce((sum, b) => sum + (b.end ? b.minutes : minutesSince(b.start, now)), 0);

  return (
    <Card className={cn('relative overflow-hidden', className)}>
      <div className="relative flex flex-col gap-6 p-6 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0">
          <p className="text-sm font-medium text-zinc-500 dark:text-zinc-400">{formatDate(t.date)}</p>
          <p className="mt-1 text-4xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">
            {pad(now.getHours() % 12 === 0 ? 12 : now.getHours() % 12)}:{pad(now.getMinutes())}
            <span className="ms-1.5 text-lg font-medium text-zinc-500 dark:text-zinc-400">{now.getHours() >= 12 ? 'PM' : 'AM'}</span>
          </p>
          <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-zinc-500 dark:text-zinc-400">
            {t.shiftName && (
              <span className="inline-flex items-center gap-1.5">
                <Clock className="size-4" />
                {t.shiftName} · {formatTime(t.shiftStart)} – {formatTime(t.shiftEnd)}
              </span>
            )}
            <span className="inline-flex items-center gap-1.5">
              {t.locationRequired ? <MapPin className="size-4" /> : <MapPinOff className="size-4" />}
              {t.locationRequired ? 'Location check on' : 'No location check'}
            </span>
          </div>
        </div>

        <div className="flex flex-col items-stretch gap-3 md:items-end">
          {record && (
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm md:justify-end">
              <StatusBadge status={record.status} />
              <span className="text-zinc-600 dark:text-zinc-300">
                In <strong className="font-semibold tabular-nums">{formatTime(record.checkIn)}</strong>
              </span>
              {record.checkOut && (
                <span className="text-zinc-600 dark:text-zinc-300">
                  Out <strong className="font-semibold tabular-nums">{formatTime(record.checkOut)}</strong>
                </span>
              )}
              <span className="text-zinc-500 dark:text-zinc-400">{formatMinutes(worked)} worked</span>
              {breakTotal > 0 && (
                <span className={cn('text-zinc-500 dark:text-zinc-400', t.allowedBreakMinutes > 0 && breakTotal > t.allowedBreakMinutes && 'font-medium text-amber-700 dark:text-amber-300')}>
                  {formatMinutes(breakTotal)} {tx('break')}
                  {t.allowedBreakMinutes > 0 && ` / ${formatMinutes(t.allowedBreakMinutes)}`}
                </span>
              )}
            </div>
          )}
          {mode === 'done' ? (
            <div className="inline-flex items-center gap-2 rounded-xl bg-emerald-50 px-5 py-3 text-sm font-medium text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300">
              <CheckCircle2 className="size-5" /> You're done for today
            </div>
          ) : onBreak ? (
            <Button
              size="lg"
              variant="primary"
              className="h-14 min-w-52 rounded-xl text-base"
              loading={breakAction.isPending}
              icon={<Play className="size-5" />}
              onClick={() => breakAction.mutate(null)}
            >
              {`${tx('End break')} · ${tx(onBreak.kind)} · ${minutesSince(onBreak.start, now) < 1 ? tx('just started') : formatMinutes(minutesSince(onBreak.start, now))}`}
            </Button>
          ) : (
            <div className="flex flex-wrap items-center gap-2 md:justify-end">
            {mode === 'out' && (
              <BreakMenu loading={breakAction.isPending} onPick={(k) => breakAction.mutate(k)} />
            )}
            <Button
              size="lg"
              variant={mode === 'in' ? 'primary' : 'danger'}
              className="h-14 min-w-52 rounded-xl text-base"
              loading={punch.isPending}
              icon={mode === 'in' ? <LogIn className="size-5" /> : <LogOut className="size-5" />}
              onClick={() => punch.mutate()}
            >
              {punch.isPending ? (mode === 'in' ? 'Checking in…' : 'Checking out…') : mode === 'in' ? 'Check in' : 'Check out'}
            </Button>
            </div>
          )}
        </div>
      </div>
      {geoError && (
        <div
          role="alert"
          className="relative flex items-start gap-2 border-t border-amber-200 bg-amber-50 px-6 py-3 text-sm text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-200"
        >
          <MapPinOff className="mt-0.5 size-4 shrink-0" />
          <span>{geoError}</span>
        </div>
      )}
    </Card>
  );
}

const BREAKS: BreakKind[] = ['Lunch', 'Prayer', 'Tea', 'Personal'];

function BreakMenu({ loading, onPick }: { loading: boolean; onPick: (kind: BreakKind) => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);
  return (
    <div ref={ref} className="relative">
      <Button
        size="lg"
        variant="secondary"
        className="h-14 rounded-xl text-base"
        loading={loading}
        icon={<Coffee className="size-5" />}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        Break
      </Button>
      {open && (
        <div
          role="menu"
          className="absolute end-0 z-40 mt-2 w-44 animate-pop-in overflow-hidden rounded-xl border border-zinc-200 bg-white py-1.5 shadow-lg dark:border-zinc-700 dark:bg-zinc-900"
        >
          {BREAKS.map((k) => (
            <button
              key={k}
              type="button"
              role="menuitem"
              className="block w-full px-4 py-2 text-start text-sm text-zinc-700 hover:bg-zinc-50 dark:text-zinc-200 dark:hover:bg-zinc-800"
              onClick={() => {
                setOpen(false);
                onPick(k);
              }}
            >
              {tx(k)}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
