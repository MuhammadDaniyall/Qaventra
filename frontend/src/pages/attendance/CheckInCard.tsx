import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Clock, LogIn, LogOut, MapPin, MapPinOff } from 'lucide-react';
import { attendanceApi } from '../../api/attendance';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { Button, Card, Skeleton, StatusBadge, useToast } from '../../components/ui';
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
      <Card className={cn('p-6 text-sm text-zinc-500', className)}>
        Check-in is unavailable: {errorMessage(today.error)}
      </Card>
    );
  }

  const t = today.data!;
  const worked = record ? (record.checkOut ? record.workedMinutes : minutesSince(record.checkIn, now)) : 0;

  return (
    <Card className={cn('relative overflow-hidden', className)}>
      <div className="relative flex flex-col gap-6 p-6 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0">
          <p className="text-sm font-medium text-zinc-500 dark:text-zinc-400">{formatDate(t.date)}</p>
          <p className="mt-1 text-4xl font-semibold tracking-tight text-zinc-900 tabular-nums dark:text-white">
            {pad(now.getHours() % 12 === 0 ? 12 : now.getHours() % 12)}:{pad(now.getMinutes())}
            <span className="ml-1.5 text-lg font-medium text-zinc-400">{now.getHours() >= 12 ? 'PM' : 'AM'}</span>
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
            </div>
          )}
          {mode === 'done' ? (
            <div className="inline-flex items-center gap-2 rounded-xl bg-emerald-50 px-5 py-3 text-sm font-medium text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300">
              <CheckCircle2 className="size-5" /> You're done for today
            </div>
          ) : (
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
