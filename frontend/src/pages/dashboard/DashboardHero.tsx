import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { CalendarCheck, FileBarChart, Inbox, Sparkles, UserPlus, Wallet, type LucideIcon } from 'lucide-react';
import { cn } from '../../lib/cn';
import { tx, txf } from '../../lib/i18n';
import type { Dashboard } from '../../types';

/**
 * Home header: a full brand-gradient banner with the greeting, quick actions and (for approvers) today's attendance
 * as a ring. Everything sits on the gradient in white, so it reads the same in light and dark mode.
 */
export function DashboardHero({
  greeting,
  subtitle,
  d,
  loading,
  showToday,
  links,
  actions,
  todayTo,
}: {
  greeting: string;
  subtitle: string;
  d: Dashboard | undefined;
  loading: boolean;
  /** Approvers see today's attendance ring. */
  showToday: boolean;
  links: { to: string; label: string; icon: LucideIcon; count?: number }[];
  /** Extra buttons (e.g. the New request menu). */
  actions?: ReactNode;
  /** Where the attendance ring leads (the daily attendance list). */
  todayTo?: string;
}) {
  return (
    <section className="relative isolate mb-6 animate-fade-in rounded-3xl bg-brand p-5 text-white shadow-[0_30px_60px_-30px_rgb(79_70_229/0.75)] sm:p-7 lg:p-8">
      {/* Clipped separately so the New request menu can open past the banner. */}
      <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden rounded-3xl" aria-hidden="true">
        <div className="absolute -top-28 -end-16 size-96 rounded-full bg-white/20 blur-3xl motion-safe:animate-glow" />
        <div
          className="absolute -bottom-32 -start-10 size-96 rounded-full bg-fuchsia-300/30 blur-3xl motion-safe:animate-glow"
          style={{ animationDelay: '-7s' }}
        />
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_1px_1px,rgb(255_255_255/0.18)_1px,transparent_0)] [background-size:22px_22px] [mask-image:linear-gradient(to_bottom,black,transparent_85%)]" />
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
        <div className="min-w-0">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-xs font-medium ring-1 ring-white/25 backdrop-blur">
            <Sparkles className="size-3.5" aria-hidden="true" /> {tx('Your AI Team is on duty')}
          </span>
          <h1 className="mt-3 text-[28px] leading-tight font-semibold tracking-tight sm:text-4xl">{greeting}</h1>
          <p className="mt-1.5 text-[15px] text-white/80">{subtitle}</p>

          {(links.length > 0 || actions) && (
            <div className="mt-6 flex flex-wrap items-center gap-2">
              {actions}
              {links.map((l) => (
                <Link
                  key={l.to}
                  to={l.to}
                  className="inline-flex h-10 items-center gap-2 rounded-xl bg-white/15 px-3.5 text-sm font-medium ring-1 ring-white/25 backdrop-blur transition-all hover:bg-white/25 active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:h-9"
                >
                  <l.icon className="size-4" aria-hidden="true" />
                  {tx(l.label)}
                  {!!l.count && (
                    <span className="min-w-5 rounded-full bg-white px-1.5 text-center text-[11px] leading-5 font-semibold text-indigo-700 tabular-nums">
                      {l.count}
                    </span>
                  )}
                </Link>
              ))}
            </div>
          )}
        </div>

        {showToday &&
          (todayTo ? (
            <Link
              to={todayTo}
              className="block rounded-2xl transition-transform hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
            >
              <TodayPanel d={d} loading={loading} />
            </Link>
          ) : (
            <TodayPanel d={d} loading={loading} />
          ))}
      </div>
    </section>
  );
}

/** Today's attendance: a ring for the share checked in, and the split beside it. */
function TodayPanel({ d, loading }: { d: Dashboard | undefined; loading: boolean }) {
  const headcount = d?.headcount ?? 0;
  const present = d?.presentToday ?? 0;
  const late = d?.lateToday ?? 0;
  const leave = d?.onLeaveToday ?? 0;
  const notIn = Math.max(0, headcount - present - leave);
  const share = headcount ? present / headcount : 0;
  const pct = Math.round(share * 100);
  const r = 46;
  const circ = 2 * Math.PI * r;

  const rows: [string, number, string][] = [
    ['On time', Math.max(0, present - late), 'bg-emerald-300'],
    ['Late', late, 'bg-amber-300'],
    ['On leave', leave, 'bg-sky-300'],
    ['Not in yet', notIn, 'bg-white/40'],
  ];

  return (
    <div className="flex items-center gap-5 rounded-2xl bg-white/10 p-4 ring-1 ring-white/20 backdrop-blur-md sm:p-5 lg:w-[22rem]">
      <div className="relative size-28 shrink-0" role="img" aria-label={txf('{pct}% checked in today', { pct })}>
        <svg viewBox="0 0 112 112" className="size-full -rotate-90">
          <circle cx="56" cy="56" r={r} fill="none" stroke="rgb(255 255 255 / 0.18)" strokeWidth="10" />
          <circle
            cx="56"
            cy="56"
            r={r}
            fill="none"
            stroke="white"
            strokeWidth="10"
            strokeLinecap="round"
            strokeDasharray={circ}
            strokeDashoffset={loading ? circ : circ * (1 - share)}
            className="transition-[stroke-dashoffset] duration-1000 ease-out"
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-2xl font-semibold tabular-nums">{loading ? '–' : `${pct}%`}</span>
          <span className="text-[11px] text-white/75">{tx('checked in')}</span>
        </div>
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-xs font-medium text-white/75">{tx('Today')}</p>
        <p className="text-sm font-semibold">
          {txf('{present} of {total} in', { present, total: headcount })}
        </p>
        <ul className="mt-2.5 space-y-1.5 text-sm">
          {rows.map(([label, value, dot]) => (
            <li key={label} className="flex items-center gap-2">
              <span className={cn('size-2 shrink-0 rounded-full', dot)} aria-hidden="true" />
              <span className="truncate text-white/85">{tx(label)}</span>
              <span className="ms-auto font-semibold tabular-nums">{value}</span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

/** Quick links for the hero, by role. */
export function heroLinks({ hr, approver, payroll, pending }: { hr: boolean; approver: boolean; payroll: boolean; pending: number }) {
  const links: { to: string; label: string; icon: LucideIcon; count?: number }[] = [];
  if (hr) links.push({ to: '/employees/new', label: 'Add employee', icon: UserPlus });
  if (approver) links.push({ to: '/approvals', label: 'Approvals', icon: Inbox, count: pending });
  if (hr) links.push({ to: '/attendance/daily', label: 'Daily attendance', icon: CalendarCheck });
  if (payroll) links.push({ to: '/payroll', label: 'Payroll', icon: Wallet });
  if (hr) links.push({ to: '/reports', label: 'Reports', icon: FileBarChart });
  return links;
}
