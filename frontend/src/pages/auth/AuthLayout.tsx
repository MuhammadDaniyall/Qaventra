import { useEffect, useState, type ReactNode } from 'react';
import { Bot, CalendarCheck, CheckCircle2, Fingerprint, Lock, ShieldCheck, Sparkles, Wallet } from 'lucide-react';
import { Logo } from '../../components/layout/Logo';
import { LanguagePicker } from '../../components/ui/LanguagePicker';
import { APP_NAME } from '../../lib/brand';
import { cn } from '../../lib/cn';
import { tx } from '../../lib/i18n';

/** What the AI Team "says" on the showcase, one line at a time. */
const ROBOT_LINES = [
  { who: 'Paro · Payroll checker', text: 'October payroll checked: 45 payslips, 3 to review before release.' },
  { who: 'Atif · Attendance watcher', text: '2 missed check-outs yesterday. Reminders drafted for approval.' },
  { who: 'Sana · CV screener', text: '6 new applicants scored. Ayesha fits the Accountant role at 86/100.' },
  { who: 'Danish · Document watcher', text: '3 CNICs expire this month. Renewal reminders are ready.' },
];

/** Types each robot line out letter by letter, then moves to the next. Shows the whole line when motion is reduced. */
function useTypewriter() {
  const [line, setLine] = useState(0);
  const [chars, setChars] = useState(0);
  const reduce = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
  const full = ROBOT_LINES[line].text;
  useEffect(() => {
    if (reduce) {
      const t = setTimeout(() => setLine((l) => (l + 1) % ROBOT_LINES.length), 4500);
      return () => clearTimeout(t);
    }
    const t = chars < full.length
      ? setTimeout(() => setChars((c) => c + 1), 28)
      : setTimeout(() => {
          setChars(0);
          setLine((l) => (l + 1) % ROBOT_LINES.length);
        }, 2600);
    return () => clearTimeout(t);
  }, [chars, full, reduce]);
  return { who: ROBOT_LINES[line].who, text: reduce ? full : full.slice(0, chars), typing: !reduce && chars < full.length };
}

function Glass({ className, children, style }: { className?: string; children: ReactNode; style?: React.CSSProperties }) {
  return (
    <div
      style={style}
      className={cn(
        'rounded-2xl border border-white/15 bg-white/[0.07] p-4 shadow-2xl shadow-indigo-950/40 ring-1 ring-white/5 backdrop-blur-xl',
        className,
      )}
    >
      {children}
    </div>
  );
}

