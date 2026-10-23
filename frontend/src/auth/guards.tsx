import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';
import { PageSpinner, EmptyState, Button } from '../components/ui';
import type { Me, UserRole } from '../types';
import { useAuth } from './AuthContext';
import { effectiveRole } from './roles';
import { moduleForPath } from '../components/layout/nav';

export function homeFor(me: Me): string {
  return effectiveRole(me) === 'SuperAdmin' ? '/platform' : '/dashboard';
}

/** Requires a signed-in user. */
export function RequireAuth({ children }: { children?: ReactNode }) {
  const { user, initializing } = useAuth();
  const location = useLocation();
  if (initializing) return <PageSpinner label="Restoring your session" />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return children ? <>{children}</> : <Outlet />;
}

/** Only for signed-out visitors (login). */
export function GuestOnly({ children }: { children?: ReactNode }) {
  const { user, initializing } = useAuth();
  if (initializing) return <PageSpinner />;
  if (user) return <Navigate to={homeFor(user)} replace />;
  return children ? <>{children}</> : <Outlet />;
}

/**
 * Renders children only for the given (effective) roles; otherwise a "no access" screen.
 * The owner outside a company is sent to the platform; inside a company they count as CompanyAdmin.
 */
export function RequireRole({ roles, children }: { roles: UserRole[]; children?: ReactNode }) {
  const { user } = useAuth();
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace />;
  const role = effectiveRole(user)!;
  const module = moduleForPath(location.pathname);
  const granted = !!user.modules && !!module && user.modules.includes(module);
  if (!roles.includes(role) && !granted) {
    // Owner in platform mode on a company page, or anyone on a platform page: go home.
    if (role === 'SuperAdmin' || roles.every((r) => r === 'SuperAdmin')) {
      return <Navigate to={homeFor(user)} replace />;
    }
    return <NoAccess />;
  }
  return children ? <>{children}</> : <Outlet />;
}

/** Self-service pages: only for logins linked to an employee record. */
export function RequireEmployee({ children }: { children?: ReactNode }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (!user.employeeId) return <Navigate to={homeFor(user)} replace />;
  return children ? <>{children}</> : <Outlet />;
}

export function NoAccess() {
  return (
    <EmptyState
      icon={ShieldAlert}
      title="You don't have access to this page"
      description="Ask your Company Super Admin if you think you should."
      action={
        <Button variant="secondary" onClick={() => window.history.back()}>
          Go back
        </Button>
      }
    />
  );
}
