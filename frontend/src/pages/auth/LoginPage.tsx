import { useId, useState, type FormEvent, type KeyboardEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { homeFor } from '../../auth/guards';
import { errorMessage, isAccessBlocked } from '../../api/client';
import { ArrowRight, Eye, EyeOff, KeyRound, Mail, ShieldOff } from 'lucide-react';
import { FormError, Input, Spinner } from '../../components/ui';
import { AuthLayout } from './AuthLayout';
import { tx } from '../../lib/i18n';

export function LoginPage() {
  const { login, notice, clearNotice } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState<string | null>(null);
  const alert = blocked ?? notice;
  const [loading, setLoading] = useState(false);
  const [show, setShow] = useState(false);
  const [caps, setCaps] = useState(false);
  const emailId = useId();
  const passwordId = useId();
  const onKey = (e: KeyboardEvent<HTMLInputElement>) => setCaps(e.getModifierState?.('CapsLock') ?? false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBlocked(null);
    clearNotice();
    setLoading(true);
    try {
      const user = await login({ email: email.trim(), password });
      const fitsRole = !!from && from !== '/' && (user.role === 'SuperAdmin') === from.startsWith('/platform');
      const target = fitsRole && from ? from : homeFor(user);
      navigate(target, { replace: true });
    } catch (err) {
      if (isAccessBlocked(err)) setBlocked(err.message);
      else setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout
      title="Welcome back"
      subtitle="Sign in with the email your company gave you."
    >
      <form onSubmit={submit} className="space-y-4">
        {alert && (
          <div
            role="alert"
            className="flex items-start gap-3 rounded-xl border border-red-300 bg-red-50 p-4 text-sm text-red-800 dark:border-red-500/40 dark:bg-red-500/10 dark:text-red-200"
          >
            <ShieldOff className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
            <div>
              <p className="font-semibold">Access blocked</p>
              <p className="mt-0.5">{alert}</p>
            </div>
          </div>
        )}
        <FormError message={error} />
        <div className="space-y-1.5">
          <label htmlFor={emailId} className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
            {tx('Work email')}
          </label>
          <div className="group relative">
            <Mail className="pointer-events-none absolute top-1/2 start-3.5 size-4 -translate-y-1/2 text-zinc-400 transition group-focus-within:text-accent-600" />
            <Input
              id={emailId}
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@company.pk"
              required
              autoFocus
              className="h-12 rounded-xl ps-10 text-[15px] shadow-sm transition focus:shadow-[0_0_0_4px_rgba(99,102,241,0.12)] sm:h-12"
            />
          </div>
        </div>
        <div className="space-y-1.5">
          <label htmlFor={passwordId} className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
            {tx('Password')}
          </label>
          <div className="group relative">
            <KeyRound className="pointer-events-none absolute top-1/2 start-3.5 size-4 -translate-y-1/2 text-zinc-400 transition group-focus-within:text-accent-600" />
            <Input
              id={passwordId}
              type={show ? 'text' : 'password'}
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onKeyUp={onKey}
              onKeyDown={onKey}
              required
              className="h-12 rounded-xl ps-10 pe-11 text-[15px] shadow-sm transition focus:shadow-[0_0_0_4px_rgba(99,102,241,0.12)] sm:h-12"
            />
            <button
              type="button"
              onClick={() => setShow((v) => !v)}
              aria-label={tx(show ? 'Hide password' : 'Show password')}
              aria-pressed={show}
              className="absolute top-1/2 end-2 flex size-8 -translate-y-1/2 items-center justify-center rounded-lg text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-700 focus-visible:outline-2 focus-visible:outline-accent-500 dark:hover:bg-zinc-800 dark:hover:text-zinc-200"
            >
              {show ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </div>
          {caps && <p className="text-xs font-medium text-amber-600 dark:text-amber-400">{tx('Caps Lock is on')}</p>}
        </div>
        <button
          type="submit"
          disabled={loading}
          className="group relative mt-2 flex h-12 w-full items-center justify-center gap-2 overflow-hidden rounded-xl bg-gradient-to-r from-indigo-600 via-violet-600 to-fuchsia-600 text-[15px] font-semibold text-white shadow-lg shadow-indigo-600/30 transition hover:shadow-xl hover:shadow-indigo-600/40 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-500 active:scale-[0.99] disabled:opacity-80"
        >
          <span className="pointer-events-none absolute inset-y-0 left-0 w-1/3 bg-gradient-to-r from-transparent via-white/30 to-transparent motion-safe:animate-shine" />
          {loading ? <Spinner className="size-5 text-white" /> : null}
          <span className="relative">{tx('Sign in')}</span>
          {!loading && <ArrowRight className="relative size-4 transition-transform group-hover:translate-x-0.5 rtl:rotate-180" />}
        </button>
        <p className="text-center text-xs text-zinc-500 dark:text-zinc-400">
          {tx('Accounts are created by your HR provider.')}
        </p>
      </form>
    </AuthLayout>
  );
}