/** The right half: a living picture of the product, with the AI Team at work. */
function Showcase() {
  const robot = useTypewriter();
  return (
    <div className="relative hidden overflow-hidden bg-[#070614] lg:block">
      {/* Aurora */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -top-1/4 -right-1/4 size-[70%] rounded-full bg-indigo-600/40 blur-[110px] motion-safe:animate-aurora" />
        <div
          className="absolute -bottom-1/4 -left-1/4 size-[65%] rounded-full bg-fuchsia-600/30 blur-[120px] motion-safe:animate-aurora"
          style={{ animationDelay: '-6s' }}
        />
        <div
          className="absolute top-1/3 left-1/3 size-[45%] rounded-full bg-emerald-500/20 blur-[110px] motion-safe:animate-aurora"
          style={{ animationDelay: '-12s' }}
        />
      </div>
      {/* Grid that fades out towards the edges */}
      <div
        className="pointer-events-none absolute inset-0 opacity-[0.09]"
        style={{
          backgroundImage: 'linear-gradient(to right, white 1px, transparent 1px), linear-gradient(to bottom, white 1px, transparent 1px)',
          backgroundSize: '48px 48px',
          maskImage: 'radial-gradient(ellipse at center, black 30%, transparent 75%)',
          WebkitMaskImage: 'radial-gradient(ellipse at center, black 30%, transparent 75%)',
        }}
      />

      <div className="relative flex h-full flex-col justify-center px-12 py-14 text-white xl:px-20">
        <span className="inline-flex w-fit items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-medium text-indigo-200 backdrop-blur motion-safe:animate-rise">
          <Sparkles className="size-3.5 text-amber-300" /> {tx('People + payroll + an AI team')}
        </span>
        <h2
          className="mt-5 max-w-lg text-4xl leading-[1.1] font-semibold tracking-tight motion-safe:animate-rise xl:text-5xl"
          style={{ animationDelay: '80ms' }}
        >
          {tx('HR that works')}{' '}
          <span className="bg-gradient-to-r from-indigo-300 via-fuchsia-300 to-emerald-300 bg-clip-text text-transparent">{tx('while you sleep.')}</span>
        </h2>
        <p className="mt-4 max-w-md text-[15px] leading-relaxed text-zinc-300 motion-safe:animate-rise" style={{ animationDelay: '160ms' }}>
          {tx('Attendance, leave and Pakistani payroll in one place, with AI robots that check the work and prepare it. People approve what matters.')}
        </p>

        {/* Product pieces, floating */}
        <div className="relative mt-10 h-[300px] max-w-xl">
          <Glass className="absolute top-0 left-0 w-[78%] motion-safe:animate-rise" style={{ animationDelay: '240ms' }}>
            <div className="flex items-center gap-3">
              <span className="relative flex size-10 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-fuchsia-500">
                <Bot className="size-5" />
                <span className="absolute -top-0.5 -right-0.5 size-2.5 rounded-full bg-emerald-400 ring-2 ring-[#1b1840]" />
              </span>
              <div className="min-w-0">
                <p className="text-sm font-semibold">{robot.who}</p>
                <p className="text-[11px] text-emerald-300">{tx('AI Team · working')}</p>
              </div>
            </div>
            <p className="mt-3 min-h-[2.75rem] text-sm leading-relaxed text-zinc-200">
              {robot.text}
              {robot.typing && <span className="ms-0.5 inline-block h-4 w-[2px] translate-y-0.5 bg-indigo-300 motion-safe:animate-blink" />}
            </p>
          </Glass>

          <div className="absolute top-[118px] right-0 w-[52%] motion-safe:animate-float" style={{ animationDelay: '-2s' }}>
            <Glass className="motion-safe:animate-rise" style={{ animationDelay: '360ms' }}>
              <div className="flex items-center justify-between">
                <p className="text-xs text-zinc-300">{tx('Net payroll')}</p>
                <Wallet className="size-4 text-indigo-200" />
              </div>
              <p className="mt-1 text-2xl font-semibold tracking-tight">PKR 8.42M</p>
              <div className="mt-3 flex h-10 items-end gap-1.5">
                {[38, 52, 44, 61, 57, 72, 66, 84].map((h, i) => (
                  <span
                    key={i}
                    className="flex-1 rounded-sm bg-gradient-to-t from-indigo-500/60 to-fuchsia-400/80"
                    style={{ height: `${h}%` }}
                  />
                ))}
              </div>
            </Glass>
          </div>

          <div className="absolute top-[190px] left-[6%] w-[46%] motion-safe:animate-float" style={{ animationDelay: '-4.5s' }}>
            <Glass className="motion-safe:animate-rise" style={{ animationDelay: '480ms' }}>
              <div className="flex items-center gap-3">
                <span className="flex size-9 items-center justify-center rounded-xl bg-emerald-500/20 ring-1 ring-emerald-400/30">
                  <CheckCircle2 className="size-5 text-emerald-300" />
                </span>
                <div>
                  <p className="text-sm font-medium">{tx('Leave approved')}</p>
                  <p className="text-[11px] text-zinc-400">{tx('Synced to attendance and pay')}</p>
                </div>
              </div>
            </Glass>
          </div>
        </div>

        <ul className="mt-6 flex flex-wrap gap-x-6 gap-y-2 text-xs text-zinc-400 motion-safe:animate-rise" style={{ animationDelay: '600ms' }}>
          <li className="flex items-center gap-1.5"><Fingerprint className="size-3.5 text-indigo-300" /> {tx('Geo-fenced and machine attendance')}</li>
          <li className="flex items-center gap-1.5"><CalendarCheck className="size-3.5 text-indigo-300" /> {tx('FBR tax, EOBI and PF')}</li>
          <li className="flex items-center gap-1.5"><ShieldCheck className="size-3.5 text-indigo-300" /> {tx('Every change audited')}</li>
        </ul>
      </div>
    </div>
  );
}

export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: ReactNode; children: ReactNode }) {
  return (
    <div className="grid min-h-screen bg-white lg:grid-cols-[1fr_1.15fr] dark:bg-zinc-950">
      <div className="relative flex flex-col overflow-hidden px-6 py-8 sm:px-10">
        {/* A soft glow behind the form; the whole picture on phones, where the showcase is hidden. */}
        <div className="pointer-events-none absolute -top-40 left-1/2 size-[520px] -translate-x-1/2 rounded-full bg-indigo-400/20 blur-[100px] lg:-left-40 lg:translate-x-0 dark:bg-indigo-500/15" />
        <div className="pointer-events-none absolute -bottom-48 -right-24 size-[380px] rounded-full bg-fuchsia-400/10 blur-[100px] lg:hidden" />

        <div className="relative flex items-center justify-between">
          <Logo />
          <LanguagePicker />
        </div>

        <div className="relative mx-auto flex w-full max-w-sm flex-1 flex-col justify-center py-10">
          <span className="inline-flex w-fit items-center gap-1.5 rounded-full bg-gradient-to-r from-indigo-50 to-fuchsia-50 px-3 py-1 text-xs font-medium text-indigo-700 ring-1 ring-indigo-200/70 motion-safe:animate-rise dark:from-indigo-500/10 dark:to-fuchsia-500/10 dark:text-indigo-200 dark:ring-indigo-400/20">
            <Sparkles className="size-3.5" /> {tx('New: your AI Team is here')}
          </span>
          <h1
            className="mt-4 bg-gradient-to-br from-zinc-900 via-zinc-800 to-indigo-700 bg-clip-text text-4xl font-semibold tracking-tight text-transparent motion-safe:animate-rise dark:from-white dark:via-zinc-100 dark:to-indigo-300"
            style={{ animationDelay: '60ms' }}
          >
            {tx(title)}
          </h1>
          <p className="mt-2 text-[15px] text-zinc-500 motion-safe:animate-rise dark:text-zinc-400" style={{ animationDelay: '120ms' }}>
            {tx(subtitle)}
          </p>
          <div className="mt-8 motion-safe:animate-rise" style={{ animationDelay: '180ms' }}>
            {children}
          </div>
        </div>

        <div className="relative flex flex-wrap items-center justify-between gap-2 text-xs text-zinc-500 dark:text-zinc-400">
          <span>© {new Date().getFullYear()} {APP_NAME}</span>
          <span className="flex items-center gap-1.5">
            <Lock className="size-3.5" /> {tx('Encrypted sign-in · every sign-in recorded')}
          </span>
        </div>
      </div>
      <Showcase />
    </div>
  );
}
