import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { authApi } from '../api/auth';
import { ACCESS_BLOCK_CODES, getPlatformToken, isAccessBlocked, getToken, onAuthFailure, setPlatformToken, setToken } from '../api/client';
import { platformApi } from '../api/platform';
import { useToast } from '../components/ui';
import type { LoginRequest, Me, Session } from '../types';

interface AuthState {
  user: Me | null;
  /** Why the user was signed out (company inactive/expired, login disabled); shown on the login page. */
  notice: string | null;
  clearNotice: () => void;
  /** True while stored tokens are being validated on first load. */
  initializing: boolean;
  login: (req: LoginRequest) => Promise<Me>;
  logout: () => Promise<void>;
  /** Owner only: open a company with full admin rights. Keeps the platform session aside. */
  enterCompany: (companyId: string) => Promise<Me>;
  /** Owner only: leave the company and restore the platform session. */
  exitCompany: () => Promise<Me | null>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Me | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const navigate = useNavigate();
  const userRef = useRef<Me | null>(null);
  userRef.current = user;
  const [initializing, setInitializing] = useState(() => !!getToken() || !!getPlatformToken());
  const queryClient = useQueryClient();
  const toast = useToast();

  const clearSession = useCallback(() => {
    setToken(null);
    setPlatformToken(null);
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  // Restore the session(s) from stored tokens.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const active = getToken();
      const platform = getPlatformToken();
      let me: Me | null = null;
      if (active) {
        try {
          me = await authApi.me(active);
        } catch (err) {
          me = null;
          if (isAccessBlocked(err) && !cancelled) setNotice(err.message);
        }
      }
      if (me && !me.isActingAsOwner && platform) {
        // Stale leftover from an earlier company visit.
        setPlatformToken(null);
      }
      if (!me && platform) {
        // The company token expired or is invalid: fall back to the owner's platform session.
        try {
          me = await authApi.me(platform);
          setToken(platform);
          setPlatformToken(null);
        } catch {
          me = null;
        }
      }
      if (cancelled) return;
      if (me) setUser(me);
      else {
        setToken(null);
        setPlatformToken(null);
      }
      setInitializing(false);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // 401 anywhere -> sign out completely; 403 -> permission toast.
  useEffect(
    () =>
      onAuthFailure((status, problem) => {
        // Company inactive/expired or login disabled: sign out at once and explain on the login page.
        // The owner acting inside a company is never blocked.
        if (status === 403 && ACCESS_BLOCK_CODES.includes(problem.code ?? '') && !userRef.current?.isActingAsOwner) {
          clearSession();
          setNotice(problem.title ?? 'Your access has been blocked.');
          navigate('/login', { replace: true });
          return;
        }
        if (status === 401) {
          if (getToken()) toast.error('Your session has expired', 'Please sign in again.');
          clearSession();
        } else {
          toast.error("You don't have permission");
        }
      }),
    [clearSession, toast, navigate],
  );

  const accept = useCallback(
    (session: Session) => {
      queryClient.clear();
      setToken(session.token);
      setUser(session.user);
      return session.user;
    },
    [queryClient],
  );

  const login = useCallback(
    async (req: LoginRequest) => {
      setPlatformToken(null);
      setNotice(null);
      return accept(await authApi.login(req));
    },
    [accept],
  );

  const enterCompany = useCallback(
    async (companyId: string) => {
      // Always enter from the platform token, even if already inside another company.
      const platform = getPlatformToken() ?? getToken();
      if (!platform) throw new Error('Not signed in.');
      if (!getPlatformToken()) setPlatformToken(platform);
      const session = await platformApi.enterCompany(companyId).catch((err) => {
        if (getToken() === platform) setPlatformToken(null);
        throw err;
      });
      return accept(session);
    },
    [accept],
  );

  const exitCompany = useCallback(async () => {
    const platform = getPlatformToken();
    if (!platform) {
      clearSession();
      return null;
    }
    try {
      const me = await authApi.me(platform);
      queryClient.clear();
      setToken(platform);
      setPlatformToken(null);
      setUser(me);
      return me;
    } catch {
      toast.error('Your platform session has expired', 'Please sign in again.');
      clearSession();
      return null;
    }
  }, [clearSession, queryClient, toast]);

  const logout = useCallback(async () => {
    const tokens = [getToken(), getPlatformToken()].filter((t): t is string => !!t);
    await Promise.all(tokens.map((t) => authApi.logout(t).catch(() => undefined)));
    clearSession();
  }, [clearSession]);

  const clearNotice = useCallback(() => setNotice(null), []);
  const value = useMemo(
    () => ({ user, notice, clearNotice, initializing, login, logout, enterCompany, exitCompany }),
    [user, notice, clearNotice, initializing, login, logout, enterCompany, exitCompany],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

/** The signed-in user; only call inside a guarded route. */
export function useUser(): Me {
  const { user } = useAuth();
  if (!user) throw new Error('No signed-in user');
  return user;
}
