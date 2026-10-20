import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { homeFor } from '../../auth/guards';
import { errorMessage, isAccessBlocked } from '../../api/client';
import { ShieldOff } from 'lucide-react';
import { Button, Field, FormError, Input } from '../../components/ui';
import { AuthLayout } from './AuthLayout';

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
        <Field label="Work email">
          <Input
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@company.pk"
            required
            autoFocus
          />
        </Field>
        <Field label="Password">
          <Input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </Field>
        <Button type="submit" size="lg" className="w-full" loading={loading}>
          Sign in
        </Button>
        <p className="text-center text-xs text-zinc-500 dark:text-zinc-400">
          Accounts are created by your HR provider.
        </p>
      </form>
    </AuthLayout>
  );
}
