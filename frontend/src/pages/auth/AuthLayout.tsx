import type { ReactNode } from 'react';
import { CalendarCheck, MapPin, ShieldCheck, Wallet } from 'lucide-react';
import { Logo } from '../../components/layout/Sidebar';
import { APP_NAME } from '../../lib/brand';

const points = [
  { icon: MapPin, title: 'Geo-fenced attendance', text: 'Check in from the browser, verified against your office locations.' },
  { icon: CalendarCheck, title: 'Leave that just works', text: 'Balances, approvals and public holidays in one flow.' },
  { icon: Wallet, title: 'Payroll for Pakistan', text: 'FBR income-tax slabs, EOBI and printable payslips, every month.' },
  { icon: ShieldCheck, title: 'Audit everything', text: 'Every sign-in and every change recorded, old value to new.' },
];

export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: ReactNode; children: ReactNode }) {
  return (
    <div className="grid min-h-screen lg:grid-cols-[1fr_1.05fr]">
      <div className="flex flex-col px-6 py-8 sm:px-10">
        <Logo />
        <div className="mx-auto flex w-full max-w-sm flex-1 flex-col justify-center py-10">
          <h1 className="text-2xl font-semibold tracking-tight text-zinc-900 dark:text-white">{title}</h1>
          <p className="mt-1.5 text-sm text-zinc-500 dark:text-zinc-400">{subtitle}</p>
          <div className="mt-8">{children}</div>
        </div>
        <p className="text-xs text-zinc-400">© {new Date().getFullYear()} {APP_NAME}</p>
      </div>

      <div className="relative hidden overflow-hidden bg-zinc-950 lg:block">
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(99,102,241,0.45),transparent_55%),radial-gradient(ellipse_at_bottom_left,rgba(139,92,246,0.35),transparent_50%)]" />
        <div
          className="absolute inset-0 opacity-[0.07]"
          style={{
            backgroundImage:
              'linear-gradient(to right, white 1px, transparent 1px), linear-gradient(to bottom, white 1px, transparent 1px)',
            backgroundSize: '44px 44px',
          }}
        />
        <div className="relative flex h-full flex-col justify-center px-14 py-16 text-white xl:px-20">
          <p className="text-sm font-medium text-indigo-300">HR for growing Pakistani companies</p>
          <h2 className="mt-3 max-w-md text-3xl leading-tight font-semibold tracking-tight xl:text-4xl">
            People, time and payroll — finally in one calm place.
          </h2>
          <ul className="mt-10 grid max-w-lg gap-5">
            {points.map((p) => (
              <li key={p.title} className="flex gap-4">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-white/10 ring-1 ring-white/15">
                  <p.icon className="size-5 text-indigo-200" aria-hidden="true" />
                </div>
                <div>
                  <p className="font-medium">{p.title}</p>
                  <p className="text-sm text-zinc-400">{p.text}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
